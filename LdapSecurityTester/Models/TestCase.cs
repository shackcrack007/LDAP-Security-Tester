using System;

namespace LdapSecurityTester.Models
{
    public class TestCase
    {
        public string Name { get; set; } = string.Empty;
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; }
        public string AuthType { get; set; } = string.Empty;
        public bool UseSsl { get; set; }
        public bool RequireSigning { get; set; }
        public bool RequireSealing { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Domain { get; set; }
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        public override string ToString()
        {
            return $"{AuthType} (SSL:{UseSsl}, Signing:{RequireSigning}, Sealing:{RequireSealing})";
        }
    }
}
