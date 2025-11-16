using System;
using System.Collections.Generic;

namespace LdapSecurityTester.Models
{
    public class TestConfiguration
    {
        public string DomainController { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public int LdapPort { get; set; } = 389;
        public int LdapsPort { get; set; } = 636;
        public List<string> AuthTypes { get; set; } = new List<string> { "Kerberos", "NTLM", "Negotiate" };
        public bool RequireSigning { get; set; }
        public bool RequireSealing { get; set; }
        public bool TestSigning { get; set; } = true;
        public bool TestSealing { get; set; } = true;
        public bool UseSsl { get; set; }
        public bool Interactive { get; set; } = true;
        public int PauseBetweenTests { get; set; } = 0;
        public int ParallelTests { get; set; } = 1;
        public TimeSpan TestTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public string OutputFormat { get; set; } = "csv";
        public string? OutputPath { get; set; }
        public bool Verbose { get; set; }
        public bool InsecureTls { get; set; }
    }
}
