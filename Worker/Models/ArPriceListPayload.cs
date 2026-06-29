namespace Worker.Models
{
    public class ArPriceListPayload
    {
        public ArPriceMasterData masterData { get; set; }
        public List<ArPriceItemData> itemData { get; set; } = new();
    }
}
