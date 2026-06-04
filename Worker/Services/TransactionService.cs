using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Worker.Aggregates;
using Worker.Models;

namespace Worker.Services
{
    public class TransactionService
    {
        private readonly PocketbaseService _pb;

        public TransactionService(PocketbaseService pb)
        {
            _pb = pb;
        }

        public async Task<List<TransactionGroup>> GetPendingGroups()
        {
            return await _pb.GetPendingGroups();
        }

        public async Task<TransactionAggregate?> GetTransaction(string groupId, string type)
        {
            if (type == "AP")
            {
                var apTask = _pb.GetApTransaction(groupId);
                var subTask = _pb.GetApSubTransaction(groupId);

                await Task.WhenAll(apTask, subTask);

                if (apTask.Result == null)
                    return null;

                var customer = !string.IsNullOrWhiteSpace(apTask.Result.vendor_code)
                    ? await _pb.GetCustomer(apTask.Result.vendor_code)
                    : null;

                return new TransactionAggregate
                {
                    ApTransaction = apTask.Result,
                    ApSubTransaction = subTask.Result,
                    Customer = customer
                };
            }

            if (type == "AR")
            {
                var arTask = _pb.GetArTransaction(groupId);
                var subTask = _pb.GetArSubTransaction(groupId);

                await Task.WhenAll(arTask, subTask);

                if (arTask.Result == null)
                    return null;

                var customer = !string.IsNullOrWhiteSpace(arTask.Result.vendor_code)
                    ? await _pb.GetCustomer(arTask.Result.vendor_code)
                    : null;

                return new TransactionAggregate
                {
                    ArTransaction = arTask.Result,
                    ArSubTransaction = subTask.Result,
                    Customer = customer
                };
            }

            throw new ArgumentOutOfRangeException(nameof(type), $"Unknown type {type}");
        }

        public async Task MarkAsSent(string id)
        {
            await _pb.UpdateSentDate(id);
        }
    }
}
