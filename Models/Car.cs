namespace CarApi.Models
{
    public class Car
    {
        public string Url { get; set; }
        public string Name { get; set; }
        public Dictionary<string, string> Details { get; set; }
        public string Description { get; set; }
        public List<IFormFile>? Images { get; set; } = null;
    }
}
