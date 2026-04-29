using System.Text.Json.Serialization;
using Worker.Converters;

namespace Worker.Models
{
    public class ApTransactionRecord
    {
        public string group_id { get; set; }
        public string vendor_code { get; set; }
        public string local_type { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? due_date { get; set; }

        public string tax_id { get; set; }
        public string ref_inv_no { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? ref_inv_date { get; set; }

        public string ref_doc_no { get; set; }
        public string ref_po_no { get; set; }
        public string ref_gr_no_by_in { get; set; }
        public string curr_code { get; set; }
        public decimal? pre_curr_amt { get; set; }
        public decimal? curr_amt { get; set; }
        public decimal? exchange_rate { get; set; }
    }
}
