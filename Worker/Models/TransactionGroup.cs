
namespace Worker.Models
{
    public class TransactionGroup
    {
        public string id { get; set; }
        public string group_id { get; set; }
        public string type { get; set; }
        public string sub_type { get; set; }
        public string? sent_to_sap_at { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
    }
}
