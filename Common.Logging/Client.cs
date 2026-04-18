using Microsoft.Extensions.Configuration;
using OpenSearch.Client;

namespace Common.Logging
{
    public class Client
    {
        private readonly IConfiguration _configuration;
        private readonly string _connection;

        public Client(IConfiguration configuration)
        {
            _configuration = configuration;
            _connection = _configuration["OpenSearch:Url"] ?? throw new ArgumentNullException("Opensearch URL not found");
        }

        public OpenSearchClient CreateOpensearchClient()
        {
            var uri = new Uri(_connection);
            var settings = new ConnectionSettings(uri)
                                .MaximumRetries(5)
                                .RequestTimeout(TimeSpan.FromSeconds(4))
                                .MaxRetryTimeout(TimeSpan.FromSeconds(12))
                                .EnableDebugMode();
            var client = new OpenSearchClient(settings);
            if (client.Ping().IsValid)
            {
                Console.WriteLine("Successfully connected to OpenSearch");
                return client;
            }
            else
            {
                Console.WriteLine("Failed to connect to OpenSearch");
                throw new Exception("Failed to connect to OpenSearch");
            }
        }
    }
}
