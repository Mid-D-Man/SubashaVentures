// Services/Newsletter/INewsletterService.cs
namespace SubashaVentures.Services.Newsletter;

public interface INewsletterService
{
    /// <summary>Subscribe a guest email. Deduplicates automatically.</summary>
    Task<NewsletterSubscribeResult> SubscribeAsync(string email, string source = "landing_page");

    /// <summary>Unsubscribe by email.</summary>
    Task<bool> UnsubscribeAsync(string email);

    /// <summary>Check whether a guest email is actively subscribed.</summary>
    Task<bool> IsSubscribedAsync(string email);

    /// <summary>
    /// Combined send list: newsletter_subscribers (active) UNION user_data
    /// where email_notifications = true, deduplicated.
    /// </summary>
    Task<List<string>> GetCombinedRecipientEmailsAsync();

    /// <summary>Newsletter-only subscriber emails (no user_data).</summary>
    Task<List<string>> GetNewsletterOnlyEmailsAsync();

    /// <summary>
    /// Called during sign-up: deactivates the guest subscription so we
    /// rely on user_data.email_notifications instead.
    /// </summary>
    Task RemoveOnUserSignUpAsync(string email);

    Task<int> GetSubscriberCountAsync();

    Task<List<NewsletterSubscriberViewModel>> GetSubscribersAsync(int skip = 0, int take = 100);
}

public enum NewsletterSubscribeResult
{
    Success,
    AlreadySubscribed,
    Failed,
    InvalidEmail
}

public class NewsletterSubscriberViewModel
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime SubscribedAt { get; set; }
    public DateTime? UnsubscribedAt { get; set; }
    public string Source { get; set; } = string.Empty;

    public string DisplayDate => SubscribedAt.ToString("MMM dd, yyyy");
    public string StatusLabel => IsActive ? "Active" : "Unsubscribed";
}
