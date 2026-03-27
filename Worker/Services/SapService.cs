using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Worker.Config;
using Worker.Models;

namespace Worker.Services
{
    public class SapService
    {
        private readonly HttpClient _http;
        private readonly AppSettings _settings;
        private readonly ILogger<SapService> _logger;

        public SapService(HttpClient http, IOptions<AppSettings> settings, ILogger<SapService> logger)
        {
            _http = http;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<bool> Send(SapPayload payload, string type)
        {
            var endpoint = type switch
            {
                "AP" => _settings.ApEndpoint,
                "AR" => _settings.ArEndpoint,
                _ => throw new Exception($"Unknown transaction type: {type}")
            };

            var response = await _http.PostAsJsonAsync(endpoint, payload);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("HTTP Error {StatusCode} : {Body}", response.StatusCode, body);
                return false;
            }

            if (body.Contains("\"processStatus\":\"error\""))
            {
                _logger.LogWarning("Business Error : {Body}", body);
                return false;
            }

            return true;
        }
    }
}
