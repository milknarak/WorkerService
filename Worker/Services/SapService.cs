using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Worker.Models;

namespace Worker.Services
{
    public class SapService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<SapService> _logger;

        public SapService(HttpClient http, IConfiguration config, ILogger<SapService> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        public async Task<bool> Send(SapPayload payload, string type)
        {
            var endpoint = type switch
            {
                "AP" => _config["AppSettings:ApEndpoint"],
                "AR" => _config["AppSettings:ArEndpoint"],
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
