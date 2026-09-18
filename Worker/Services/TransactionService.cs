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

        public async Task<TransactionAggregate?> GetTransaction(string groupId, TransactionType type, CancellationToken ct = default)
        {
            if (type == TransactionType.Ap)
            {
                var apTask = _pb.GetApTransaction(groupId, ct);
                var subTask = _pb.GetApSubTransaction(groupId, ct);

                await Task.WhenAll(apTask, subTask);

                if (apTask.Result == null)
                    return null;

                var customer = !string.IsNullOrWhiteSpace(apTask.Result.vendor_code)
                    ? await _pb.GetCustomer(apTask.Result.vendor_code, ct)
                    : null;

                return new TransactionAggregate
                {
                    ApTransaction = apTask.Result,
                    ApSubTransaction = subTask.Result,
                    Customer = customer
                };
            }

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

            throw new ArgumentOutOfRangeException(nameof(type), $"Unknown type {type}");
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
