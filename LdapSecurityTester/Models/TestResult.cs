using System;

namespace LdapSecurityTester.Models
{
    public class TestResult
    {
        public string TestName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Details { get; set; }
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public TestCase? TestCase { get; set; }

        public string Status => Success ? "PASS" : "FAIL";
    }
}
