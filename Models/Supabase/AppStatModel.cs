// Models/Supabase/AppStatModel.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

[Table("app_stats")]
public class AppStatModel : BaseModel
{
    [PrimaryKey("key", false)]
    [Column("key")]
    public string Key { get; set; } = string.Empty;

    [Column("value")]
    public long Value { get; set; }

    [Column("label")]
    public string? Label { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
