using System.Text.Json.Serialization;
using Worker.Converters;

namespace Worker.Models
{
    public class ArTransaction
    {
        public string ou_code { get; set; }
        public string ar_code { get; set; }
        public string vendor_code { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? tran_date { get; set; }
        public string credit_code { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? due_date { get; set; }
        public string ref_doc_no { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? ref_doc_date { get; set; }
        public string curr_code { get; set; }
        public decimal? exchange_rate { get; set; }
        public decimal? curr_amt { get; set; }
        public decimal? local_amt { get; set; }
        public decimal? vat_amt { get; set; }
        public string remark { get; set; }
        public string system_id { get; set; }
        public string branch_code { get; set; }
        public string is_manual_acc { get; set; }
        public string cr_by { get; set; }
        public string prog_id { get; set; }
        public List<ArSubTransaction> arSubTransaction { get; set; } = new();
        public List<ArTransactionAcc> arTransactionAcc { get; set; } = new();
    }
}
