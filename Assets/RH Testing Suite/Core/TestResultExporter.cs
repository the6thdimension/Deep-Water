using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Text;
using System.Linq;

namespace RH.Testing
{
    /// <summary>
    /// Handles exporting test results to various formats
    /// </summary>
    public static class TestResultExporter
    {
        /// <summary>
        /// Serializable class to store test results
        /// </summary>
        [Serializable]
        private class SerializableTestResults
        {
            public List<TestResult> Results = new List<TestResult>();
            public int TotalTests;
            public int PassedTests;
            public int FailedTests;
            public float TotalDuration;
            public string ExportDate;
        }
        
        /// <summary>
        /// Exports test results to a file
        /// </summary>
        public static void ExportResults(string filePath)
        {
            try
            {
                // Get all test results
                List<TestResult> results = TestRunner.GetAllTestResults();
                
                // Create a serializable container
                SerializableTestResults container = new SerializableTestResults
                {
                    Results = results,
                    TotalTests = results.Count,
                    PassedTests = results.Count(r => r.Success),
                    FailedTests = results.Count(r => !r.Success),
                    TotalDuration = results.Sum(r => r.ExecutionDuration),
                    ExportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                
                // Determine the export format based on file extension
                string extension = Path.GetExtension(filePath).ToLower();
                
                switch (extension)
                {
                    case ".json":
                        ExportToJson(filePath, container);
                        break;
                    case ".csv":
                        ExportToCsv(filePath, results);
                        break;
                    case ".html":
                        ExportToHtml(filePath, container);
                        break;
                    default:
                        // Default to JSON
                        ExportToJson(filePath, container);
                        break;
                }
                
                Debug.Log($"Test results exported to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error exporting test results: {e.Message}");
            }
        }
        
        /// <summary>
        /// Exports test results to JSON format
        /// </summary>
        private static void ExportToJson(string filePath, SerializableTestResults container)
        {
            string json = JsonUtility.ToJson(container, true);
            File.WriteAllText(filePath, json);
        }
        
        /// <summary>
        /// Exports test results to CSV format
        /// </summary>
        private static void ExportToCsv(string filePath, List<TestResult> results)
        {
            StringBuilder csv = new StringBuilder();
            
            // Add header
            csv.AppendLine("Test Name,Success,Message,Execution Time,Duration (seconds)");
            
            // Add data rows
            foreach (var result in results)
            {
                csv.AppendLine($"\"{result.TestName}\",{result.Success},\"{result.Message}\",\"{result.ExecutionTime}\",{result.ExecutionDuration}");
            }
            
            File.WriteAllText(filePath, csv.ToString());
        }
        
        /// <summary>
        /// Exports test results to HTML format
        /// </summary>
        private static void ExportToHtml(string filePath, SerializableTestResults container)
        {
            StringBuilder html = new StringBuilder();
            
            // Add HTML header
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("  <title>RH Test Suite Results</title>");
            html.AppendLine("  <style>");
            html.AppendLine("    body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("    h1 { color: #333; }");
            html.AppendLine("    .summary { background-color: #f5f5f5; padding: 10px; margin-bottom: 20px; border-radius: 5px; }");
            html.AppendLine("    .passed { color: green; }");
            html.AppendLine("    .failed { color: red; }");
            html.AppendLine("    table { border-collapse: collapse; width: 100%; }");
            html.AppendLine("    th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            html.AppendLine("    th { background-color: #f2f2f2; }");
            html.AppendLine("    tr:nth-child(even) { background-color: #f9f9f9; }");
            html.AppendLine("  </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            // Add title
            html.AppendLine("  <h1>RH Test Suite Results</h1>");
            
            // Add summary
            html.AppendLine("  <div class=\"summary\">");
            html.AppendLine($"    <p><strong>Export Date:</strong> {container.ExportDate}</p>");
            html.AppendLine($"    <p><strong>Total Tests:</strong> {container.TotalTests}</p>");
            html.AppendLine($"    <p><strong>Passed Tests:</strong> <span class=\"passed\">{container.PassedTests}</span></p>");
            html.AppendLine($"    <p><strong>Failed Tests:</strong> <span class=\"failed\">{container.FailedTests}</span></p>");
            html.AppendLine($"    <p><strong>Total Duration:</strong> {container.TotalDuration:F3} seconds</p>");
            html.AppendLine("  </div>");
            
            // Add results table
            html.AppendLine("  <h2>Test Results</h2>");
            html.AppendLine("  <table>");
            html.AppendLine("    <tr>");
            html.AppendLine("      <th>Test Name</th>");
            html.AppendLine("      <th>Status</th>");
            html.AppendLine("      <th>Message</th>");
            html.AppendLine("      <th>Execution Time</th>");
            html.AppendLine("      <th>Duration (seconds)</th>");
            html.AppendLine("    </tr>");
            
            foreach (var result in container.Results)
            {
                string statusClass = result.Success ? "passed" : "failed";
                string statusText = result.Success ? "Passed" : "Failed";
                
                html.AppendLine("    <tr>");
                html.AppendLine($"      <td>{result.TestName}</td>");
                html.AppendLine($"      <td class=\"{statusClass}\">{statusText}</td>");
                html.AppendLine($"      <td>{result.Message}</td>");
                html.AppendLine($"      <td>{result.ExecutionTime}</td>");
                html.AppendLine($"      <td>{result.ExecutionDuration:F3}</td>");
                html.AppendLine("    </tr>");
            }
            
            html.AppendLine("  </table>");
            
            // Add HTML footer
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            File.WriteAllText(filePath, html.ToString());
        }
    }
}
