
namespace Worker.Models
{
    public class PocketResponse<T>
    {
        public int page { get; set; }
        public int perPage { get; set; }
        public int totalItems { get; set; }
        public int totalPages { get; set; }
        public List<T> items { get; set; }
    }
}
