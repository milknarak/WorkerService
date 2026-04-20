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

        public async Task<TransactionAggregate> GetTransaction(string groupId, string type)
        {
            if (type == "AP")
            {
                var apTask = _pb.GetApTransaction(groupId);
                var subTask = _pb.GetApSubTransaction(groupId);
                var taxTask = _pb.GetApTransactionPurcTax(groupId);
                var accTask = _pb.GetApTransactionAcc(groupId);

                await Task.WhenAll(apTask, subTask, taxTask, accTask);

                return new TransactionAggregate
                {
                    ApTransaction = apTask.Result,
                    ApSubTransaction = subTask.Result,
                    ApTransactionPurcTax = taxTask.Result,
                    ApTransactionAcc = accTask.Result
                };
            }

            if (type == "AR")
            {
                var arTask = _pb.GetArTransaction(groupId);
                var subTask = _pb.GetArSubTransaction(groupId);
                var accTask = _pb.GetArTransactionAcc(groupId);

                await Task.WhenAll(arTask, subTask, accTask);

                return new TransactionAggregate
                {
                    ArTransaction = arTask.Result,
                    ArSubTransaction = subTask.Result,
                    ArTransactionAcc = accTask.Result
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
