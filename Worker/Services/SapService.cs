using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Worker.Config;
using Worker.Helpers;
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

        // ERP คืน "already in the system" (อังกฤษเสมอ) เมื่อข้อมูลถูกส่งเข้าไปแล้ว
        // เคสนี้เกิดจากคนลบ+สร้าง record ใหม่ใน PocketBase (sent_to_sap_at หาย) แต่ ERP มีข้อมูลอยู่แล้ว
        private const string AlreadyInSystemMarker = "already in the system";

        public async Task<SapSendResult> Send(SapPayload payload, TransactionType type, CancellationToken ct = default)
        {
            var endpoint = type switch
            {
                TransactionType.Ap => _settings.ApEndpoint,
                TransactionType.Ar => _settings.ArEndpoint,
                _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unknown transaction type: {type}")
            };

            return await PostAndValidate(endpoint, payload, ct);
        }

        // AR-DODO: ส่งจำนวนลิตรไป InsertArTransPriceList ให้ ERP หาราคาเอง
        public async Task<SapSendResult> SendPriceList(ArPriceListPayload payload, CancellationToken ct = default)
        {
            return await PostAndValidate(_settings.ArPriceListEndpoint, payload, ct);
        }

        private async Task<SapSendResult> PostAndValidate(string endpoint, object payload, CancellationToken ct)
        {
            var response = await _http.PostAsJsonAsync(endpoint, payload, ct);

            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("HTTP Error {StatusCode} : {Body}", response.StatusCode, body);
                return SapSendResult.Failed($"HTTP {(int)response.StatusCode}: {body}");
            }

            SapResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<SapResponse>(body, JsonHelper.Options);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse SAP response: {Body}", body);
                return SapSendResult.Failed($"Unparseable SAP response: {body}");
            }

            if (!string.Equals(parsed?.responseApi?.statusCode, "200", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("SAP API Error {StatusCode} {StatusDesc} : {Body}",
                    parsed?.responseApi?.statusCode, parsed?.responseApi?.statusDesc, body);
                return SapSendResult.Failed($"SAP API {parsed?.responseApi?.statusCode}: {parsed?.responseApi?.statusDesc}");
            }

            if (string.Equals(parsed?.responseRefData?.processStatus, "error", StringComparison.OrdinalIgnoreCase))
            {
                var errMsg = parsed?.responseRefData?.processErrMsg;

                var alreadyInSystem = errMsg != null &&
                    errMsg.Contains(AlreadyInSystemMarker, StringComparison.OrdinalIgnoreCase);

                _logger.LogWarning("SAP Business Error : {ErrMsg} | Body : {Body}", errMsg, body);
                return SapSendResult.Failed(errMsg, alreadyInSystem);
            }

            return SapSendResult.Ok();
        }
    }
}
