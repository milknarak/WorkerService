using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Worker.Aggregates;
using Worker.Models;

namespace Worker.Services
{
    public class TransactionService
    {
        private readonly PocketbaseService _pb;

        public string Name => _pb.Name;

        public TransactionService(PocketbaseService pb)
        {
            _pb = pb;
        }

        public async Task<List<TransactionGroup>> GetPendingGroups(CancellationToken ct = default)
        {
            return await _pb.GetPendingGroups(ct);
        }

        // ── AP: 1 group → หลาย ap_transaction (แต่ละตัวยิง ERP แยก + track สถานะรายตัว) ──
        public Task<List<ApTransactionRecord>> GetApTransactions(string groupId, CancellationToken ct = default)
        {
            return _pb.GetApTransactions(groupId, ct);
        }

        // รวม sub (ผูกด้วย transaction_id) + vendor ของ ap_transaction ตัวเดียว → aggregate ให้ PayloadMapper
        public async Task<TransactionAggregate> GetApAggregate(ApTransactionRecord ap, CancellationToken ct = default)
        {
            var subs = await _pb.GetApSubTransaction(ap.id, ct);

            var customer = !string.IsNullOrWhiteSpace(ap.vendor_code)
                ? await _pb.GetCustomer(ap.vendor_code, ct)
                : null;

            return new TransactionAggregate
            {
                ApTransaction = ap,
                ApSubTransaction = subs,
                Customer = customer
            };
        }

        public Task MarkApSent(string apId, CancellationToken ct = default) => _pb.UpdateApSentDate(apId, ct);
        public Task MarkApSkipped(string apId, string? message, CancellationToken ct = default) => _pb.MarkApSkipped(apId, message, ct);
        public Task RecordApFailure(string apId, int retryTime, string? message, CancellationToken ct = default) => _pb.UpdateApFailure(apId, retryTime, message, ct);
        public Task StampApMessage(string apId, string? message, CancellationToken ct = default) => _pb.StampApMessage(apId, message, ct);

        // ── ap_debit_note: รันเลข ref_inv_no เอง (IMIFYY/xxxxx) จาก ap_parameter ──
        // ERP ไม่ส่งเลขอ้างอิงมาให้ debit note → เราออกเลขเอง, รีเซ็ตตัวนับเมื่อขึ้นปีใหม่
        // record parameter_code='ap_debit_note_running_no', เก็บเลขล่าสุดแบบเต็มไว้ใน description
        private const string DebitNoteRunningParam = "ap_debit_note_running_no";
        private const string DebitNotePrefix = "IMIF";
        // ^IMIF<YY>/<running>$ — จับปี 2 หลัก + ตัวนับ เพื่อเทียบปีและ +1
        private static readonly Regex DebitNoteRefRegex =
            new(@"^IMIF(\d{2})/(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // คืน ref_inv_no ของ ap_debit_note — ถ้ามีอยู่แล้ว (รอบ retry) ใช้ซ้ำ ไม่กินเลขใหม่
        // ถ้ายังไม่มี: จองเลขถัดไปจาก ap_parameter แล้ว persist กลับลง ap_transactions ให้ idempotent
        public async Task<string> EnsureDebitNoteRefInvNo(ApTransactionRecord ap, DateTime now, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(ap.ref_inv_no))
                return ap.ref_inv_no;

            var param = await _pb.GetApParameter(DebitNoteRunningParam, ct)
                ?? throw new InvalidOperationException(
                    $"ap_parameter '{DebitNoteRunningParam}' not found — create the record before issuing ap_debit_note ref_inv_no");

            var refNo = NextDebitNoteRef(param.description, now.Year % 100);

            // จองเลขก่อน (เขียนลง ap_parameter) แล้วค่อย persist ลง txn
            // crash ระหว่างกลาง = เสียเลข 1 ตัว (gap) ยอมรับได้ แต่ห้ามออกเลขซ้ำ
            await _pb.UpdateApParameterValue(param.id, refNo, ct);
            await _pb.UpdateApRefInvNo(ap.id, refNo, ct);
            ap.ref_inv_no = refNo;

            return refNo;
        }

        // คำนวณเลขถัดไป: ปีเดิม → +1, ขึ้นปีใหม่ (หรือยังไม่มีค่า/parse ไม่ได้) → เริ่ม 00001
        private static string NextDebitNoteRef(string? lastRef, int currentYy)
        {
            var next = 1;

            if (!string.IsNullOrWhiteSpace(lastRef))
            {
                var m = DebitNoteRefRegex.Match(lastRef.Trim());
                if (m.Success
                    && int.TryParse(m.Groups[1].Value, out var lastYy)
                    && int.TryParse(m.Groups[2].Value, out var lastNo)
                    && lastYy == currentYy)
                {
                    next = lastNo + 1;
                }
            }

            return $"{DebitNotePrefix}{currentYy:00}/{next:00000}";
        }

        // ── AR: ยังเป็น 1 group → 1 ar_transaction (คงเดิม) ──
        public async Task<TransactionAggregate?> GetTransaction(string groupId, TransactionType type, CancellationToken ct = default)
        {
            if (type == TransactionType.Ar)
            {
                var arTask = _pb.GetArTransaction(groupId, ct);
                var subTask = _pb.GetArSubTransaction(groupId, ct);

                await Task.WhenAll(arTask, subTask);

                if (arTask.Result == null)
                    return null;

                // AR payload (COCO/DODO) ไม่ใช้ชื่อลูกค้า → ไม่ต้องดึง customer
                return new TransactionAggregate
                {
                    ArTransaction = arTask.Result,
                    ArSubTransaction = subTask.Result
                };
            }

            // AP ใช้ GetApTransactions/GetApAggregate (1 group → หลาย txn) ไม่ผ่านเมธอดนี้
            throw new ArgumentOutOfRangeException(nameof(type), $"GetTransaction handles AR only; got {type}");
        }

        public async Task MarkAsSent(string id, CancellationToken ct = default)
        {
            await _pb.UpdateSentDate(id, ct);
        }

        public async Task MarkAsSkipped(string id, string? message, CancellationToken ct = default)
        {
            await _pb.MarkAsSkipped(id, message, ct);
        }

        public async Task RecordFailure(string id, int retryTime, string? message, CancellationToken ct = default)
        {
            await _pb.UpdateFailure(id, retryTime, message, ct);
        }
    }
}
