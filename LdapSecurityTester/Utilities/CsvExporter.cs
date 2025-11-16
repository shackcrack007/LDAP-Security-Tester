using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LdapSecurityTester.Models;

namespace LdapSecurityTester.Utilities
{
    public class CsvExporter
    {
        public void ExportResults(List<TestResult> results, string filePath)
        {
            var csv = new StringBuilder();
            csv.AppendLine("TestName,Success,Details,Error,Timestamp,Duration");

            foreach (var result in results)
            {
                csv.AppendLine($"\"{EscapeCsvField(result.TestName)}\",{result.Success},\"{EscapeCsvField(result.Details)}\",\"{EscapeCsvField(result.Error)}\",\"{result.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{result.Duration.TotalMilliseconds}\"");
            }

            File.WriteAllText(filePath, csv.ToString());
        }

        private string EscapeCsvField(string? field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            // Replace quotes with double quotes
            return field.Replace("\"", "\"\"");
        }
    }
}
