using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace OpensearchJaegerWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherForecastController(ILogger<WeatherForecastController> logger) : ControllerBase
    {
        private readonly ILogger<WeatherForecastController> _logger = logger;
        private static readonly string[] Summaries =
        [
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        ];

        [HttpGet]
        public object Get()
        {
            _logger.LogInformation("Received request for weather forecast.");
            _logger.LogDebug("Debug message");
            _logger.LogTrace("Trace message");
            _logger.LogError("Error message");
            _logger.LogWarning("Warning message");
            _logger.LogCritical("Critical message");
            _logger.LogInformation("Information message");
            return new
            {
                TraceId = Activity.Current?.TraceId.ToString(),
                Data = Enumerable.Range(1, 5).Select(index => new WeatherForecast
                {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                })
                .ToArray()
            };
        }
    }
}
