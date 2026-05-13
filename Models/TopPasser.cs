namespace AlumniTrackingAPI.Models
{
    public class TopPasser
    {
        public string Name { get; set; } = string.Empty;
        public int? Rank { get; set; }
        public string BatchYear { get; set; } = string.Empty;
        public string AwardText { get; set; } = string.Empty;
    }
}