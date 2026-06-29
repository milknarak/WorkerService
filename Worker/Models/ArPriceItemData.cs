namespace Worker.Models
{
    public class ArPriceItemData
    {
        public int seq { get; set; }
        public string item_type { get; set; }
        public string item_code { get; set; }
        public decimal? item_qty { get; set; }
        public string item_unit_code { get; set; }
        public string cr_by { get; set; }
        public string prog_id { get; set; }
    }
}
