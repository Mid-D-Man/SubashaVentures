// Pages/Admin/AdminNewsletter.razor.cs
using Microsoft.AspNetCore.Components;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Domain.User;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Email;
using SubashaVentures.Services.Newsletter;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.VisualElements;
using Client = Supabase.Client;

namespace SubashaVentures.Pages.Admin;

public partial class AdminNewsletter : ComponentBase
{
    [Inject] private INewsletterService NewsletterService { get; set; } = default!;
    [Inject] private IEmailService EmailService { get; set; } = default!;
    [Inject] private IUserSegmentationService SegmentationService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private Client Supabase { get; set; } = default!;
    [Inject] private ILogger<AdminNewsletter> Logger { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    // ── Modal refs ────────────────────────────────────────────────────────────
    private DynamicModal? _composeModal;
    private DynamicModal? _subscribersModal;
    private ConfirmationPopup? _confirmSendPopup;

    // ── Stats ─────────────────────────────────────────────────────────────────
    private int _subscriberCount;
    private int _usersWithEmailCount;
    private int _totalReach;

    // ── Compose state ─────────────────────────────────────────────────────────
    private EmailType _emailType = EmailType.Newsletter;
    private string _subject = string.Empty;
    private string _body = string.Empty;
    private string _ctaText = string.Empty;
    private string _ctaUrl = string.Empty;
    private string _directEmail = string.Empty;
    private bool _isSending;
    private string _sendError = string.Empty;
    private string _sendSuccess = string.Empty;

    // ── Segmentation state ────────────────────────────────────────────────────
    private List<MembershipTier> _selectedTiers = new();
    private string _minSpent = string.Empty;
    private string _maxSpent = string.Empty;
    private string _minOrders = string.Empty;
    private string _maxOrders = string.Empty;
    private bool _filterEmailVerified;
    private bool _filterHasOrders;
    private int _segmentSize = -1;
    private bool _isCalculatingSegment;

    // ── Subscribers list ──────────────────────────────────────────────────────
    private List<NewsletterSubscriberViewModel> _subscribers = new();
    private bool _isLoadingSubscribers;

    // ── SVGs ──────────────────────────────────────────────────────────────────
    private string _mailIcon = string.Empty;
    private string _mailSmallIcon = string.Empty;
    private string _userIcon = string.Empty;
    private string _statsIcon = string.Empty;
    private string _composeIcon = string.Empty;
    private string _subscribersIcon = string.Empty;
    private string _sendIcon = string.Empty;
    private string _warningIcon = string.Empty;
    private string _checkIcon = string.Empty;
    private string _historyIcon = string.Empty;

    // ── Can send validation ───────────────────────────────────────────────────
    private bool CanSend =>
        !string.IsNullOrWhiteSpace(_subject) &&
        !string.IsNullOrWhiteSpace(_body) &&
        (_emailType != EmailType.Direct || !string.IsNullOrWhiteSpace(_directEmail));

    // ─────────────────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        var isAdmin = await PermissionService.IsSuperiorAdminAsync();
        if (!isAdmin)
        {
            Navigation.NavigateTo("admin");
            return;
        }

        await LoadIcons();
        await LoadStats();
    }

    // ── Icons ─────────────────────────────────────────────────────────────────

