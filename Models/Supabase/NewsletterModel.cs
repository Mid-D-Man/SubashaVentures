// Models/Supabase/NewsletterModel.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

[Table("newsletter_subscribers")]
public class NewsletterModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("subscribed_at")]
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;

    [Column("unsubscribed_at")]
    public DateTime? UnsubscribedAt { get; set; }

    [Column("source")]
    public string Source { get; set; } = "landing_page";

    public bool ShouldSerializeId() => false;
    public bool ShouldSerializeSubscribedAt() => false;
}
