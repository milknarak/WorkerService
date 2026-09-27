namespace Worker.Models
{
    public class ApSubTransactionRecord
    {
        public string id { get; set; }
        public string group_id { get; set; }
        // ผูกกับ ap_transactions.id — 1 group มีหลาย ap_transaction แล้ว sub ต้องรู้ว่าเป็นของตัวไหน
        public string transaction_id { get; set; }
        public int seq { get; set; }
        public string sub_group_type { get; set; }
        public decimal? curr_amt { get; set; }
        public decimal? local_amt { get; set; }
        public string remark { get; set; }
    }
}
