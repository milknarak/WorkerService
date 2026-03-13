using Worker.Models;

namespace Worker.Aggregates
{
    public class TransactionAggregate
    {
        public ApTransaction ApTransaction { get; set; }
        public List<ApSubTransaction> ApSubTransaction { get; set; }
        public List<ApTransactionPurcTax> ApTransactionPurcTax { get; set; }
        public List<ApTransactionAcc> ApTransactionAcc { get; set; }

        public ArTransaction ArTransaction { get; set; }
        public List<ArSubTransaction> ArSubTransaction { get; set; }
        public List<ArTransactionAcc> ArTransactionAcc { get; set; }
    }
}
