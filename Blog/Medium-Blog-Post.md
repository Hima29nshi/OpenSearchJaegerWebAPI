# Complete Guide: OpenSearch, Jaeger, Prometheus & Grafana Integration in .NET 10

## Introduction

Building production-ready applications requires comprehensive observability across three key pillars: **logging**, **tracing**, and **metrics**. In this guide, we'll integrate:

- **OpenSearch** - Centralized logging and full-text search
- **Jaeger** - Distributed tracing for request flow visualization
- **Prometheus** - Time-series metrics collection
- **Grafana** - Beautiful dashboards and visualization

By the end, you'll have a complete observability stack running in Docker with your .NET 10 application.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│          .NET 10 Web Application (ASP.NET Core)             │
│  ┌──────────────┬──────────────┬──────────────────────────┐ │
│  │   Logging    │   Tracing    │      Metrics             │ │
│  │ (Serilog)    │  (OpenTel.)  │   (OpenTelemetry)        │ │
│  └──────┬───────┴──────┬───────┴────────────┬─────────────┘ │
└─────────┼──────────────┼────────────────────┼───────────────┘
          │              │                    │
          ▼              ▼                    ▼
      ┌────────┐   ┌─────────┐          ┌──────────┐
      │OpenSrch│   │ Jaeger  │          │Prometheus│
      │        │   │         │          │          │
      └────────┘   └─────────┘          └──────────┘
          │              │                    │
          └──────────────┼────────────────────┘
                         ▼
                    ┌──────────┐
                    │ Grafana  │
                    │(Visualz.)│
                    └──────────┘
```

**Data Flow:**
1. Application emits logs, traces, and metrics
2. Each goes to its respective backend
3. Grafana queries all backends for unified dashboards

---

## Prerequisites

- **.NET 10 SDK** installed
- **Docker Desktop** running
- **Visual Studio 2026** or VS Code
- Basic knowledge of ASP.NET Core

---

## Part 1: Project Setup

### Step 1.1: Create ASP.NET Core Application

```bash
dotnet new webapi -n OpensearchJaegerWebAPI
cd OpensearchJaegerWebAPI
```

### Step 1.2: Install Required NuGet Packages

```bash
# Logging
dotnet add package Serilog
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.OpenSearch

# Tracing
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.OpenTelemetry.Protocol
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http

# Metrics
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore
```

**Screenshot Tip:** Show your terminal with completed package installations.

---

## Part 2: OpenSearch Integration (Logging)

### Step 2.1: Configure Serilog

Create `Common/Logging/LoggingExtensions.cs`:

```csharp
using Serilog;
using Serilog.Sinks.OpenSearch;

namespace Common.Logging
{
    public static class LoggingExtensions
    {
        public static void AddLogging(this WebApplicationBuilder builder, IConfiguration configuration)
        {
            var opensearchUrl = configuration["OpenSearch:Url"] ?? "http://localhost:9200";
            var opensearchUsername = configuration["OpenSearch:Username"] ?? "";
            var opensearchPassword = configuration["OpenSearch:Password"] ?? "";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.OpenSearch(new OpenSearchSinkOptions(new Uri(opensearchUrl))
                {
                    AutoRegisterTemplate = true,
                    IndexFormat = "logs-{0:yyyy.MM.dd}",
                    BasicAuthUsername = opensearchUsername,
                    BasicAuthPassword = opensearchPassword,
                })
                .CreateLogger();

            builder.Host.UseSerilog();
        }

        public static void UseLogging(this IApplicationBuilder app, IConfiguration configuration)
        {
            app.UseSerilogRequestLogging();
        }
    }
}
```

### Step 2.2: Update Program.cs

```csharp
builder.Logging.ClearProviders();
builder.AddLogging(builder.Configuration);

