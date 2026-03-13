using System.Text.Json.Serialization;
using Worker.Converters;

namespace Worker.Models
{
    public class ApTransactionPurcTax
    {
        public string group_id { get; set; }
        public int purc_seq { get; set; }
        public int seq { get; set; }
        public string tax_id { get; set; }
        public string branch_yn { get; set; }
        public string branch_code { get; set; }
        public string branch_name { get; set; }
        public string tax_status { get; set; }
        public string purc_type { get; set; }
        public string purc_code { get; set; }
        public string purc_tax_no { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? purc_tax_date { get; set; }
        public string payment_name { get; set; }
        public decimal gs_non_vat_amt { get; set; }
        public decimal gs_amt { get; set; }
        public decimal vat_rate { get; set; }
        public decimal vat_amt { get; set; }
        public decimal total_amt { get; set; }
        public string remark { get; set; }
    }
}
