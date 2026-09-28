using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
