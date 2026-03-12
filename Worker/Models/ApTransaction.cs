using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Worker.Converters;

namespace Worker.Models
{
    public class ApTransaction
    {
        public string group_id { get; set; }
        public string ou_code { get; set; }
        public string system_id { get; set; }
        public string local_type { get; set; }
        public string doc_type { get; set; }
        public string ap_code { get; set; }
        public string vendor_code { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? tran_date { get; set; }
        public string credit_code { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? due_date { get; set; }
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
        public decimal? local_amt { get; set; }
        public string remark { get; set; }
        public string is_manual_acc { get; set; }
        public string cr_by { get; set; }
        public DateTime? cr_date { get; set; }
        public string prog_id { get; set; }
        public string upd_by { get; set; }
        public DateTime? upd_date { get; set; }
        public string upd_prog_id { get; set; }
        public List<ApSubTransaction> apSubTransaction { get; set; } = new();
        public List<ApTransactionPurcTax> apTransactionPurcTax { get; set; } = new();
        public List<ApTransactionAcc> apTransactionAcc { get; set; } = new();
    }
}
