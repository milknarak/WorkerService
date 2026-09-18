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
        private readonly IEnumerable<TransactionService> _transactionServices;
        private readonly SapService _sapService;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ProcessService> _logger;

        public ProcessService(
            IEnumerable<TransactionService> transactionServices,
            SapService sapService,
            TimeProvider timeProvider,
            ILogger<ProcessService> logger)
        {
            _transactionServices = transactionServices;
            _sapService = sapService;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Process(CancellationToken ct = default)
        {
            foreach (var ts in _transactionServices)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await ProcessInstance(ts, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Process error for instance {Instance}", ts.Name);
                }
            }
        }

        private async Task ProcessInstance(TransactionService transactionService, CancellationToken ct)
        {
            var groups = await transactionService.GetPendingGroups(ct);

            if(groups == null || !groups.Any())
            {
                _logger.LogInformation("[{Instance}] No pending transaction groups to process.", transactionService.Name);
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

                    var data = await transactionService.GetTransaction(g.group_id, type, ct);

                    if (data == null)
                    {
                        _logger.LogWarning("{Type} transaction not found for group {GroupId} — skipping", type, g.group_id);
                        continue;
                    }

                    // Header ที่ไม่มี sub-transaction เลย → ยอดรวมจะเป็น 0 (curr_amt=0) แล้วโดน ERP ปัดตก (E008)
                    // กันไว้ตั้งแต่ต้น ไม่ต้องยิงไป ERP; ปล่อย pending ไว้ให้ upstream แก้ข้อมูล
                    var subCount = type == TransactionType.Ap
                        ? data.ApSubTransaction?.Count ?? 0
                        : data.ArSubTransaction?.Count ?? 0;

                    if (subCount == 0)
                    {
                        _logger.LogWarning(
                            "{Type} group {GroupId} has no sub-transactions — skipping (would produce curr_amt=0)",
                            type, g.group_id);
                        continue;
                    }

                    var now = _timeProvider.GetLocalNow().LocalDateTime;

                    var isDodo = type == TransactionType.Ar &&
                                 string.Equals(g.sub_type, "DODO", StringComparison.OrdinalIgnoreCase);

                    SapSendResult result;
                    if (isDodo)
                    {
                        var priceList = PayloadMapper.MapArPriceList(data, now);
                        result = await _sapService.SendPriceList(priceList, ct);
                    }
                    else
                    {
                        var payload = PayloadMapper.Map(data, type, now);
                        result = await _sapService.Send(payload, type, ct);
                    }

                    if (result.Success)
                    {
                        await transactionService.MarkAsSent(g.id, ct);
                        _logger.LogInformation("[{Instance}] Processed group {GroupId} successfully", transactionService.Name, g.group_id);
                        continue;
                    }

                    // ERP ยืนยันว่าข้อมูลอยู่ในระบบแล้ว (duplicate — เกิดจากลบ+สร้าง record ใหม่ใน PocketBase)
                    // → flag skip ไม่วนส่งซ้ำ ไม่แตะ sent_to_sap_at
                    if (result.IsAlreadyInSystem)
                    {
                        await transactionService.MarkAsSkipped(g.id, result.ErrorMessage, ct);
                        _logger.LogWarning(
                            "[{Instance}] Group {GroupId} already in ERP — skipping. {ErrMsg}",
                            transactionService.Name, g.group_id, result.ErrorMessage);
                        continue;
                    }

                    // error อื่น → บันทึก retry_time + ข้อความ แล้วปล่อยให้ retry รอบหน้า (auto-heal เมื่อ ERP แก้ข้อมูล)
                    await transactionService.RecordFailure(g.id, g.retry_time + 1, result.ErrorMessage, ct);
                    _logger.LogWarning(
                        "[{Instance}] Send failed for group {GroupId} (retry {Retry}). {ErrMsg}",
                        transactionService.Name, g.group_id, g.retry_time + 1, result.ErrorMessage);
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
