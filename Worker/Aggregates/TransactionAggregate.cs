using Worker.Models;

namespace Worker.Aggregates
{
    public class TransactionAggregate
    {
        public ApTransactionRecord ApTransaction { get; set; }
        public List<ApSubTransactionRecord> ApSubTransaction { get; set; }

        public ArTransactionRecord ArTransaction { get; set; }
        public List<ArSubTransactionRecord> ArSubTransaction { get; set; }
    }
}
