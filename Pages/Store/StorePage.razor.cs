// Pages/Store/StorePage.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Domain.Partner;
using SubashaVentures.Domain.Product;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Partners;
using SubashaVentures.Services.Products;

namespace SubashaVentures.Pages.Store;

public partial class StorePage : ComponentBase
{
    [Parameter] public string Slug { get; set; } = string.Empty;

    [Inject] private IPartnerStoreService       StoreService       { get; set; } = default!;
    [Inject] private IPartnerStoreReviewService  ReviewService      { get; set; } = default!;
    [Inject] private IProductService             ProductService     { get; set; } = default!;
    [Inject] private IPermissionService          PermissionService  { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider  { get; set; } = default!;
    [Inject] private NavigationManager           Navigation         { get; set; } = default!;

    // ── Data ───────────────────────────────────────────────────
    private PartnerStoreViewModel?         store         = null;
    private PartnerStoreRatingSummary      ratingSummary = PartnerStoreRatingSummary.Empty(string.Empty);
    private List<PartnerStoreReviewViewModel> reviews    = new();
    private List<ProductViewModel>           products   = new();

    // ── State ──────────────────────────────────────────────────
    private bool isLoading          = true;
    private bool isAuthenticated    = false;
    private bool hasReviewed        = false;
    private bool showReviewForm     = false;
    private bool isSubmittingReview = false;
    private bool reviewSubmitSuccess = false;

    private string  currentUserId    = string.Empty;
    private string  currentUserName  = string.Empty;
    private string? currentUserAvatar = null;
    private string  sortBy           = "default";
    private string  reviewSubmitError = string.Empty;

    // ── Review form ────────────────────────────────────────────
    private StoreReviewFormData          reviewForm   = new();
    private Dictionary<string, string>   reviewErrors = new();

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
        await LoadUserContext();
        await LoadStore();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Handle slug change (navigation to different store)
        if (!isLoading &&
            store != null &&
            !store.StoreSlug.Equals(Slug, StringComparison.OrdinalIgnoreCase))
        {
            await LoadStore();
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
            currentUserName   = $"{firstName} {lastName}".Trim();
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

            // Resolve by slug
            store = await StoreService.GetStoreBySlugAsync(Slug);

            if (store == null)
            {
                isLoading = false;
                StateHasChanged();
                return;
            }

            // Load everything in parallel
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

            // Check if the current user has already reviewed
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
        reviewForm         = new StoreReviewFormData();
        reviewErrors.Clear();
        reviewSubmitError  = string.Empty;
        reviewSubmitSuccess = false;
        showReviewForm     = true;
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
                currentUserId,
                currentUserName,
                currentUserAvatar,
                request);

            if (result.Success)
            {
                reviewSubmitSuccess = true;
                hasReviewed         = true;

                // Refresh summary without full reload
                ratingSummary = await ReviewService.GetStoreRatingSummaryAsync(store.Id);

                // Close form after brief pause so user sees success message
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

        // Update the count locally without a full reload
        var review = reviews.FirstOrDefault(r => r.Id == reviewId);
        if (review != null)
        {
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
    }

    private void HandleSortChange(ChangeEventArgs e)
    {
        sortBy = e.Value?.ToString() ?? "default";
        StateHasChanged();
    }

    // ── Navigation ─────────────────────────────────────────────

    private void GoToShop()    => Navigation.NavigateTo("/shop");
    private void GoToSignIn()  => Navigation.NavigateTo($"/signin?returnUrl=/store/{Slug}");

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
