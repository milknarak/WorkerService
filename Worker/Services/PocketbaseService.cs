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
        private readonly PocketbaseInstance _instance;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<PocketbaseService> _logger;
        private readonly SemaphoreSlim _authLock = new(1, 1);
        private string? _token;

        public string Name => _instance.Name;

        public PocketbaseService(
            HttpClient http,
            PocketbaseInstance instance,
            TimeProvider timeProvider,
            ILogger<PocketbaseService> logger)
        {
            _http = http;
            _instance = instance;
            _timeProvider = timeProvider;
            _logger = logger;

            _http.BaseAddress = new Uri(_instance.Url);
        }

        public async Task Authenticate(bool forceRefresh = false, CancellationToken ct = default)
        {
            if (!forceRefresh && !string.IsNullOrEmpty(_token))
                return;

            await _authLock.WaitAsync(ct);
            try
            {
                if (!forceRefresh && !string.IsNullOrEmpty(_token))
                    return;

                _token = null;
                _http.DefaultRequestHeaders.Authorization = null;

                var body = new
                {
                    identity = _instance.User,
                    password = _instance.Password
                };

                var json = JsonSerializer.Serialize(body);

                var content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

                var res = await _http.PostAsync(
                    "/api/collections/_superusers/auth-with-password",
                    content,
                    ct);

                res.EnsureSuccessStatusCode();

                var result =
                    await res.Content.ReadFromJsonAsync<AuthResponse>(ct);

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

        private async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken ct)
        {
            await Authenticate(ct: ct);

            var res = await send();

            if (res.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("Pocketbase token rejected (401). Re-authenticating and retrying.");
                res.Dispose();

                await Authenticate(forceRefresh: true, ct: ct);
                res = await send();
            }

            res.EnsureSuccessStatusCode();
            return res;
        }

        public async Task<List<TransactionGroup>> GetPendingGroups(CancellationToken ct = default)
        {
            using var res = await SendAsync(() => _http.GetAsync(
                "/api/collections/transaction_groups/records?filter=sent_to_sap_at=null", ct), ct);

            var result = await res.Content.ReadFromJsonAsync<PocketResponse<TransactionGroup>>(JsonHelper.Options, ct);

            return result?.items ?? new List<TransactionGroup>();
        }

        public async Task<ApTransactionRecord?> GetApTransaction(string groupId, CancellationToken ct = default)
        {
            using var res = await SendAsync(() => _http.GetAsync(
                $"/api/collections/ap_transactions/records?filter=group_id='{groupId}'", ct), ct);

            var result = await res.Content.ReadFromJsonAsync<PocketResponse<ApTransactionRecord>>(JsonHelper.Options, ct);

            return result?.items?.FirstOrDefault();
        }

        public async Task<List<ApSubTransactionRecord>> GetApSubTransaction(string groupId, CancellationToken ct = default)
        {
            using var res = await SendAsync(() => _http.GetAsync(
                $"/api/collections/ap_sub_transactions/records?filter=group_id='{groupId}'", ct), ct);

            var result = await res.Content.ReadFromJsonAsync<PocketResponse<ApSubTransactionRecord>>(JsonHelper.Options, ct);

            return result?.items ?? new List<ApSubTransactionRecord>();
        }

        public async Task<ArTransactionRecord?> GetArTransaction(string groupId, CancellationToken ct = default)
        {
            using var res = await SendAsync(() => _http.GetAsync(
                $"/api/collections/ar_transactions/records?filter=group_id='{groupId}'", ct), ct);

            var result = await res.Content.ReadFromJsonAsync<PocketResponse<ArTransactionRecord>>(JsonHelper.Options, ct);

            return result?.items?.FirstOrDefault();
        }

        public async Task<List<ArSubTransactionRecord>> GetArSubTransaction(string groupId, CancellationToken ct = default)
        {
            using var res = await SendAsync(() => _http.GetAsync(
                $"/api/collections/ar_sub_transactions/records?filter=group_id='{groupId}'", ct), ct);

            var result = await res.Content.ReadFromJsonAsync<PocketResponse<ArSubTransactionRecord>>(JsonHelper.Options, ct);

            return result?.items ?? new List<ArSubTransactionRecord>();
        }

        public async Task<CustomerRecord?> GetCustomer(string code, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var url = $"/api/collections/master_vendors/records?filter=vendor_code='{code}'";

            _logger.LogInformation("[{Instance}] Fetching vendor with code '{Code}'", _instance.Name, code);

            try
            {
                using var res = await SendAsync(() => _http.GetAsync(url, ct), ct);

                var result = await res.Content.ReadFromJsonAsync<PocketResponse<CustomerRecord>>(JsonHelper.Options, ct);
                var vendor = result?.items?.FirstOrDefault();

                if (vendor == null)
                    _logger.LogWarning("[{Instance}] Customer not found for code '{Code}'", _instance.Name, code);

                return vendor;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(
                    "[{Instance}] Customer fetch failed for code '{Code}' — status {Status}, url {Url}",
                    _instance.Name, code, ex.StatusCode, url);
                return null;
            }
        }

        public async Task UpdateSentDate(string id, CancellationToken ct = default)
        {
            var payload = new
            {
                sent_to_sap_at = _timeProvider.GetUtcNow().UtcDateTime
            };

            using var res = await SendAsync(() => _http.PatchAsJsonAsync(
                $"/api/collections/transaction_groups/records/{id}",
                payload,
                ct), ct);
        }
    }
}
