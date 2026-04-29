using Worker.Models;

namespace Worker.Aggregates
{
    public class TransactionAggregate
    {
        public ApTransactionRecord ApTransaction { get; set; }
        public List<ApSubTransactionRecord> ApSubTransaction { get; set; }

        public ArTransaction ArTransaction { get; set; }
        public List<ArSubTransaction> ArSubTransaction { get; set; }
        public List<ArTransactionAcc> ArTransactionAcc { get; set; }
    }
}
