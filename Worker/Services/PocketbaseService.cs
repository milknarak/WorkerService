using Microsoft.Extensions.Options;
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
        private string? _token;

        public PocketbaseService(HttpClient http, IOptions<AppSettings> config)
        {
            _http = http;
            _config = config.Value;

            _http.BaseAddress = new Uri(_config.PocketbaseUrl);
        }

        public async Task Authenticate()
        {
            if (!string.IsNullOrEmpty(_token))
                return;

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

            _token = result.token;

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _token);
        }

        public async Task<List<TransactionGroup>> GetPendingGroups()
        {
            await Authenticate();

            var res = await _http.GetAsync(
                "/api/collections/transaction_groups/records?filter=sent_to_sap_at=null");

            res.EnsureSuccessStatusCode();

            var result =
                await res.Content.ReadFromJsonAsync<PocketResponse<TransactionGroup>>(JsonHelper.Options);

            return result.items;
        }

        public async Task<ApTransaction?> GetApTransaction(string groupId)
        {
            await Authenticate();

            var res = await _http.GetAsync(
                $"/api/collections/ap_transactions/records?filter=group_id='{groupId}'");

            res.EnsureSuccessStatusCode();

            var result =
                await res.Content.ReadFromJsonAsync<PocketResponse<ApTransaction>>(JsonHelper.Options);

            return result.items.FirstOrDefault();
        }

        public async Task<List<ApSubTransaction>> GetApSubTransaction(string groupId)
        {
            await Authenticate();

            var res = await _http.GetAsync(
                $"/api/collections/ap_sub_transactions/records?filter=group_id='{groupId}'");

            res.EnsureSuccessStatusCode();

            var result =
                await res.Content.ReadFromJsonAsync<PocketResponse<ApSubTransaction>>();

            return result.items;
        }

        public async Task<List<ApTransactionPurcTax>> GetApTransactionPurcTax(string groupId)
        {
            await Authenticate();

            var res = await _http.GetAsync(
                $"/api/collections/ap_transaction_purc_taxes/records?filter=group_id='{groupId}'");

            res.EnsureSuccessStatusCode();

            var result =
                await res.Content.ReadFromJsonAsync<PocketResponse<ApTransactionPurcTax>>(JsonHelper.Options);

            return result.items;
        }

        public async Task<List<ApTransactionAcc>> GetApTransactionAcc(string groupId)
        {
            await Authenticate();

            var res = await _http.GetAsync(
                $"/api/collections/ap_transaction_accs/records?filter=group_id='{groupId}'");

            res.EnsureSuccessStatusCode();

            var result =
                await res.Content.ReadFromJsonAsync<PocketResponse<ApTransactionAcc>>();

            return result.items;
        }

        public async Task<ArTransaction?> GetArTransaction(string groupId)
        {
            await Authenticate();

            var res = await _http.GetAsync(
                $"/api/collections/ar_transactions/records?filter=group_id='{groupId}'");

            res.EnsureSuccessStatusCode();

            var result =
                await res.Content.ReadFromJsonAsync<PocketResponse<ArTransaction>>(JsonHelper.Options);

            return result.items.FirstOrDefault();
        }

        public async Task<List<ArSubTransaction>> GetArSubTransaction(string groupId)
        {
            await Authenticate();

            var res = await _http.GetAsync(
                $"/api/collections/ar_sub_transactions/records?filter=group_id='{groupId}'");

            res.EnsureSuccessStatusCode();

            var result =
                await res.Content.ReadFromJsonAsync<PocketResponse<ArSubTransaction>>();

            return result.items;
        }
        public async Task<List<ArTransactionAcc>> GetArTransactionAcc(string groupId)
        {
            await Authenticate();

            var res = await _http.GetAsync(
                $"/api/collections/ar_transaction_accs/records?filter=group_id='{groupId}'");

            res.EnsureSuccessStatusCode();

            var result =
                await res.Content.ReadFromJsonAsync<PocketResponse<ArTransactionAcc>>();

            return result.items;
        }

        public async Task UpdateSentDate(string id)
        {
            await Authenticate();

            var payload = new
            {
                sent_to_sap_at = DateTime.UtcNow
            };

            var res = await _http.PatchAsJsonAsync(
                $"/api/collections/transaction_groups/records/{id}",
                payload);

            res.EnsureSuccessStatusCode();
        }
    }
}