using Common.Logging;
using Common.Tracing;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddTracing(builder.Configuration);
builder.AddLogger();
builder.Services.AddMetrics(builder.Configuration);
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseLogger(app.Configuration);
app.UseTracing();
app.MapPrometheusScrapingEndpoint();

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();
