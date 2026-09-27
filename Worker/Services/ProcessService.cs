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

                    if (type == TransactionType.Ap)
                    {
                        // AP: 1 group มีได้หลาย ap_transaction → ยิง ERP ทีละตัว track สถานะรายตัว
                        await ProcessApGroup(transactionService, g, ct);
                    }
                    else
                    {
                        // AR: ยังเป็น 1 group → 1 ar_transaction (คงเดิม)
                        await ProcessArGroup(transactionService, g, type, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Process error group {GroupId}", g.group_id);
                    continue;
                }
            }
        }

        // AP: 1 transaction_group → หลาย ap_transaction; แต่ละตัว = 1 การยิง ERP
        // แสตมป์ sent_to_sap_at ของ group ก็ต่อเมื่อ ap_transaction ทุกตัวถูก finalize (สำเร็จ/skip) แล้ว
        // สมมติ: ap_transaction ทุกตัวของ group ถูกสร้างครบก่อน worker หยิบไปทำ (ไม่มีตัวมาเพิ่มหลัง group ถูกแสตมป์)
        private async Task ProcessApGroup(TransactionService transactionService, TransactionGroup g, CancellationToken ct)
        {
            var apTxns = await transactionService.GetApTransactions(g.group_id, ct);

            if (apTxns == null || apTxns.Count == 0)
            {
                // ไม่มี ap_transaction เลย → ยังส่งไม่ได้ ปล่อย group pending ให้ upstream เติมข้อมูล (เหมือนเดิม)
                _logger.LogWarning("[{Instance}] AP group {GroupId} has no ap_transactions — skipping", transactionService.Name, g.group_id);
                return;
            }

            var now = _timeProvider.GetLocalNow().LocalDateTime;
            var allDone = true;

            foreach (var ap in apTxns)
            {
                ct.ThrowIfCancellationRequested();

                // ตัวที่ finalize แล้ว (ยิงสำเร็จ หรือ ERP ยืนยันว่ามีแล้ว) → ข้าม ไม่ยิงซ้ำ
                if (!string.IsNullOrEmpty(ap.sent_to_sap_at) || ap.is_skipped)
                    continue;

                try
                {
                    var data = await transactionService.GetApAggregate(ap, ct);

                    // ไม่มี sub เลย → ยอดรวม 0 (curr_amt=0) โดน ERP ปัดตก (E008) → ยังไม่ยิง ปล่อยให้ upstream แก้
                    if ((data.ApSubTransaction?.Count ?? 0) == 0)
                    {
                        _logger.LogWarning(
                            "[{Instance}] ap_transaction {ApId} (group {GroupId}) has no sub-transactions — skipping (would produce curr_amt=0)",
                            transactionService.Name, ap.id, g.group_id);
                        allDone = false;
                        continue;
                    }

                    var payload = PayloadMapper.Map(data, TransactionType.Ap, now);
                    var result = await _sapService.Send(payload, TransactionType.Ap, ct);

                    if (result.Success)
                    {
                        await transactionService.MarkApSent(ap.id, ct);
                        _logger.LogInformation("[{Instance}] Sent ap_transaction {ApId} (group {GroupId})", transactionService.Name, ap.id, g.group_id);
                        continue;
                    }

                    // ERP ยืนยันว่าตัวนี้อยู่ในระบบแล้ว (duplicate) → flag skip รายตัว group อื่นยังส่งต่อได้
                    if (result.IsAlreadyInSystem)
                    {
                        await transactionService.MarkApSkipped(ap.id, result.ErrorMessage, ct);
                        _logger.LogWarning(
                            "[{Instance}] ap_transaction {ApId} already in ERP — skipping. {ErrMsg}",
                            transactionService.Name, ap.id, result.ErrorMessage);
                        continue;
                    }

                    // error อื่น → บันทึก retry รายตัว แล้วปล่อยให้รอบหน้าลองใหม่ (group ยังไม่ครบ จึงยังถูกกวาด)
                    await transactionService.RecordApFailure(ap.id, ap.retry_time + 1, result.ErrorMessage, ct);
                    _logger.LogWarning(
                        "[{Instance}] Send failed for ap_transaction {ApId} (group {GroupId}) (retry {Retry}). {ErrMsg}",
                        transactionService.Name, ap.id, g.group_id, ap.retry_time + 1, result.ErrorMessage);
                    allDone = false;
                }
                catch (Exception ex)
                {
                    // 1 ตัวพัง ไม่ให้ล้มทั้ง group — ตัวอื่นยังยิงต่อได้ รอบหน้าค่อยลองตัวนี้ใหม่
                    _logger.LogError(ex, "[{Instance}] Process error ap_transaction {ApId} (group {GroupId})", transactionService.Name, ap.id, g.group_id);
                    allDone = false;
                }
            }

            // ครบทุก ap_transaction (สำเร็จ/skip) → แสตมป์ group ให้หลุดจากการกวาด
            if (allDone)
            {
                await transactionService.MarkAsSent(g.id, ct);
                _logger.LogInformation("[{Instance}] Group {GroupId} complete — all ap_transactions finalized", transactionService.Name, g.group_id);
            }
        }

        // AR: 1 group → 1 ar_transaction (รวม DODO price list) — logic เดิมไม่เปลี่ยน
        private async Task ProcessArGroup(TransactionService transactionService, TransactionGroup g, TransactionType type, CancellationToken ct)
        {
            var data = await transactionService.GetTransaction(g.group_id, type, ct);

            if (data == null)
            {
                _logger.LogWarning("{Type} transaction not found for group {GroupId} — skipping", type, g.group_id);
                return;
            }

            // Header ที่ไม่มี sub-transaction เลย → ยอดรวมจะเป็น 0 (curr_amt=0) แล้วโดน ERP ปัดตก (E008)
            // กันไว้ตั้งแต่ต้น ไม่ต้องยิงไป ERP; ปล่อย pending ไว้ให้ upstream แก้ข้อมูล
            if ((data.ArSubTransaction?.Count ?? 0) == 0)
            {
                _logger.LogWarning(
                    "{Type} group {GroupId} has no sub-transactions — skipping (would produce curr_amt=0)",
                    type, g.group_id);
                return;
            }

            var now = _timeProvider.GetLocalNow().LocalDateTime;

            var isDodo = string.Equals(g.sub_type, "DODO", StringComparison.OrdinalIgnoreCase);

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
                return;
            }

            // ERP ยืนยันว่าข้อมูลอยู่ในระบบแล้ว (duplicate — เกิดจากลบ+สร้าง record ใหม่ใน PocketBase)
            // → flag skip ไม่วนส่งซ้ำ ไม่แตะ sent_to_sap_at
            if (result.IsAlreadyInSystem)
            {
                await transactionService.MarkAsSkipped(g.id, result.ErrorMessage, ct);
                _logger.LogWarning(
                    "[{Instance}] Group {GroupId} already in ERP — skipping. {ErrMsg}",
                    transactionService.Name, g.group_id, result.ErrorMessage);
                return;
            }

            // error อื่น → บันทึก retry_time + ข้อความ แล้วปล่อยให้ retry รอบหน้า (auto-heal เมื่อ ERP แก้ข้อมูล)
            await transactionService.RecordFailure(g.id, g.retry_time + 1, result.ErrorMessage, ct);
            _logger.LogWarning(
                "[{Instance}] Send failed for group {GroupId} (retry {Retry}). {ErrMsg}",
                transactionService.Name, g.group_id, g.retry_time + 1, result.ErrorMessage);
        }
    }
}
