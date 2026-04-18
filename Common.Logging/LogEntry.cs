using System.Diagnostics;
using System.Net;

namespace Common.Logging
{
    public class LogEntry
    {
        public string? ApplicationName { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Message { get; set; }
        public string? TraceId { get; set; } = Activity.Current?.TraceId.ToString();
        public string? RequestPath { get; set; }
        public string? UserAgent { get; set; }
        public string? RequestScheme { get; set; }
        public int StatusCode { get; set; }
        public string? Protocol { get; set; }
        public string? Host { get; set; }
    }
}
