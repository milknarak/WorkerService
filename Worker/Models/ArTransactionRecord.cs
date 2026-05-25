using System.Text.Json.Serialization;
using Worker.Converters;

namespace Worker.Models
{
    public class ArTransactionRecord
    {
        public string group_id { get; set; }
        public string vendor_code { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? due_date { get; set; }

        public string ref_doc_no { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? ref_doc_date { get; set; }

        public string curr_code { get; set; }
        public decimal? curr_amt { get; set; }
        public decimal? exchange_rate { get; set; }
    }
}
