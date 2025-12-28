namespace SubashaVentures.Domain.Product;

public class ReviewViewModel
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string Comment { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public bool IsVerifiedPurchase { get; set; }
    public int HelpfulCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    public string DisplayRating => $"{Rating}/5";
    public string TimeAgo => GetTimeAgo(CreatedAt);
    
    private string GetTimeAgo(DateTime date)
    {
        var span = DateTime.UtcNow - date;
        if (span.TotalDays > 365) return $"{(int)(span.TotalDays / 365)} year(s) ago";
        if (span.TotalDays > 30) return $"{(int)(span.TotalDays / 30)} month(s) ago";
        if (span.TotalDays > 1) return $"{(int)span.TotalDays} day(s) ago";
        if (span.TotalHours > 1) return $"{(int)span.TotalHours} hour(s) ago";
        if (span.TotalMinutes > 1) return $"{(int)span.TotalMinutes} minute(s) ago";
        return "Just now";
    }
    
    
}
