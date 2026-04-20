using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Worker.Config;
using Worker.Helpers;
using Worker.Models;

namespace Worker.Services
{
    public class PocketbaseService
    {
        private readonly HttpClient _http;
        private readonly AppSettings _config;
        private readonly ILogger<PocketbaseService> _logger;
        private readonly SemaphoreSlim _authLock = new(1, 1);
        private string? _token;

        public PocketbaseService(
            HttpClient http,
            IOptions<AppSettings> config,
            ILogger<PocketbaseService> logger)
        {
            _http = http;
            _config = config.Value;
            _logger = logger;

            _http.BaseAddress = new Uri(_config.PocketbaseUrl);
        }

        public async Task Authenticate(bool forceRefresh = false)
        {
            if (!forceRefresh && !string.IsNullOrEmpty(_token))
                return;

            await _authLock.WaitAsync();
            try
            {
                if (!forceRefresh && !string.IsNullOrEmpty(_token))
                    return;

                _token = null;
                _http.DefaultRequestHeaders.Authorization = null;

                var body = new
                {
                    identity = _config.PocketbaseUser,
                    password = _config.PocketbasePassword
                };

                var json = JsonSerializer.Serialize(body);

                var content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

                var res = await _http.PostAsync(
                    "/api/collections/_superusers/auth-with-password",
                    content);

                res.EnsureSuccessStatusCode();

                var result =
                    await res.Content.ReadFromJsonAsync<AuthResponse>();

                if (result == null || string.IsNullOrEmpty(result.token))
                    throw new InvalidOperationException("Pocketbase authentication returned empty token.");

                _token = result.token;

                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _token);
            }
            finally
            {
                _authLock.Release();
            }
        }

        private async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
        {
            await Authenticate();

            var res = await send();

            if (res.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("Pocketbase token rejected (401). Re-authenticating and retrying.");
                res.Dispose();

                await Authenticate(forceRefresh: true);
                res = await send();
            }

            res.EnsureSuccessStatusCode();
            return res;
        }

        public async Task<List<TransactionGroup>> GetPendingGroups()
        {
            using var res = await SendAsync(() => _http.GetAsync(
                "/api/collections/transaction_groups/records?filter=sent_to_sap_at=null"));

            var result = await res.Content.ReadFromJsonAsync<PocketResponse<TransactionGroup>>(JsonHelper.Options);

            return result?.items ?? new List<TransactionGroup>();
        }

        public async Task<ApTransaction?> GetApTransaction(string groupId)
        {
            using var res = await SendAsync(() => _http.GetAsync(
                $"/api/collections/ap_transactions/records?filter=group_id='{groupId}'"));

            var result = await res.Content.ReadFromJsonAsync<PocketResponse<ApTransaction>>(JsonHelper.Options);

            return result?.items?.FirstOrDefault();
        }

        public async Task<List<ApSubTransaction>> GetApSubTransaction(string groupId)
        {
            using var res = await SendAsync(() => _http.GetAsync(
                $"/api/collections/ap_sub_transactions/records?filter=group_id='{groupId}'"));

            var result = await res.Content.ReadFromJsonAsync<PocketResponse<ApSubTransaction>>(JsonHelper.Options);

            return result?.items ?? new List<ApSubTransaction>();
        }

        public async Task<List<ApTransactionPurcTax>> GetApTransactionPurcTax(string groupId)
        {
            using var res = await SendAsync(() => _http.GetAsync(
                $"/api/collections/ap_transaction_purc_taxes/records?filter=group_id='{groupId}'"));

            var result = await res.Content.ReadFromJsonAsync<PocketResponse<ApTransactionPurcTax>>(JsonHelper.Options);

            return result?.items ?? new List<ApTransactionPurcTax>();
        }

        public async Task<List<ApTransactionAcc>> GetApTransactionAcc(string groupId)
        {
            using var res = await SendAsync(() => _http.GetAsync(
                $"/api/collections/ap_transaction_accs/records?filter=group_id='{groupId}'"));

            var result = await res.Content.ReadFromJsonAsync<PocketResponse<ApTransactionAcc>>(JsonHelper.Options);

            return result?.items ?? new List<ApTransactionAcc>();
        }

        public async Task<ArTransaction?> GetArTransaction(string groupId)
        {
            using var res = await SendAsync(() => _http.GetAsync(
                $"/api/collections/ar_transactions/records?filter=group_id='{groupId}'"));

            var result = await res.Content.ReadFromJsonAsync<PocketResponse<ArTransaction>>(JsonHelper.Options);

            return result?.items?.FirstOrDefault();
        }

        public async Task<List<ArSubTransaction>> GetArSubTransaction(string groupId)
        {
            using var res = await SendAsync(() => _http.GetAsync(
                $"/api/collections/ar_sub_transactions/records?filter=group_id='{groupId}'"));

            var result = await res.Content.ReadFromJsonAsync<PocketResponse<ArSubTransaction>>(JsonHelper.Options);

            return result?.items ?? new List<ArSubTransaction>();
        }

        public async Task<List<ArTransactionAcc>> GetArTransactionAcc(string groupId)
        {
            using var res = await SendAsync(() => _http.GetAsync(
                $"/api/collections/ar_transaction_accs/records?filter=group_id='{groupId}'"));

            var result = await res.Content.ReadFromJsonAsync<PocketResponse<ArTransactionAcc>>(JsonHelper.Options);

            return result?.items ?? new List<ArTransactionAcc>();
        }

        public async Task UpdateSentDate(string id)
        {
            var payload = new
            {
                sent_to_sap_at = DateTime.UtcNow
            };

            using var res = await SendAsync(() => _http.PatchAsJsonAsync(
                $"/api/collections/transaction_groups/records/{id}",
                payload));
        }
    }
}
