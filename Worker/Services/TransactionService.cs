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
            await _pb.Authenticate();

            return await _pb.GetPendingGroups();
        }

        public async Task<TransactionAggregate> GetTransaction(string groupId, string type)
        {
            if (type == "AP")
            {
                var ap = await _pb.GetApTransaction(groupId);
                var sub = await _pb.GetApSubTransaction(groupId);
                var tax = await _pb.GetApTransactionPurcTax(groupId);
                var acc = await _pb.GetApTransactionAcc(groupId);

                return new TransactionAggregate
                {
                    ApTransaction = ap,
                    ApSubTransaction = sub,
                    ApTransactionPurcTax = tax,
                    ApTransactionAcc = acc
                };
            }

            if (type == "AR")
            {
                var ar = await _pb.GetArTransaction(groupId);
                var sub = await _pb.GetArSubTransaction(groupId);
                var acc = await _pb.GetArTransactionAcc(groupId);

                return new TransactionAggregate
                {
                    ArTransaction = ar,
                    ArSubTransaction = sub,
                    ArTransactionAcc = acc
                };
            }

            throw new Exception($"Unknown type {type}");
        }

        public async Task MarkAsSent(string id)
        {
            await _pb.UpdateSentDate(id);
        }
    }
}