// ... rest of your configuration
```

### Step 2.3: Configure appsettings.json

```json
{
  "OpenSearch:Url": "http://localhost:9200",
  "OpenSearch:Username": "",
  "OpenSearch:Password": "",
  "ApplicationName": "opensearchjaeger"
}
```

**Screenshot Tip:** Show the OpenSearch configuration in appsettings.json.

---

## Part 3: Jaeger Distributed Tracing

### Step 3.1: Create Tracing Extensions

Create `Common/Tracing/JaegerTracingMiddleware.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            
            // Register ActivitySource as singleton for dependency injection
            services.AddSingleton(new ActivitySource(applicationName));

            var tracingEndpoint = configuration["Jaeger:ExporterUrl"];
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(serviceName: applicationName))
                .WithTracing(tracing =>
                {
                    tracing.AddAspNetCoreInstrumentation();
                    tracing.AddHttpClientInstrumentation();
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

        public static IApplicationBuilder UseTracing(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JaegerTracingMiddleware>();
        }
    }
}
```

### Step 3.2: Configure appsettings.json

```json
{
  "Jaeger:ExporterUrl": "http://localhost:4317"
}
```

**Screenshot Tip:** Show the Jaeger endpoint configuration.

---

## Part 4: Prometheus Metrics Collection

### Step 4.1: Add Metrics Extension

Add to your `JaegerTracingMiddleware.cs`:

```csharp
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
```

### Step 4.2: Update Program.cs Middleware Order

**Critical:** The metrics endpoint must be BEFORE `UseHttpsRedirection()`:

```csharp
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseLogger(app.Configuration);
app.UseTracing();
app.MapPrometheusScrapingEndpoint();  // ✅ BEFORE HttpsRedirection

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.Run();
```

**Screenshot Tip:** Show the middleware order with emphasis on MapPrometheusScrapingEndpoint placement.

---

## Part 5: Docker Compose Setup

### Step 5.1: Create docker-compose.yaml

```yaml
services:
  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "4317:4317"    # OTLP gRPC receiver
      - "4318:4318"    # OTLP HTTP receiver
      - "16686:16686"  # Jaeger UI
      - "14268:14268"  # Jaeger collector (Thrift)
    environment:
      - COLLECTOR_OTLP_ENABLED=true
      - LOG_LEVEL=debug
    networks:
      - monitoring

  opensearch:
    image: opensearchproject/opensearch:latest
    container_name: opensearch
    environment:
      - discovery.type=single-node
      - OPENSEARCH_JAVA_OPTS=-Xms512m -Xmx512m
      - DISABLE_SECURITY_PLUGIN=true
    ports:
      - "9200:9200"
      - "9600:9600"
    networks:
      - monitoring

  opensearch-dashboards:
    image: opensearchproject/opensearch-dashboards:latest
    container_name: opensearch-dashboards
    ports:
      - "5601:5601"
    environment:
      - OPENSEARCH_HOSTS=http://opensearch:9200
    networks:
      - monitoring
    depends_on:
      - opensearch

  prometheus:
    image: prom/prometheus
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
    ports:
      - 9090:9090
    networks:
      - monitoring
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
    extra_hosts:
      - "host.docker.internal:host-gateway"

  grafana:
    image: grafana/grafana:latest
    container_name: grafana
    restart: unless-stopped
    ports:
      - "3000:3000"
    volumes:
      - grafana-storage:/var/lib/grafana
    environment:
      - GF_SECURITY_ADMIN_USER=admin
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_USERS_ALLOW_SIGN_UP=false
    networks:
      - monitoring
    extra_hosts:
      - "host.docker.internal:host-gateway"

volumes:
  grafana-storage:

networks:
  monitoring:
    driver: bridge
```

### Step 5.2: Create prometheus.yml

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'opensearchjaeger'
    static_configs:
      - targets: ['host.docker.internal:5297']
    metrics_path: '/metrics'
    scheme: 'http'
    scrape_interval: 10s
    scrape_timeout: 10s
```

**Key Points:**
- `host.docker.internal` allows Docker containers to reach your host machine
- Port `5297` is the HTTP endpoint from launchSettings.json
- `/metrics` is the Prometheus scraping endpoint

**Screenshot Tip:** Show the docker-compose.yaml in your editor with services highlighted.

---

## Part 6: Running the Full Stack

### Step 6.1: Start Docker Containers

```powershell
# Navigate to the folder containing docker-compose.yaml
cd Common.Tracing

# Start all services
docker-compose up -d

# Verify containers are running
docker-compose ps
```

**Screenshot Tip:** Show the output of `docker-compose ps` with all services running.

### Step 6.2: Verify Each Service

| Service | URL | Expected |
|---------|-----|----------|
| Jaeger UI | http://localhost:16686 | Jaeger dashboard |
| OpenSearch Dashboards | http://localhost:5601 | Logs dashboard |
| Prometheus | http://localhost:9090 | Metrics explorer |
| Grafana | http://localhost:3000 | Grafana login (admin/admin) |

### Step 6.3: Run Your Application

```powershell
# In Visual Studio, press F5 or
dotnet run
```

**Screenshot Tip:** Show all four services running in separate browser tabs.

---

## Part 7: Verifying Traces in Jaeger

### Step 7.1: Generate Traces

Call your API endpoint:

```bash
curl http://localhost:5297/api/weatherforecast
```

### Step 7.2: View in Jaeger

1. Go to **http://localhost:16686**
2. In the **Service** dropdown, select **opensearchjaeger**
3. Click **Find Traces**

**Screenshot Tip:** 
- Show the Jaeger service dropdown with "opensearchjaeger" selected
- Show a trace timeline with HTTP span details
- Highlight the tags showing http.method, http.url, responseLength

---

## Part 8: Viewing Logs in OpenSearch

### Step 8.1: Access OpenSearch Dashboards

1. Go to **http://localhost:5601**
2. Go to **Stack Management → Index Patterns**
3. Create index pattern: `logs-*`
4. Go to **Discover** tab

