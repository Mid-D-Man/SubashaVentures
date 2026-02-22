// Services/Newsletter/NewsletterService.cs
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Newsletter;

public class NewsletterService : INewsletterService
{
    private readonly Client _supabase;
    private readonly ILogger<NewsletterService> _logger;

    public NewsletterService(Client supabase, ILogger<NewsletterService> logger)
    {
        _supabase = supabase;
        _logger = logger;
    }

    // ── Subscribe ─────────────────────────────────────────────────────────────

    public async Task<NewsletterSubscribeResult> SubscribeAsync(string email, string source = "landing_page")
    {
        if (string.IsNullOrWhiteSpace(email))
            return NewsletterSubscribeResult.InvalidEmail;

        email = email.Trim().ToLowerInvariant();

        if (!IsValidEmail(email))
            return NewsletterSubscribeResult.InvalidEmail;

        try
        {
            NewsletterModel? existing = null;
            try
            {
                existing = await _supabase
                    .From<NewsletterModel>()
                    .Where(n => n.Email == email)
                    .Single();
            }
            catch { /* not found — normal */ }

            if (existing != null)
            {
                if (existing.IsActive)
                    return NewsletterSubscribeResult.AlreadySubscribed;

                // Reactivate lapsed subscriber
                existing.IsActive = true;
                existing.UnsubscribedAt = null;
                existing.Source = source;
                await existing.Update<NewsletterModel>();

                await MID_HelperFunctions.DebugMessageAsync(
                    $"Newsletter reactivated: {email}", LogLevel.Info);

                return NewsletterSubscribeResult.Success;
            }

            var subscriber = new NewsletterModel
            {
                Email = email,
                IsActive = true,
                SubscribedAt = DateTime.UtcNow,
                Source = source
            };

            await _supabase.From<NewsletterModel>().Insert(subscriber);

            await MID_HelperFunctions.DebugMessageAsync(
                $"Newsletter new subscriber: {email}", LogLevel.Info);

            return NewsletterSubscribeResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe email: {Email}", email);
            return NewsletterSubscribeResult.Failed;
        }
    }

    // ── Unsubscribe ───────────────────────────────────────────────────────────

    public async Task<bool> UnsubscribeAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        email = email.Trim().ToLowerInvariant();

        try
        {
            NewsletterModel? existing = null;
            try
            {
                existing = await _supabase
                    .From<NewsletterModel>()
                    .Where(n => n.Email == email)
                    .Single();
            }
            catch { /* not found */ }

            if (existing == null) return false;

            existing.IsActive = false;
            existing.UnsubscribedAt = DateTime.UtcNow;
            await existing.Update<NewsletterModel>();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe: {Email}", email);
            return false;
        }
    }

    // ── Lookup ────────────────────────────────────────────────────────────────

    public async Task<bool> IsSubscribedAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        email = email.Trim().ToLowerInvariant();

        try
        {
            NewsletterModel? existing = null;
            try
            {
                existing = await _supabase
                    .From<NewsletterModel>()
                    .Where(n => n.Email == email)
                    .Single();
            }
            catch { /* not found */ }

            return existing?.IsActive == true;
        }
        catch
        {
            return false;
        }
    }

    // ── Combined recipient list ───────────────────────────────────────────────

    public async Task<List<string>> GetCombinedRecipientEmailsAsync()
    {
        try
        {
            var emailSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Guest newsletter subscribers
            var newsletterResult = await _supabase
                .From<NewsletterModel>()
                .Where(n => n.IsActive)
                .Get();

            foreach (var n in newsletterResult?.Models ?? new())
            {
                if (!string.IsNullOrWhiteSpace(n.Email))
                    emailSet.Add(n.Email.ToLowerInvariant());
            }

            // 2. Registered users with email_notifications = true
            var usersResult = await _supabase
                .From<UserModel>()
                .Where(u => u.EmailNotifications)
                .Where(u => !u.IsDeleted)
                .Get();

            foreach (var u in usersResult?.Models ?? new())
            {
                if (!string.IsNullOrWhiteSpace(u.Email))
                    emailSet.Add(u.Email.ToLowerInvariant());
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Combined newsletter recipients: {emailSet.Count}", LogLevel.Info);

            return emailSet.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get combined recipient emails");
            return new List<string>();
        }
    }

    public async Task<List<string>> GetNewsletterOnlyEmailsAsync()
    {
        try
        {
            var result = await _supabase
                .From<NewsletterModel>()
                .Where(n => n.IsActive)
                .Get();

            return result?.Models?
                .Where(n => !string.IsNullOrWhiteSpace(n.Email))
                .Select(n => n.Email.ToLowerInvariant())
                .ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get newsletter-only emails");
            return new List<string>();
        }
    }

    // ── Sign-up conversion ────────────────────────────────────────────────────

    public async Task RemoveOnUserSignUpAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        email = email.Trim().ToLowerInvariant();

        try
        {
            NewsletterModel? existing = null;
            try
            {
                existing = await _supabase
                    .From<NewsletterModel>()
                    .Where(n => n.Email == email)
                    .Single();
            }
            catch { /* not found — nothing to do */ return; }

            existing.IsActive = false;
            existing.UnsubscribedAt = DateTime.UtcNow;
            existing.Source = "converted_to_user";
            await existing.Update<NewsletterModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Newsletter subscriber converted to user account: {email}", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove newsletter subscriber on signup: {Email}", email);
        }
    }

    // ── Stats / List ──────────────────────────────────────────────────────────

    public async Task<int> GetSubscriberCountAsync()
    {
        try
        {
            var result = await _supabase
                .From<NewsletterModel>()
                .Where(n => n.IsActive)
                .Get();

            return result?.Models?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<List<NewsletterSubscriberViewModel>> GetSubscribersAsync(int skip = 0, int take = 100)
    {
        try
        {
            var result = await _supabase
                .From<NewsletterModel>()
                .Order("subscribed_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Range(skip, skip + take - 1)
                .Get();

            return result?.Models?
                .Select(n => new NewsletterSubscriberViewModel
                {
                    Id = n.Id,
                    Email = n.Email,
                    IsActive = n.IsActive,
                    SubscribedAt = n.SubscribedAt,
                    UnsubscribedAt = n.UnsubscribedAt,
                    Source = n.Source
                })
                .ToList() ?? new List<NewsletterSubscriberViewModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get newsletter subscribers");
            return new List<NewsletterSubscriberViewModel>();
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
