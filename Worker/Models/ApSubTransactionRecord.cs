namespace Worker.Models
{
    public class ApSubTransactionRecord
    {
        public string group_id { get; set; }
        public int seq { get; set; }
        public string sub_group_type { get; set; }
        public decimal? curr_amt { get; set; }
        public decimal? local_amt { get; set; }
        public string remark { get; set; }
    }
}