    private async Task LoadIcons()
    {
        try
        {
            var primary = "var(--primary-color)";
            var muted = "var(--text-secondary)";

            _mailIcon = await VisualElements.GetSvgWithColorAsync(SvgType.Mail, 32, 32, primary);
            _mailSmallIcon = await VisualElements.GetSvgWithColorAsync(SvgType.Mail, 16, 16, muted);
            _userIcon = await VisualElements.GetSvgWithColorAsync(SvgType.User, 32, 32, primary);
            _statsIcon = await VisualElements.GetSvgWithColorAsync(SvgType.Stats, 32, 32, primary);
            _composeIcon = await VisualElements.GetSvgWithColorAsync(SvgType.Mail, 18, 18, "white");
            _subscribersIcon = await VisualElements.GetSvgWithColorAsync(SvgType.User, 18, 18, primary);
            _sendIcon = await VisualElements.GetSvgWithColorAsync(SvgType.Mail, 18, 18, "white");
            _warningIcon = await VisualElements.GetSvgWithColorAsync(SvgType.Warning, 16, 16, "var(--danger-color)");
            _checkIcon = await VisualElements.GetSvgWithColorAsync(SvgType.CheckMark, 16, 16, "var(--success-color)");
            _historyIcon = await VisualElements.GetSvgWithColorAsync(SvgType.History, 48, 48, "var(--text-muted)");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load newsletter page icons");
        }
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    private async Task LoadStats()
    {
        try
        {
            _subscriberCount = await NewsletterService.GetSubscriberCountAsync();

            var users = await Supabase
                .From<SubashaVentures.Models.Supabase.UserModel>()
                .Where(u => u.EmailNotifications && !u.IsDeleted)
                .Get();

            _usersWithEmailCount = users?.Models?.Count ?? 0;

            var combined = await NewsletterService.GetCombinedRecipientEmailsAsync();
            _totalReach = combined.Count;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load newsletter stats");
        }

        StateHasChanged();
    }

    // ── Modal helpers ─────────────────────────────────────────────────────────

    private void OpenComposeModal()
    {
        ResetComposeForm();
        _composeModal?.Open();
    }

    private void CloseComposeModal()
    {
        _composeModal?.Close();
    }

    private async Task OpenSubscribersModal()
    {
        _subscribersModal?.Open();
        await LoadSubscribers();
    }

    private async Task LoadSubscribers()
    {
        _isLoadingSubscribers = true;
        StateHasChanged();

        try
        {
            _subscribers = await NewsletterService.GetSubscribersAsync(0, 200);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load subscribers");
        }
        finally
        {
            _isLoadingSubscribers = false;
            StateHasChanged();
        }
    }

    // ── Compose form helpers ──────────────────────────────────────────────────

    private void SetEmailType(EmailType type)
    {
        _emailType = type;
        _segmentSize = -1;
        _sendError = string.Empty;
        _sendSuccess = string.Empty;
        StateHasChanged();
    }

    private void ToggleTier(MembershipTier tier, bool add)
    {
        if (add && !_selectedTiers.Contains(tier))
            _selectedTiers.Add(tier);
        else if (!add)
            _selectedTiers.Remove(tier);
    }

    private async Task PreviewSegment()
    {
        _isCalculatingSegment = true;
        StateHasChanged();

        try
        {
            var criteria = BuildSegmentationCriteria();
            var ids = await SegmentationService.GetUserIdsByCriteriaAsync(criteria);
            _segmentSize = ids.Count;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to calculate segment size");
            _segmentSize = 0;
        }
        finally
        {
            _isCalculatingSegment = false;
            StateHasChanged();
        }
    }

    private UserSegmentationCriteria BuildSegmentationCriteria()
    {
        var c = new UserSegmentationCriteria();

        if (_selectedTiers.Any())
            c.MembershipTiers = new List<MembershipTier>(_selectedTiers);

        if (decimal.TryParse(_minSpent, out var minS) && minS > 0)
            c.MinSpent = minS;

        if (decimal.TryParse(_maxSpent, out var maxS) && maxS > 0)
            c.MaxSpent = maxS;

        if (int.TryParse(_minOrders, out var minO) && minO > 0)
            c.MinOrders = minO;

        if (int.TryParse(_maxOrders, out var maxO) && maxO > 0)
            c.MaxOrders = maxO;

        if (_filterEmailVerified)
            c.IsEmailVerified = true;

        if (_filterHasOrders)
            c.MinOrders = c.MinOrders ?? 1;

        return c;
    }

    // ── Send flow ─────────────────────────────────────────────────────────────

    private async Task SendEmail()
    {
        _sendError = string.Empty;
        _sendSuccess = string.Empty;

        if (!CanSend)
        {
            _sendError = "Please fill in the required fields.";
            return;
        }

        if (_emailType == EmailType.Direct &&
            string.IsNullOrWhiteSpace(_directEmail))
        {
            _sendError = "Please enter a recipient email address.";
            return;
        }

        _confirmSendPopup?.Open();
    }

    private async Task ExecuteSend()
    {
        _isSending = true;
        _sendError = string.Empty;
        StateHasChanged();

        try
        {
            EmailResult result;

            switch (_emailType)
            {
                case EmailType.Newsletter:
                    result = await EmailService.SendNewsletterAsync(new SendNewsletterRequest
                    {
                        Subject = _subject,
                        HtmlContent = _body,
                        CtaText = string.IsNullOrWhiteSpace(_ctaText) ? null : _ctaText,
                        CtaUrl = string.IsNullOrWhiteSpace(_ctaUrl) ? null : _ctaUrl
                    });
                    break;

                case EmailType.Segmented:
                    var criteria = BuildSegmentationCriteria();
                    var userIds = await SegmentationService.GetUserIdsByCriteriaAsync(criteria);

                    if (!userIds.Any())
                    {
                        _sendError = "No users match the selected criteria.";
                        _isSending = false;
                        StateHasChanged();
                        return;
                    }

                    // Send to each matched user via transactional
                    var users = await SegmentationService.GetUsersByMultipleCriteriaAsync(criteria);
                    var errors = 0;

                    foreach (var user in users)
                    {
                        var r = await EmailService.SendTransactionalAsync(new SendTransactionalRequest
                        {
                            To = user.Email,
                            Type = "newsletter",
                            Data = new Dictionary<string, object>
                            {
                                { "subject", _subject },
                                { "content", _body },
                                { "ctaText", _ctaText },
                                { "ctaUrl", _ctaUrl }
                            }
                        });

                        if (!r.Success) errors++;
                    }

                    result = new EmailResult
                    {
                        Success = errors == 0,
                        Sent = users.Count - errors,
                        Failed = errors
                    };
                    break;

                case EmailType.Direct:
                default:
                    result = await EmailService.SendTransactionalAsync(new SendTransactionalRequest
                    {
                        To = _directEmail.Trim(),
                        Type = "newsletter",
                        Data = new Dictionary<string, object>
                        {
                            { "subject", _subject },
                            { "content", _body },
                            { "ctaText", _ctaText },
                            { "ctaUrl", _ctaUrl }
                        }
                    });
                    break;
            }

            if (result.Success)
            {
                _sendSuccess = $"Email sent successfully to {result.Sent} recipient(s).";
                Logger.LogInformation("Newsletter sent: {Sent} success, {Failed} failed",
                    result.Sent, result.Failed);

                // Reload stats after successful send
                await LoadStats();
            }
            else
            {
                _sendError = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Failed to send email. Please try again."
                    : result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Email send failed");
            _sendError = "An unexpected error occurred. Please try again.";
        }
        finally
        {
            _isSending = false;
            StateHasChanged();
        }
    }

    // ── Subscriber management ─────────────────────────────────────────────────

    private async Task UnsubscribeGuest(string email)
    {
        var ok = await NewsletterService.UnsubscribeAsync(email);
        if (ok)
        {
            await LoadSubscribers();
            await LoadStats();
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void ResetComposeForm()
    {
        _emailType = EmailType.Newsletter;
        _subject = string.Empty;
        _body = string.Empty;
        _ctaText = string.Empty;
        _ctaUrl = string.Empty;
        _directEmail = string.Empty;
        _sendError = string.Empty;
        _sendSuccess = string.Empty;
        _segmentSize = -1;
        _selectedTiers.Clear();
        _minSpent = _maxSpent = _minOrders = _maxOrders = string.Empty;
        _filterEmailVerified = false;
        _filterHasOrders = false;
    }

    // ── Enums ─────────────────────────────────────────────────────────────────

    private enum EmailType
    {
        Newsletter,
        Segmented,
        Direct
    }
}
