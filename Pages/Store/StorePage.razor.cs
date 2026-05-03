// Pages/Store/StorePage.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Domain.Partner;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Partners;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Models.Firebase;  
namespace SubashaVentures.Pages.Store;

public partial class StorePage : ComponentBase
{
    [Parameter] public string Slug { get; set; } = string.Empty;

    [Inject] private IPartnerStoreService        StoreService      { get; set; } = default!;
    [Inject] private IPartnerStoreReviewService  ReviewService     { get; set; } = default!;
    [Inject] private IProductService             ProductService    { get; set; } = default!;
    [Inject] private IPermissionService          PermissionService { get; set; } = default!;
    [Inject] private IVisualElementsService      VisualElements    { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private NavigationManager           Navigation        { get; set; } = default!;

    // ── Data ───────────────────────────────────────────────────
    private PartnerStoreViewModel?            store         = null;
    private PartnerStoreRatingSummary         ratingSummary = PartnerStoreRatingSummary.Empty(string.Empty);
    private List<PartnerStoreReviewViewModel> reviews       = new();
    private List<ProductViewModel>            products      = new();

    // ── SVGs ───────────────────────────────────────────────────
    private string storeIconSvg    = string.Empty; // not-found hero
    private string storeIconLgSvg  = string.Empty; // logo placeholder in banner
    private string locationIconSvg = string.Empty;
    private string starIconSvg     = string.Empty;
    private string phoneIconSvg    = string.Empty;
    private string mailIconSvg     = string.Empty;
    private string editIconSvg     = string.Empty;
    private string checkIconSvg    = string.Empty;
    private string warningIconSvg  = string.Empty;
    private string thumbsUpIconSvg = string.Empty;
    private string boxIconSvg      = string.Empty;
    private string chatIconSvg     = string.Empty;

    // ── State ──────────────────────────────────────────────────
    private bool isLoading           = true;
    private bool isAuthenticated     = false;
    private bool hasReviewed         = false;
    private bool showReviewForm      = false;
    private bool isSubmittingReview  = false;
    private bool reviewSubmitSuccess = false;

    private string  currentUserId     = string.Empty;
    private string  currentUserName   = string.Empty;
    private string? currentUserAvatar = null;
    private string  sortBy            = "default";
    private string  reviewSubmitError = string.Empty;

    // ── Review form ────────────────────────────────────────────
    private StoreReviewFormData        reviewForm   = new();
    private Dictionary<string, string> reviewErrors = new();

    // ── Computed ───────────────────────────────────────────────
    private List<ProductViewModel> sortedProducts => sortBy switch
    {
        "price-asc"  => products.OrderBy(p => p.Price).ToList(),
        "price-desc" => products.OrderByDescending(p => p.Price).ToList(),
        "rating"     => products.OrderByDescending(p => p.Rating).ToList(),
        "newest"     => products.OrderByDescending(p => p.CreatedAt).ToList(),
        _            => products.OrderByDescending(p => p.IsFeatured)
                                .ThenByDescending(p => p.Rating)
                                .ToList()
    };

    // ── Lifecycle ──────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(LoadUserContext(), LoadIconsAsync());
        await LoadStore();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!isLoading &&
            store != null &&
            !store.StoreSlug.Equals(Slug, StringComparison.OrdinalIgnoreCase))
        {
            await LoadStore();
        }
    }

    private async Task LoadIconsAsync()
    {
        try
        {
            storeIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.ShopNow, width: 64, height: 64, fillColor: "var(--text-muted)");

            storeIconLgSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.ShopNow, width: 36, height: 36, fillColor: "rgba(255,255,255,0.7)");

            starIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Star, width: 16, height: 16, fillColor: "#f59e0b");

            thumbsUpIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.ThumbsUp, width: 14, height: 14, fillColor: "currentColor");

            mailIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Mail, width: 14, height: 14, fillColor: "currentColor");

            warningIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Warning, width: 14, height: 14, fillColor: "currentColor");

            boxIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.AllProducts, width: 40, height: 40, fillColor: "var(--text-muted)");

            chatIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Messages, width: 40, height: 40, fillColor: "var(--text-muted)");

            // Inline-generated icons (no matching SvgType entry)
            locationIconSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' " +
                "stroke-linejoin='round' " +
                "d='M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7z'/>" +
                "<circle cx='12' cy='9' r='2.5' stroke='currentColor' stroke-width='1.5'/>",
                14, 14, "0 0 24 24", "fill='none'");

            phoneIconSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' " +
                "stroke-linejoin='round' " +
                "d='M22 16.92v3a2 2 0 01-2.18 2 19.79 19.79 0 01-8.63-3.07A19.5 19.5 0 013.07 " +
                "10.8 19.79 19.79 0 01.01 2.22 2 2 0 012 .04h3a2 2 0 012 1.72 12.84 12.84 0 " +
                "00.7 2.81 2 2 0 01-.45 2.11L6.09 7.91a16 16 0 006 6l1.27-1.27a2 2 0 012.11-.45 " +
                "12.84 12.84 0 002.81.7A2 2 0 0122 14.92z'/>",
                14, 14, "0 0 24 24", "fill='none'");

            editIconSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' " +
                "stroke-linejoin='round' " +
                "d='M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7'/>" +
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' " +
                "stroke-linejoin='round' " +
                "d='M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z'/>",
                16, 16, "0 0 24 24", "fill='none'");

            checkIconSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='2' stroke-linecap='round' " +
                "stroke-linejoin='round' d='M20 6L9 17l-5-5'/>",
                14, 14, "0 0 24 24", "fill='none'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StorePage icon load error: {ex.Message}");
        }
    }

    private async Task LoadUserContext()
    {
        try
        {
            isAuthenticated = await PermissionService.IsAuthenticatedAsync();
            if (!isAuthenticated) return;

            currentUserId = await PermissionService.GetCurrentUserIdAsync() ?? string.Empty;

            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user      = authState.User;

            var firstName = user.FindFirst("first_name")?.Value ?? string.Empty;
            var lastName  = user.FindFirst("last_name")?.Value  ?? string.Empty;
            currentUserName = $"{firstName} {lastName}".Trim();
            if (string.IsNullOrEmpty(currentUserName))
                currentUserName = user.Identity?.Name ?? "User";

            currentUserAvatar = user.FindFirst("avatar_url")?.Value;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StorePage user context error: {ex.Message}");
        }
    }

    private async Task LoadStore()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            store = await StoreService.GetStoreBySlugAsync(Slug);

            if (store == null)
            {
                isLoading = false;
                StateHasChanged();
                return;
            }

            var reviewsTask  = ReviewService.GetStoreReviewsAsync(store.Id);
            var summaryTask  = ReviewService.GetStoreRatingSummaryAsync(store.Id);
            var productsTask = ProductService.GetProductsByPartnerAsync(
                Guid.Parse(store.PartnerId));

            await Task.WhenAll(reviewsTask, summaryTask, productsTask);

            reviews       = await reviewsTask;
            ratingSummary = await summaryTask;
            products      = (await productsTask)
                .Where(p => p.IsActive && p.IsInStock)
                .ToList();

            if (isAuthenticated && !string.IsNullOrEmpty(currentUserId))
            {
                hasReviewed = await ReviewService.HasUserReviewedStoreAsync(
                    store.Id, currentUserId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StorePage load error: {ex.Message}");
            store = null;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    // ── Review form ────────────────────────────────────────────

    private void OpenReviewForm()
    {
        reviewForm          = new StoreReviewFormData();
        reviewErrors.Clear();
        reviewSubmitError   = string.Empty;
        reviewSubmitSuccess = false;
        showReviewForm      = true;
        StateHasChanged();
    }

    private void CloseReviewForm()
    {
        showReviewForm = false;
        reviewErrors.Clear();
        StateHasChanged();
    }

    private async Task HandleSubmitReview()
    {
        reviewErrors.Clear();
        reviewSubmitError = string.Empty;

        if (reviewForm.Rating == 0)
            reviewErrors["Rating"] = "Please select a star rating";

        if (string.IsNullOrWhiteSpace(reviewForm.Comment) ||
            reviewForm.Comment.Trim().Length < 10)
            reviewErrors["Comment"] = "Comment must be at least 10 characters";

        if (reviewErrors.Any()) { StateHasChanged(); return; }
        if (store == null) return;

        isSubmittingReview = true;
        StateHasChanged();

        try
        {
            var request = new SubmitStoreReviewRequest
            {
                StoreId   = store.Id,
                PartnerId = store.PartnerId,
                Rating    = reviewForm.Rating,
                Title     = string.IsNullOrWhiteSpace(reviewForm.Title)
                    ? null : reviewForm.Title.Trim(),
                Comment   = reviewForm.Comment.Trim()
            };

            var result = await ReviewService.SubmitReviewAsync(
                currentUserId, currentUserName, currentUserAvatar, request);

            if (result.Success)
            {
                reviewSubmitSuccess = true;
                hasReviewed         = true;
                ratingSummary = await ReviewService.GetStoreRatingSummaryAsync(store.Id);
                await Task.Delay(2000);
                showReviewForm = false;
            }
            else if (result.AlreadyReviewed)
            {
                hasReviewed       = true;
                reviewSubmitError = result.ErrorMessage ?? "You have already reviewed this store.";
            }
            else
            {
                reviewSubmitError = result.ErrorMessage ?? "Failed to submit review.";
            }
        }
        catch (Exception ex)
        {
            reviewSubmitError = $"Error: {ex.Message}";
        }
        finally
        {
            isSubmittingReview = false;
            StateHasChanged();
        }
    }

    private async Task HandleMarkHelpful(string reviewId)
    {
        await ReviewService.MarkHelpfulAsync(reviewId);

        var review = reviews.FirstOrDefault(r => r.Id == reviewId);
        if (review == null) return;

        var idx = reviews.IndexOf(review);
        reviews[idx] = new PartnerStoreReviewViewModel
        {
            Id                 = review.Id,
            StoreId            = review.StoreId,
            PartnerId          = review.PartnerId,
            UserId             = review.UserId,
            UserName           = review.UserName,
            UserAvatar         = review.UserAvatar,
            Rating             = review.Rating,
            Title              = review.Title,
            Comment            = review.Comment,
            IsVerifiedPurchase = review.IsVerifiedPurchase,
            HelpfulCount       = review.HelpfulCount + 1,
            IsApproved         = review.IsApproved,
            CreatedAt          = review.CreatedAt,
            UpdatedAt          = review.UpdatedAt
        };
        StateHasChanged();
    }

    private void HandleSortChange(ChangeEventArgs e)
    {
        sortBy = e.Value?.ToString() ?? "default";
        StateHasChanged();
    }

    // ── Navigation ─────────────────────────────────────────────

    private void GoToShop()   => Navigation.NavigateTo("/shop");
    private void GoToSignIn() => Navigation.NavigateTo($"/signin?returnUrl=/store/{Slug}");

    // ── Helpers ────────────────────────────────────────────────

    private bool   HasReviewError(string field) => reviewErrors.ContainsKey(field);
    private string GetReviewError(string field) =>
        reviewErrors.GetValueOrDefault(field, string.Empty);

    public class StoreReviewFormData
    {
        public int     Rating  { get; set; } = 0;
        public string? Title   { get; set; }
        public string  Comment { get; set; } = string.Empty;
    }
}
