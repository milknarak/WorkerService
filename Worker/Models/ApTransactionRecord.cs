using System.Text.Json.Serialization;
using Worker.Converters;

namespace Worker.Models
{
    public class ApTransactionRecord
    {
        // PocketBase record id — ใช้เป็น key ผูก sub (transaction_id) และแสตมป์สถานะรายตัว
        public string id { get; set; }
        public string group_id { get; set; }
        public string vendor_code { get; set; }
        public string local_type { get; set; }

        // สถานะการส่ง ERP ราย ap_transaction (1 group มีได้หลายตัว → track แยกกันส่งซ้ำ)
        // ความหมายเหมือนชุดเดียวกันบน transaction_groups แต่ย้ายลงมาระดับ txn
        public string? sent_to_sap_at { get; set; }
        public bool is_skipped { get; set; }
        public int retry_time { get; set; }
        public string? send_failed_message { get; set; }

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
