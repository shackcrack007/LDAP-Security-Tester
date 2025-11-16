namespace LdapSecurityTester.Models
{
    public class LdapTestResult
    {
        public bool Success { get; set; }
        public string? Details { get; set; }
        public string? Error { get; set; }
    }
}