**Screenshot Tip:**
- Show the index pattern creation screen
- Show the Discover view with application logs displayed
- Highlight a log entry with full details

---

## Part 9: Configuring Prometheus Data Source in Grafana

### Step 9.1: Access Grafana

1. Go to **http://localhost:3000**
2. Login with **admin / admin**
3. Go to **Configuration → Data Sources**

**Screenshot Tip:** Show the Grafana login page.

### Step 9.2: Add Prometheus Data Source

1. Click **Add data source**
2. Select **Prometheus**
3. Configure:
   - **Name:** `Prometheus`
   - **URL:** `http://prometheus:9090`  ✅ Use service name, not localhost
   - **Access:** Browser
4. Click **Save & Test**

**Screenshot Tip:**
- Show the data source configuration form
- Show the green checkmark: "Data source is working"
- Show the Prometheus service being queried

---

## Part 10: Creating Grafana Dashboards

### Step 10.1: Import Community Dashboard

1. Go to **Dashboards → Browse**
2. Click **New → Import**
3. Enter dashboard ID: **3662** (ASP.NET Core Metrics)
4. Select **Prometheus** as data source
5. Click **Import**

**Screenshot Tip:**
- Show the import dialog with dashboard ID entered
- Show the selection of Prometheus data source
- Show the imported dashboard with metrics

### Step 10.2: Create Custom Dashboard

1. Click **New → Dashboard**
2. Click **Add new panel**
3. Select Prometheus as data source
4. Enter query: `http_requests_received_total`
5. Customize visualization and save

**Common Metrics to Visualize:**

```
# HTTP Requests
http_requests_received_total
rate(http_requests_received_total[5m])

# Request Duration
http_request_duration_seconds_bucket
rate(http_request_duration_seconds_sum[5m]) / rate(http_request_duration_seconds_count[5m])

# Exceptions
http_requests_failed_total
exceptions_total

# Resource Usage
process_cpu_seconds_total
process_resident_memory_bytes
```

**Screenshot Tip:**
- Show the panel editor with a Prometheus query
- Show the final dashboard with multiple panels
- Highlight different visualization types (graph, stat, gauge)

---

## Part 11: Best Practices & Troubleshooting

### Configuration Checklist

- [ ] `appsettings.json` has correct OpenSearch URL
- [ ] `appsettings.json` has correct Jaeger endpoint (4317)
- [ ] `prometheus.yml` uses `http://host.docker.internal:5297`
- [ ] `Program.cs` has `MapPrometheusScrapingEndpoint()` BEFORE `UseHttpsRedirection()`
- [ ] All Docker containers are running: `docker-compose ps`

### Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| Jaeger: "No traces" | Ensure `/api/` is in your request URL for tracing filter |
| Prometheus: "dial tcp [::1]:9090: connection refused" | Change data source URL to `http://prometheus:9090` (not localhost) |
| OpenSearch: No logs appearing | Check Serilog config in appsettings.json and verify OpenSearch is running |
| Grafana: Data source DOWN | Verify containers are on same network and use service name, not localhost |
| Metrics endpoint 404 | Move `MapPrometheusScrapingEndpoint()` before `UseHttpsRedirection()` |

**Screenshot Tip:** Show the Prometheus targets page with all targets showing UP/GREEN.

---

## Part 12: Advanced: Custom Metrics

Create custom metrics for business logic:

```csharp
using System.Diagnostics.Metrics;

public class WeatherService
{
    private static readonly Meter _meter = new("WeatherService");
    private static readonly Counter<long> _requestCounter = 
        _meter.CreateCounter<long>("weather_requests_total");

    public void GetWeather()
    {
        _requestCounter.Add(1, new KeyValuePair<string, object>("status", "success"));
        // Your business logic
    }
}
```

Register in Program.cs:

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("WeatherService");
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddPrometheusExporter();
    });
```

**Screenshot Tip:** Show custom metrics appearing in Prometheus and Grafana.

---

## Conclusion

You now have a complete observability stack:

- ✅ **Logs** → OpenSearch (full-text search)
- ✅ **Traces** → Jaeger (request flow visualization)
- ✅ **Metrics** → Prometheus (time-series data)
- ✅ **Visualization** → Grafana (unified dashboards)

This stack is production-ready and can handle enterprise-scale applications.

### Next Steps

1. Add more custom metrics for your business domain
2. Create alert rules in Prometheus
3. Set up Grafana notifications
4. Integrate with your CI/CD pipeline
5. Implement custom instrumentation for critical paths

---

## Repository

Full source code available at: [GitHub Repository Link]

---

## Resources

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/instrumentation/net/)
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Dashboard Library](https://grafana.com/grafana/dashboards/)
- [OpenSearch Documentation](https://opensearch.org/docs/)

---

**Happy observing! 🚀**
