// Pages/User/Reviews.razor.cs
using Microsoft.AspNetCore.Components;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.User;

public partial class Reviews : ComponentBase
{
    [Inject] private IReviewService ReviewService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<Reviews> Logger { get; set; } = default!;

    private DynamicModal ReviewModal { get; set; } = default!;
    private ConfirmationPopup DeleteConfirmation { get; set; } = default!;

    private List<ReviewModel> ReviewsList = new();
    private Dictionary<string, string> ProductNames = new();
    private Dictionary<string, string> ProductImages = new();
    
    private ReviewModel? EditingReview;
    private string ValidationError = string.Empty;
    private bool IsLoading = true;
    private bool IsAuthenticated = false;
    private bool IsModalOpen = false;
    private string? ReviewToDelete;

    private double AverageRating => ReviewsList.Any() 
        ? ReviewsList.Average(r => r.Rating) 
        : 0;
    
    private int HelpfulCount => ReviewsList.Sum(r => r.HelpfulCount);
    
    private int VerifiedCount => ReviewsList.Count(r => r.IsVerifiedPurchase);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "🔄 Initializing Reviews page",
                LogLevel.Info
            );

            // Check authentication first
            IsAuthenticated = await PermissionService.IsAuthenticatedAsync();
            
            if (!IsAuthenticated)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "⚠️ User not authenticated",
                    LogLevel.Warning
                );
                IsLoading = false;
                return;
            }

            await LoadReviews();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing Reviews page");
            Logger.LogError(ex, "Failed to initialize Reviews page");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadReviews()
    {
        try
        {
            IsLoading = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "📥 Loading user reviews",
                LogLevel.Info
            );

            // Get current user ID
            var userId = await PermissionService.GetCurrentUserIdAsync();
            
            if (string.IsNullOrEmpty(userId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ Could not get user ID",
                    LogLevel.Error
                );
                return;
            }

            // Get user's reviews
            ReviewsList = await ReviewService.GetUserReviewsAsync(userId);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Loaded {ReviewsList.Count} reviews",
                LogLevel.Info
            );

            // Load product information for each review
            await LoadProductInformation();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading reviews");
            Logger.LogError(ex, "Failed to load reviews");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadProductInformation()
    {
        try
        {
            var uniqueProductIds = ReviewsList
                .Select(r => r.ProductId)
                .Distinct()
                .ToList();

            await MID_HelperFunctions.DebugMessageAsync(
                $"📦 Loading info for {uniqueProductIds.Count} unique products",
                LogLevel.Info
            );

            foreach (var productId in uniqueProductIds)
            {
                if (int.TryParse(productId, out var id))
                {
                    var product = await ProductService.GetProductByIdAsync(id);
                    
                    if (product != null)
                    {
                        ProductNames[productId] = product.Name;
                        ProductImages[productId] = product.Images?.FirstOrDefault() 
                            ?? "/images/placeholder.jpg";
                    }
                    else
                    {
                        ProductNames[productId] = $"Product #{productId}";
                        ProductImages[productId] = "/images/placeholder.jpg";
                    }
                }
                else
                {
                    ProductNames[productId] = $"Product #{productId}";
                    ProductImages[productId] = "/images/placeholder.jpg";
                }
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "✅ Product information loaded",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading product information");
            Logger.LogError(ex, "Failed to load product information");
        }
    }

    private void EditReview(ReviewModel review)
    {
        try
        {
            EditingReview = new ReviewModel
            {
                Id = review.Id,
                ProductId = review.ProductId,
                UserId = review.UserId,
                UserName = review.UserName,
                UserAvatar = review.UserAvatar,
                Rating = review.Rating,
                Title = review.Title,
                Comment = review.Comment,
                Images = new List<string>(review.Images),
                IsVerifiedPurchase = review.IsVerifiedPurchase,
                HelpfulCount = review.HelpfulCount,
                IsApproved = review.IsApproved,
                CreatedAt = review.CreatedAt
            };

            ValidationError = string.Empty;
            IsModalOpen = true;
            StateHasChanged();

            _ = MID_HelperFunctions.DebugMessageAsync(
                $"✏️ Editing review: {review.Id}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start editing review");
        }
    }

    private void SetRating(int rating)
    {
        if (EditingReview != null)
        {
            EditingReview = EditingReview with { Rating = rating };
            StateHasChanged();
        }
    }

    private async Task SaveReview()
    {
        try
        {
            if (EditingReview == null)
            {
                ValidationError = "No review to save";
                return;
            }

            // Validate
            if (EditingReview.Rating < 1 || EditingReview.Rating > 5)
            {
                ValidationError = "Please select a rating (1-5 stars)";
                StateHasChanged();
                return;
            }

            if (string.IsNullOrWhiteSpace(EditingReview.Comment) || 
                EditingReview.Comment.Length < 10)
            {
                ValidationError = "Review must be at least 10 characters";
                StateHasChanged();
                return;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"💾 Saving review: {EditingReview.Id}",
                LogLevel.Info
            );

            var updateRequest = new UpdateReviewRequest
            {
                Rating = EditingReview.Rating,
                Title = EditingReview.Title,
                Comment = EditingReview.Comment,
                ImageUrls = EditingReview.Images
            };

            var success = await ReviewService.UpdateReviewAsync(EditingReview.Id, updateRequest);

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Review saved successfully",
                    LogLevel.Info
                );

                // Update the review in the list
                var existingReview = ReviewsList.FirstOrDefault(r => r.Id == EditingReview.Id);
                if (existingReview != null)
                {
                    var index = ReviewsList.IndexOf(existingReview);
                    ReviewsList[index] = EditingReview;
                }

                CloseReviewModal();
            }
            else
            {
                ValidationError = "Failed to save review. Please try again.";
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ Failed to save review",
                    LogLevel.Error
                );
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving review");
            Logger.LogError(ex, "Failed to save review");
            ValidationError = "An error occurred while saving. Please try again.";
            StateHasChanged();
        }
    }

    private void CloseReviewModal()
    {
        IsModalOpen = false;
        EditingReview = null;
        ValidationError = string.Empty;
        StateHasChanged();
    }

    private void ConfirmDelete(string reviewId)
    {
        ReviewToDelete = reviewId;
        DeleteConfirmation.Open();
    }

    private async Task DeleteReview()
    {
        try
        {
            if (string.IsNullOrEmpty(ReviewToDelete))
                return;

            await MID_HelperFunctions.DebugMessageAsync(
                $"🗑️ Deleting review: {ReviewToDelete}",
                LogLevel.Warning
            );

            var success = await ReviewService.DeleteReviewAsync(ReviewToDelete);

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Review deleted successfully",
                    LogLevel.Info
                );

                ReviewsList.RemoveAll(r => r.Id == ReviewToDelete);
                ReviewToDelete = null;
                StateHasChanged();
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ Failed to delete review",
                    LogLevel.Error
                );
                Logger.LogError("Failed to delete review: {ReviewId}", ReviewToDelete);
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting review");
            Logger.LogError(ex, "Failed to delete review");
        }
    }

    private void ShowImagePreview(string imageUrl)
    {
        // TODO: Implement image preview modal
        Logger.LogInformation("Image preview clicked: {ImageUrl}", imageUrl);
    }

    private void NavigateToProduct(string productId)
    {
        Navigation.NavigateTo($"/products/{productId}");
    }

    private void NavigateToSignIn()
    {
        PermissionService.NavigateToSignIn("/user/reviews");
    }

    private void NavigateToShop()
    {
        Navigation.NavigateTo("/shop");
    }
}
