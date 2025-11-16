using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LdapSecurityTester.Models;
using Microsoft.Extensions.Logging;

namespace LdapSecurityTester.Services
{
    public class TestRunner
    {
        private readonly LdapTester _ldapTester;
        private readonly ILogger<TestRunner> _logger;

        public TestRunner(LdapTester ldapTester, ILogger<TestRunner> logger)
        {
            _ldapTester = ldapTester;
            _logger = logger;
        }

        public List<TestCase> BuildTestMatrix(TestConfiguration config)
        {
            var testCases = new List<TestCase>();

            foreach (var authType in config.AuthTypes)
            {
                // Generate combinations of signing and sealing based on configuration
                var signingOptions = config.TestSigning ? new[] { false, true } : new[] { config.RequireSigning };
                var sealingOptions = config.TestSealing ? new[] { false, true } : new[] { config.RequireSealing };

                foreach (var requireSigning in signingOptions)
                {
                    foreach (var requireSealing in sealingOptions)
                    {
                        var port = config.UseSsl ? config.LdapsPort : config.LdapPort;
                        var testCase = new TestCase
                        {
                            Name = $"{authType} (SSL:{config.UseSsl}, Signing:{requireSigning}, Sealing:{requireSealing})",
                            Server = config.DomainController,
                            Port = port,
                            AuthType = authType,
                            UseSsl = config.UseSsl,
                            RequireSigning = requireSigning,
                            RequireSealing = requireSealing,
                            Username = config.Username,
                            Password = config.Password,
                            Domain = config.Domain,
                            Timeout = config.TestTimeout
                        };

                        testCases.Add(testCase);
                    }
                }
            }

            _logger.LogInformation("Built test matrix with {TestCount} test cases", testCases.Count);
            return testCases;
        }

        public async Task<List<TestResult>> RunTestsAsync(List<TestCase> testCases, TestConfiguration config,
            IProgress<TestProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var results = new List<TestResult>();
            var totalTests = testCases.Count;

            _logger.LogInformation("Starting execution of {TotalTests} tests", totalTests);

            if (config.ParallelTests > 1)
            {
                results = await RunTestsInParallelAsync(testCases, config, progress, cancellationToken);
            }
            else
            {
                results = await RunTestsSequentiallyAsync(testCases, config, progress, cancellationToken);
            }

            _logger.LogInformation("Completed execution of {TotalTests} tests. Passed: {PassedCount}, Failed: {FailedCount}",
                totalTests, results.Count(r => r.Success), results.Count(r => !r.Success));

            return results;
        }

        private async Task<List<TestResult>> RunTestsSequentiallyAsync(List<TestCase> testCases, TestConfiguration config,
            IProgress<TestProgress>? progress, CancellationToken cancellationToken)
        {
            var results = new List<TestResult>();

            for (int i = 0; i < testCases.Count; i++)
            {
                var testCase = testCases[i];
                var result = await ExecuteTestAsync(testCase, cancellationToken);
                results.Add(result);

                progress?.Report(new TestProgress
                {
                    CurrentTest = i + 1,
                    TotalTests = testCases.Count,
                    CurrentTestName = testCase.Name,
                    LastResult = result
                });

                // Pause between tests if configured
                if (config.PauseBetweenTests > 0 && i < testCases.Count - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(config.PauseBetweenTests), cancellationToken);
                }
            }

            return results;
        }

        private async Task<List<TestResult>> RunTestsInParallelAsync(List<TestCase> testCases, TestConfiguration config,
            IProgress<TestProgress>? progress, CancellationToken cancellationToken)
        {
            var results = new List<TestResult>();
            var semaphore = new SemaphoreSlim(config.ParallelTests, config.ParallelTests);
            var completedCount = 0;

            var tasks = testCases.Select(async testCase =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await ExecuteTestAsync(testCase, cancellationToken);

                    lock (results)
                    {
                        results.Add(result);
                        completedCount++;

                        progress?.Report(new TestProgress
                        {
                            CurrentTest = completedCount,
                            TotalTests = testCases.Count,
                            CurrentTestName = testCase.Name,
                            LastResult = result
                        });
                    }

                    return result;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return results.OrderBy(r => testCases.FindIndex(tc => tc.Name == r.TestName)).ToList();
        }

        private async Task<TestResult> ExecuteTestAsync(TestCase testCase, CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;

            try
            {
                _logger.LogDebug("Executing test: {TestName}", testCase.Name);

                var ldapResult = await _ldapTester.TestConnectionAsync(testCase, cancellationToken);
                var duration = DateTime.Now - startTime;

                return new TestResult
                {
                    TestName = testCase.Name,
                    Success = ldapResult.Success,
                    Details = ldapResult.Details,
                    Error = ldapResult.Error,
                    Timestamp = startTime,
                    Duration = duration,
                    TestCase = testCase
                };
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                _logger.LogError(ex, "Unexpected error executing test: {TestName}", testCase.Name);

                return new TestResult
                {
                    TestName = testCase.Name,
                    Success = false,
                    Details = null,
                    Error = $"Test execution failed: {ex.Message}",
                    Timestamp = startTime,
                    Duration = duration,
                    TestCase = testCase
                };
            }
        }
    }

    public class TestProgress
    {
        public int CurrentTest { get; set; }
        public int TotalTests { get; set; }
        public string CurrentTestName { get; set; } = string.Empty;
        public TestResult? LastResult { get; set; }
        public double PercentComplete => TotalTests > 0 ? (double)CurrentTest / TotalTests * 100 : 0;
    }
}
