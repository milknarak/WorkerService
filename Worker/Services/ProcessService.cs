using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Worker.Mappers;
using Worker.Models;

namespace Worker.Services
{
    public class ProcessService
    {
        private readonly TransactionService _transactionService;
        private readonly SapService _sapService;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ProcessService> _logger;

        public ProcessService(
            TransactionService transactionService,
            SapService sapService,
            TimeProvider timeProvider,
            ILogger<ProcessService> logger)
        {
            _transactionService = transactionService;
            _sapService = sapService;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Process(CancellationToken ct = default)
        {
            var groups = await _transactionService.GetPendingGroups(ct);

            if(groups == null || !groups.Any())
            {
                _logger.LogInformation("No pending transaction groups to process.");
                return;
            }

            foreach (var g in groups)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (!TransactionTypeExtensions.TryParse(g.type, out var type))
                    {
                        _logger.LogWarning("Unknown type '{Type}' for group {GroupId} — skipping", g.type, g.group_id);
                        continue;
                    }

                    var data = await _transactionService.GetTransaction(g.group_id, type, ct);

                    if (data == null)
                    {
                        _logger.LogWarning("{Type} transaction not found for group {GroupId} — skipping", type, g.group_id);
                        continue;
                    }

                    var now = _timeProvider.GetLocalNow().LocalDateTime;

                    var isDodo = type == TransactionType.Ar &&
                                 string.Equals(g.sub_type, "DODO", StringComparison.OrdinalIgnoreCase);

                    bool success;
                    if (isDodo)
                    {
                        var priceList = PayloadMapper.MapArPriceList(data, now);
                        success = await _sapService.SendPriceList(priceList, ct);
                    }
                    else
                    {
                        var payload = PayloadMapper.Map(data, type, now);
                        success = await _sapService.Send(payload, type, ct);
                    }

                    if (!success)
                    {
                        _logger.LogWarning("Send failed for group {GroupId}", g.group_id);
                        continue;
                    }

                    await _transactionService.MarkAsSent(g.id, ct);
                    _logger.LogInformation("Processed group {GroupId} successfully", g.group_id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Process error group {GroupId}", g.group_id);
                    continue;
                }
            }
        }
    }
}
