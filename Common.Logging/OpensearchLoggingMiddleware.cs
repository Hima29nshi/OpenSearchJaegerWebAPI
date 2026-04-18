using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Web;

namespace Common.Logging
{
    public class OpensearchLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public OpensearchLoggingMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var logEntry = new LogEntry
            {
                ApplicationName = _configuration["ApplicationName"]?.ToLower(),
                Timestamp = DateTime.UtcNow,
                RequestPath = context.Request.Path,
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                RequestScheme = context.Request.Scheme,
                StatusCode = context.Response.StatusCode,
                Protocol = context.Request.Protocol,
                Host = context.Request.Host.Value
            };
            try
            {
                var logging = new Client(_configuration);
                var client = logging.CreateOpensearchClient();
                await client.IndexAsync(logEntry, idx => idx.Index(logEntry.ApplicationName + "-" + DateTime.UtcNow.ToString("yyyy.MM.dd")));
                await _next(context);
            }
            catch (Exception ex)
            {
                // Handle exceptions related to OpenSearch client creation
                Console.WriteLine($"Error creating OpenSearch client: {ex.Message}");
            }
        }
    }

    public static class OpensearchLoggingMiddlewareExtensions
    {
        public static void AddLogger(this WebApplicationBuilder builder)
        {
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
            builder.Host.UseNLog();
        }

        public static IApplicationBuilder UseLogger(this IApplicationBuilder builder, IConfiguration configuration)
        {
            return builder.UseMiddleware<OpensearchLoggingMiddleware>(configuration);
        }
    }
}
