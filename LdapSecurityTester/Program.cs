using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LdapSecurityTester.Models;
using LdapSecurityTester.Services;
using LdapSecurityTester.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LdapSecurityTester
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var rootCommand = BuildCommandLine();
            return await rootCommand.InvokeAsync(args);
        }

        private static RootCommand BuildCommandLine()
        {
            var rootCommand = new RootCommand("LDAP Security Configuration Tester");

            // Connection options
            var dcOption = new Option<string>(
                aliases: new[] { "--dc", "--domain-controller" },
                description: "Domain controller hostname or IP address")
            { IsRequired = true };

            var domainOption = new Option<string>(
                aliases: new[] { "--domain", "-D" },
                description: "Domain name")
            { IsRequired = true };

            var userOption = new Option<string?>(
                aliases: new[] { "--user", "-u" },
                description: "Username for authentication (if not provided, will prompt or use current context)");

            var passwordOption = new Option<string?>(
                aliases: new[] { "--password", "-p" },
                description: "Password for authentication (if not provided, will prompt securely)");

            // Port options
            var ldapPortOption = new Option<int>(
                "--ldap-port",
                () => 389,
                "LDAP port (default: 389)");

            var ldapsPortOption = new Option<int>(
                "--ldaps-port",
                () => 636,
                "LDAPS port (default: 636)");

            // Test configuration options
            var authTypesOption = new Option<string[]>(
                "--auth-types",
                () => new[] { "Kerberos", "NTLM", "Negotiate" },
                "Authentication types to test (comma-separated)")
            {
                AllowMultipleArgumentsPerToken = true
            };

            var useSslOption = new Option<bool>(
                "--ssl",
                "Use SSL/TLS (LDAPS)");

            var requireSigningOption = new Option<bool>(
                "--require-signing",
                "Require LDAP signing for all tests");

            var requireSealingOption = new Option<bool>(
                "--require-sealing",
                "Require LDAP sealing for all tests");

            var testSigningOption = new Option<bool>(
                "--test-signing",
                () => true,
                "Test both signing enabled and disabled");

            var testSealingOption = new Option<bool>(
                "--test-sealing",
                () => true,
                "Test both sealing enabled and disabled");

            // Execution options
            var noPauseOption = new Option<bool>(
                "--no-pause",
                "Run tests without pausing between them");

            var parallelOption = new Option<int>(
                "--parallel",
                () => 1,
                "Number of tests to run in parallel");

            var timeoutOption = new Option<int>(
                "--timeout",
                () => 30,
                "Test timeout in seconds");

            // Output options
            var outputFormatOption = new Option<string>(
                "--output-format",
                () => "csv",
                "Output format (csv, json)");

            var outputPathOption = new Option<string?>(
                "--output-path",
                "Output file path (if not specified, auto-generated)");

            var verboseOption = new Option<bool>(
                aliases: new[] { "--verbose", "-v" },
                "Enable verbose logging");

            // Add all options to root command
            rootCommand.AddOption(dcOption);
            rootCommand.AddOption(domainOption);
            rootCommand.AddOption(userOption);
            rootCommand.AddOption(passwordOption);
            rootCommand.AddOption(ldapPortOption);
            rootCommand.AddOption(ldapsPortOption);
            rootCommand.AddOption(authTypesOption);
            rootCommand.AddOption(useSslOption);
            rootCommand.AddOption(requireSigningOption);
            rootCommand.AddOption(requireSealingOption);
            rootCommand.AddOption(testSigningOption);
            rootCommand.AddOption(testSealingOption);
            rootCommand.AddOption(noPauseOption);
            rootCommand.AddOption(parallelOption);
            rootCommand.AddOption(timeoutOption);
            rootCommand.AddOption(outputFormatOption);
            rootCommand.AddOption(outputPathOption);
            rootCommand.AddOption(verboseOption);

            rootCommand.SetHandler(async (context) =>
            {
                var config = new TestConfiguration
                {
                    DomainController = context.ParseResult.GetValueForOption(dcOption)!,
                    Domain = context.ParseResult.GetValueForOption(domainOption)!,
                    Username = context.ParseResult.GetValueForOption(userOption),
                    Password = context.ParseResult.GetValueForOption(passwordOption),
                    LdapPort = context.ParseResult.GetValueForOption(ldapPortOption),
                    LdapsPort = context.ParseResult.GetValueForOption(ldapsPortOption),
                    AuthTypes = context.ParseResult.GetValueForOption(authTypesOption)!.ToList(),
                    UseSsl = context.ParseResult.GetValueForOption(useSslOption),
                    RequireSigning = context.ParseResult.GetValueForOption(requireSigningOption),
                    RequireSealing = context.ParseResult.GetValueForOption(requireSealingOption),
                    TestSigning = context.ParseResult.GetValueForOption(testSigningOption),
                    TestSealing = context.ParseResult.GetValueForOption(testSealingOption),
                    Interactive = !context.ParseResult.GetValueForOption(noPauseOption),
                    ParallelTests = context.ParseResult.GetValueForOption(parallelOption),
                    TestTimeout = TimeSpan.FromSeconds(context.ParseResult.GetValueForOption(timeoutOption)),
                    OutputFormat = context.ParseResult.GetValueForOption(outputFormatOption)!,
                    OutputPath = context.ParseResult.GetValueForOption(outputPathOption),
                    Verbose = context.ParseResult.GetValueForOption(verboseOption)
                };

                // Prompt for password if not provided
                if (string.IsNullOrEmpty(config.Password) && !string.IsNullOrEmpty(config.Username))
                {
                    config.Password = SecureInput.ReadPassword($"Enter password for {config.Username}: ");
                }

                var cancellationTokenSource = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cancellationTokenSource.Cancel();
                };

                context.ExitCode = await RunApplication(config, cancellationTokenSource.Token);
            });

            return rootCommand;
        }

        private static async Task<int> RunApplication(TestConfiguration config, CancellationToken cancellationToken)
        {
            // Setup dependency injection and logging
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        if (config.Verbose)
                        {
                            builder.SetMinimumLevel(LogLevel.Debug);
                        }
                        else
                        {
                            builder.SetMinimumLevel(LogLevel.Information);
                        }
                    });
                    services.AddSingleton(config);
                    services.AddSingleton<LdapTester>();
                    services.AddSingleton<TestRunner>();
                    services.AddSingleton<CsvExporter>();
                })
                .Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var testRunner = host.Services.GetRequiredService<TestRunner>();
            var csvExporter = host.Services.GetRequiredService<CsvExporter>();

            try
            {
                // Display startup information
                DisplayStartupInfo(config, logger);

                // Build test matrix
                var testCases = testRunner.BuildTestMatrix(config);
                logger.LogInformation("Generated {TestCount} test cases", testCases.Count);

                // Create progress reporter
                var progress = new Progress<TestProgress>(p =>
                {
                    if (config.Verbose)
                    {
                        logger.LogInformation("Progress: {Current}/{Total} ({Percent:F1}%) - {TestName}: {Status}",
                            p.CurrentTest, p.TotalTests, p.PercentComplete, p.CurrentTestName,
                            p.LastResult?.Status ?? "Running");
                    }
                    else
                    {
                        Console.Write($"\rProgress: {p.CurrentTest}/{p.TotalTests} ({p.PercentComplete:F1}%)");
                    }
                });

                // Run tests
                var results = await testRunner.RunTestsAsync(testCases, config, progress, cancellationToken);

                if (!config.Verbose)
                {
                    Console.WriteLine(); // New line after progress
                }

                // Display results summary
                DisplayResultsSummary(results, logger);

                // Export results
                var outputPath = config.OutputPath ?? $"LDAP_Test_Results_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                csvExporter.ExportResults(results, outputPath);
                logger.LogInformation("Results exported to: {OutputPath}", outputPath);

                // Display LDAP client policies (Windows only)
                DisplayLdapClientPolicies(logger);

                logger.LogInformation("Testing completed successfully!");
                return 0;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Testing was cancelled by user");
                return 1;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during testing");
                return 1;
            }
        }

        private static void DisplayStartupInfo(TestConfiguration config, ILogger logger)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("LDAP Security Configuration Tester");
            Console.ResetColor();

            logger.LogInformation("Target DC: {DomainController}", config.DomainController);
            logger.LogInformation("Domain: {Domain}", config.Domain);
            logger.LogInformation("Test User: {TestUser}",
                string.IsNullOrEmpty(config.Username) ? "Current User Context" : config.Username);
            logger.LogInformation("Auth Types: {AuthTypes}", string.Join(", ", config.AuthTypes));
            logger.LogInformation("Using SSL: {UseSsl}", config.UseSsl);
            logger.LogInformation("Parallel Tests: {ParallelTests}", config.ParallelTests);
        }

        private static void DisplayResultsSummary(System.Collections.Generic.List<TestResult> results, ILogger logger)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n=== TEST SUMMARY ===");
            Console.ResetColor();

            int totalTests = results.Count;
            int passedTests = results.Count(r => r.Success);
            int failedTests = totalTests - passedTests;

            logger.LogInformation("Total Tests: {TotalTests}", totalTests);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Passed: {passedTests}");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed: {failedTests}");
            Console.ResetColor();

            if (failedTests > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nFailed Tests:");
                Console.ResetColor();

                foreach (var failedResult in results.Where(r => !r.Success))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($" - {failedResult.TestName}: {failedResult.Error}");
                    Console.ResetColor();
                }
            }
        }

        private static void DisplayLdapClientPolicies(ILogger logger)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n=== CURRENT LDAP CLIENT POLICIES ===");
            Console.ResetColor();

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    object? ldapSigningObj = Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\LDAP",
                        "LDAPClientIntegrity", null);

                    if (ldapSigningObj != null && ldapSigningObj is int ldapSigningValue)
                    {
                        string signingValue = ldapSigningValue switch
                        {
                            0 => "None",
                            1 => "Negotiate signing",
                            2 => "Require signing",
                            _ => $"Unknown ({ldapSigningValue})"
                        };
                        logger.LogInformation("LDAP Client Integrity: {SigningValue}", signingValue);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine("LDAP Client Integrity: Not configured (default: Negotiate signing)");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("LDAP client registry settings check is only supported on Windows.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Could not read LDAP client registry settings: {Error}", ex.Message);
            }
        }
    }
}
