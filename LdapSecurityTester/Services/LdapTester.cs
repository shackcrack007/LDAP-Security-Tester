using System;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LdapSecurityTester.Models;
using Microsoft.Extensions.Logging;

namespace LdapSecurityTester.Services
{
    public class LdapTester
    {
        private readonly ILogger<LdapTester> _logger;

        public LdapTester(ILogger<LdapTester> logger)
        {
            _logger = logger;
        }

        public async Task<LdapTestResult> TestConnectionAsync(TestCase testCase, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => TestConnection(testCase, cancellationToken), cancellationToken);
        }

        private LdapTestResult TestConnection(TestCase testCase, CancellationToken cancellationToken)
        {
            LdapConnection? ldapConnection = null;
            try
            {
                _logger.LogDebug("Starting LDAP test: {TestName}", testCase.Name);

                var identifier = new LdapDirectoryIdentifier(testCase.Server, testCase.Port);
                ldapConnection = new LdapConnection(identifier);

                // Configure SSL/TLS
                if (testCase.UseSsl)
                {
                    ldapConnection.SessionOptions.SecureSocketLayer = true;
                    ldapConnection.SessionOptions.VerifyServerCertificate = (con, cert) => true; // TODO: Make configurable
                }

                // Configure signing and sealing
                ldapConnection.SessionOptions.Sealing = testCase.RequireSealing;
                ldapConnection.SessionOptions.Signing = testCase.RequireSigning;

                _logger.LogDebug("LDAP options - Signing: {Signing}, Sealing: {Sealing}",
                    ldapConnection.SessionOptions.Signing, ldapConnection.SessionOptions.Sealing);

                // Configure authentication type
                ldapConnection.AuthType = testCase.AuthType.ToLowerInvariant() switch
                {
                    "ntlm" => AuthType.Ntlm,
                    "kerberos" => AuthType.Kerberos,
                    "negotiate" => AuthType.Negotiate,
                    "basic" => AuthType.Basic,
                    _ => AuthType.Anonymous
                };

                // Configure credentials
                if (!string.IsNullOrEmpty(testCase.Username) && !string.IsNullOrEmpty(testCase.Password))
                {
                    NetworkCredential credential;

                    if (testCase.AuthType.ToLowerInvariant() == "kerberos" && !string.IsNullOrEmpty(testCase.Domain))
                    {
                        credential = new NetworkCredential($"{testCase.Username}@{testCase.Domain}", testCase.Password);
                        _logger.LogDebug("Using Kerberos credentials for {Username}@{Domain}", testCase.Username, testCase.Domain);
                    }
                    else
                    {
                        credential = new NetworkCredential(testCase.Username, testCase.Password, testCase.Domain);
                        _logger.LogDebug("Using credentials for {Domain}\\{Username}", testCase.Domain, testCase.Username);
                    }

                    ldapConnection.Credential = credential;
                }
                else if (ldapConnection.AuthType == AuthType.Basic)
                {
                    return new LdapTestResult
                    {
                        Success = false,
                        Error = "Username and password are required for Basic authentication."
                    };
                }

                // Check for cancellation before bind
                cancellationToken.ThrowIfCancellationRequested();

                // Perform bind
                ldapConnection.Bind();
                _logger.LogDebug("LDAP bind successful");

                // Get default naming context
                var rootSearchRequest = new SearchRequest("", "(objectClass=*)", SearchScope.Base, "defaultNamingContext");
                var rootSearchResponse = (SearchResponse)ldapConnection.SendRequest(rootSearchRequest);

                if (rootSearchResponse.Entries.Count == 0 ||
                    rootSearchResponse.Entries[0].Attributes["defaultNamingContext"] == null ||
                    rootSearchResponse.Entries[0].Attributes["defaultNamingContext"].Count == 0)
                {
                    throw new Exception("Could not retrieve default naming context.");
                }

                string? defaultNC = rootSearchResponse.Entries[0].Attributes["defaultNamingContext"][0] as string;
                if (string.IsNullOrEmpty(defaultNC))
                {
                    throw new Exception("Default naming context is null or empty.");
                }

                // Check for cancellation before search
                cancellationToken.ThrowIfCancellationRequested();

                // Query for domain computers to validate connection
                var computerSearchRequest = new SearchRequest(defaultNC, "(objectClass=computer)",
                    SearchScope.Subtree, "cn", "dNSHostName", "operatingSystem");
                computerSearchRequest.SizeLimit = 100;

                var computerSearchResponse = (SearchResponse)ldapConnection.SendRequest(computerSearchRequest);
                int computerCount = computerSearchResponse.Entries.Count;

                string details = $"Bind successful, queried {computerCount} computers from {defaultNC}";

                if (computerCount > 0)
                {
                    var sampleComputers = new System.Collections.Generic.List<string>();
                    for (int i = 0; i < Math.Min(3, computerCount); i++)
                    {
                        SearchResultEntry entry = computerSearchResponse.Entries[i];
                        string computerName = "Unknown";

                        if (entry.Attributes["dNSHostName"] != null && entry.Attributes["dNSHostName"].Count > 0)
                        {
                            computerName = (string)entry.Attributes["dNSHostName"][0];
                        }
                        else if (entry.Attributes["cn"] != null && entry.Attributes["cn"].Count > 0)
                        {
                            computerName = (string)entry.Attributes["cn"][0];
                        }

                        sampleComputers.Add(computerName);
                    }
                    details += $" (samples: {string.Join(", ", sampleComputers)})";
                }

                _logger.LogDebug("LDAP test completed successfully: {Details}", details);
                return new LdapTestResult { Success = true, Details = details };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("LDAP test was cancelled: {TestName}", testCase.Name);
                return new LdapTestResult { Success = false, Error = "Test was cancelled" };
            }
            catch (LdapException ldapEx)
            {
                string error = $"LDAP Error ({ldapEx.ErrorCode}): {ldapEx.Message}";
                if (!string.IsNullOrEmpty(ldapEx.ServerErrorMessage))
                {
                    error += $", ServerErrorMessage: {ldapEx.ServerErrorMessage}";
                }
                if (ldapEx.InnerException != null)
                {
                    error += $", InnerException: {ldapEx.InnerException.Message}";
                }

                _logger.LogWarning("LDAP test failed: {Error}", error);
                return new LdapTestResult { Success = false, Error = error };
            }
            catch (Exception ex)
            {
                string error = $"Exception: {ex.Message}";
                _logger.LogError(ex, "Unexpected error during LDAP test: {TestName}", testCase.Name);
                return new LdapTestResult { Success = false, Error = error };
            }
            finally
            {
                ldapConnection?.Dispose();
            }
        }
    }
}
