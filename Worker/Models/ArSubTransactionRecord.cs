namespace Worker.Models
{
    public class ArSubTransactionRecord
    {
        public string group_id { get; set; }
        public int seq { get; set; }
        public decimal? curr_amt { get; set; }
        public string remark { get; set; }
    }
}
