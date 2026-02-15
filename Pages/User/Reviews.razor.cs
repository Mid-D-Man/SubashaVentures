// Pages/User/Reviews.razor.cs
using Microsoft.AspNetCore.Components;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Domain.Enums;
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
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;

    private DynamicModal ReviewModal { get; set; } = default!;
    private ConfirmationPopup DeleteConfirmation { get; set; } = default!;

    private List<ReviewModel> ReviewsList = new();
    private Dictionary<string, string> ProductNames = new();
    private Dictionary<string, string> ProductImages = new();
    
    private ReviewEditModel? EditingReview;
    private string ValidationError = string.Empty;
    private bool IsLoading = true;
    private bool IsProcessing = false;
    private bool IsAuthenticated = false;
    private bool IsModalOpen = false;
    private string? ReviewToDelete;

    private string LockSvg = string.Empty;
    private string StarSvg = string.Empty;
    private string StarFilledSvg = string.Empty;
    private string PackageSvg = string.Empty;
    private string EditSvg = string.Empty;
    private string DeleteSvg = string.Empty;
    private string CheckmarkSvg = string.Empty;

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
                "Initializing Reviews page",
                LogLevel.Info
            );

            await LoadSvgsAsync();

            IsAuthenticated = await PermissionService.IsAuthenticatedAsync();
            
            if (!IsAuthenticated)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User not authenticated",
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

    private async Task LoadSvgsAsync()
    {
        try
        {
            LockSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' d='M12 15v2m0 0v2m0-2h2m-2 0H10m8-7a9 9 0 11-18 0 9 9 0 0118 0z'/>",
                24, 24, "0 0 24 24", "fill='none'"
            );

            StarSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Star,
                width: 20,
                height: 20,
                strokeColor: "currentColor",
                className: "star-outline"
            );

            StarFilledSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Star,
                width: 20,
                height: 20,
                fillColor: "#FFC107"
            );

            PackageSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Order,
                width: 24,
                height: 24,
                fillColor: "currentColor"
            );

            EditSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' d='M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7'/><path stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' d='M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z'/>",
                16, 16, "0 0 24 24", "fill='none'"
            );

            DeleteSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' d='M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2'/>",
                16, 16, "0 0 24 24", "fill='none'"
            );

            CheckmarkSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.CheckMark,
                width: 14,
                height: 14,
                strokeColor: "currentColor"
            );

            await MID_HelperFunctions.DebugMessageAsync(
                "SVGs loaded successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading SVGs");
            Logger.LogError(ex, "Failed to load SVGs");
        }
    }

    private async Task LoadReviews()
    {
        try
        {
            IsLoading = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "Loading user reviews",
                LogLevel.Info
            );

            var userId = await PermissionService.GetCurrentUserIdAsync();
            
            if (string.IsNullOrEmpty(userId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Could not get user ID",
                    LogLevel.Error
                );
                return;
            }

            ReviewsList = await ReviewService.GetUserReviewsAsync(userId);

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {ReviewsList.Count} reviews",
                LogLevel.Info
            );

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
                $"Loading info for {uniqueProductIds.Count} unique products",
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
                "Product information loaded",
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
            EditingReview = new ReviewEditModel
            {
                Id = review.Id,
                ProductId = review.ProductId,
                Rating = review.Rating,
                Title = review.Title ?? string.Empty,
                Comment = review.Comment,
                Images = new List<string>(review.Images)
            };

            ValidationError = string.Empty;
            IsModalOpen = true;
            StateHasChanged();

            _ = MID_HelperFunctions.DebugMessageAsync(
                $"Editing review: {review.Id}",
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
            EditingReview.Rating = rating;
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

            IsProcessing = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Saving review: {EditingReview.Id}",
                LogLevel.Info
            );

            var updateRequest = new UpdateReviewRequest
            {
                Rating = EditingReview.Rating,
                Title = string.IsNullOrWhiteSpace(EditingReview.Title) ? null : EditingReview.Title,
                Comment = EditingReview.Comment,
                ImageUrls = EditingReview.Images
            };

            var success = await ReviewService.UpdateReviewAsync(EditingReview.Id, updateRequest);

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Review saved successfully",
                    LogLevel.Info
                );

                await LoadReviews();
                CloseReviewModal();
            }
            else
            {
                ValidationError = "Failed to save review. Please try again.";
                await MID_HelperFunctions.DebugMessageAsync(
                    "Failed to save review",
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
        finally
        {
            IsProcessing = false;
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
            {
                DeleteConfirmation.Close();
                return;
            }

            IsProcessing = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Deleting review: {ReviewToDelete}",
                LogLevel.Warning
            );

            var success = await ReviewService.DeleteReviewAsync(ReviewToDelete);

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Review deleted successfully",
                    LogLevel.Info
                );

                ReviewsList.RemoveAll(r => r.Id == ReviewToDelete);
                ReviewToDelete = null;
                
                DeleteConfirmation.Close();
                StateHasChanged();
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Failed to delete review",
                    LogLevel.Error
                );
                Logger.LogError("Failed to delete review: {ReviewId}", ReviewToDelete);
                
                DeleteConfirmation.Close();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting review");
            Logger.LogError(ex, "Failed to delete review");
            
            DeleteConfirmation.Close();
        }
        finally
        {
            IsProcessing = false;
            StateHasChanged();
        }
    }

    private void ShowImagePreview(string imageUrl)
    {
        Logger.LogInformation("Image preview clicked: {ImageUrl}", imageUrl);
    }

    private void NavigateToProduct(string productId)
    {
        Navigation.NavigateTo($"products/{productId}");
    }

    private void NavigateToSignIn()
    {
        PermissionService.NavigateToSignIn("user/reviews");
    }

    private void NavigateToShop()
    {
        Navigation.NavigateTo("shop");
    }

    private class ReviewEditModel
    {
        public string Id { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public List<string> Images { get; set; } = new();
    }
}
