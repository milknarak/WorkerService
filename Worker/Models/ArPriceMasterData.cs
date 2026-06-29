using System.Text.Json.Serialization;
using Worker.Converters;

namespace Worker.Models
{
    public class ArPriceMasterData
    {
        public string ou_code { get; set; }
        public string customer_code { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? order_date { get; set; }

        [JsonConverter(typeof(NullableDateTimeConverter))]
        public DateTime? delivery_date { get; set; }

        public string cr_by { get; set; }
        public string prog_id { get; set; }
    }
}
