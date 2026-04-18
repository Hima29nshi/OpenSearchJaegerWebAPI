using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace Common.Tracing
{
    public class JaegerTracingMiddleware
    {
        private readonly RequestDelegate _next;

        public JaegerTracingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);
        }
    }

    public static class JaegerTracingMiddlewareExtensions
    {
        public static void AddTracing(this IServiceCollection services, IConfiguration configuration)
        {
            string applicationName = configuration["ApplicationName"] ?? "Jaeger";

            services.AddSingleton(new ActivitySource(applicationName));

            var tracingEndpoint = configuration["Jaeger:ExporterUrl"];
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(serviceName: applicationName))
                .WithTracing(tracing =>
                {
                    tracing.AddAspNetCoreInstrumentation();
                    tracing.AddHttpClientInstrumentation(options =>
                    {
                        options.FilterHttpRequestMessage = (request) =>
                        {
                            // ❌ Exclude OpenSearch bulk API
                            if (request.RequestUri != null &&
                                request.RequestUri.AbsoluteUri.Contains("/_bulk"))
                            {
                                return false;
                            }

                            return true;
                        };
                    });
                    tracing.AddSource(applicationName);
                    if (!string.IsNullOrWhiteSpace(tracingEndpoint))
                    {
                        tracing.AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(tracingEndpoint);
                            otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                        });
                    }
                    else
                    {
                        tracing.AddConsoleExporter();
                    }
                });
        }

        public static void AddMetrics(this IServiceCollection services, IConfiguration configuration)
        {
            string applicationName = configuration["ApplicationName"] ?? "Jaeger";

            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(serviceName: applicationName))
                .WithMetrics(metrics =>
                {
                    metrics.AddMeter(applicationName);
                    metrics.AddAspNetCoreInstrumentation();
                    metrics.AddPrometheusExporter();
                });
        }
        public static IApplicationBuilder UseTracing(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JaegerTracingMiddleware>();
        }
    }
}