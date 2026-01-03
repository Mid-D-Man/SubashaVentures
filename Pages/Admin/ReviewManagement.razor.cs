// Pages/Admin/ReviewManagement.razor.cs
using Microsoft.AspNetCore.Components;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Orders;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Utilities.HelperScripts;
using Blazored.Toast.Services;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class ReviewManagement : ComponentBase
{
    // State
    private bool isLoading = true;
    private bool isProcessing = false;
    private bool hasPermission = false;
    
    private string currentTab = "all";
    private string searchQuery = "";
    private string rejectionReason = "";
    
    // Data
    private List<ReviewAdminDto> allReviews = new();
    private List<ReviewAdminDto> filteredReviews = new();
    private HashSet<string> selectedReviews = new();
    private Dictionary<string, string> productNameCache = new();
    
    private ReviewStatusCounts statusCounts = new();
    private int verifiedCount = 0;
    
    // Modal state
    private ReviewModel? selectedReviewForAction = null;
    private DynamicModal? approveModal;
    private DynamicModal? rejectModal;
    private DynamicModal? detailsModal;
    private ConfirmationPopup? deleteConfirmation;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "🎯 Initializing Review Management page",
                LogLevel.Info
            );

            // Check permissions
            hasPermission = await PermissionService.IsSuperiorAdminAsync();

            if (!hasPermission)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ Access denied - user is not superior admin",
                    LogLevel.Warning
                );
                ToastService.ShowError("Access denied. Admin permissions required.");
                return;
            }

            await LoadReviews();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing Review Management");
            ToastService.ShowError("Failed to initialize page. Please refresh.");
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadReviews()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "📥 Loading all reviews...",
                LogLevel.Info
            );

            // Load all reviews
            allReviews = await ReviewService.GetAllReviewsAdminAsync();

            // Load status counts
            statusCounts = await ReviewService.GetReviewStatusCountsAsync();

            // Count verified purchases
            verifiedCount = allReviews.Count(r => r.IsVerifiedPurchase);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Loaded {allReviews.Count} reviews",
                LogLevel.Info
            );

            // Apply current filter
            ApplyFilters();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading reviews");
            ToastService.ShowError("Failed to load reviews.");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void ApplyFilters()
    {
        try
        {
            // Start with all reviews
            var filtered = allReviews.AsEnumerable();

            // Apply tab filter
            filtered = currentTab switch
            {
                "pending" => filtered.Where(r => !r.IsApproved),
                "approved" => filtered.Where(r => r.IsApproved),
                _ => filtered // "all"
            };

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var search = searchQuery.ToLower();
                filtered = filtered.Where(r =>
                    r.ProductName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    r.UserName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    r.CommentPreview.Contains(search, StringComparison.OrdinalIgnoreCase)
                );
            }

            filteredReviews = filtered
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            MID_HelperFunctions.DebugMessage(
                $"📊 Filtered to {filteredReviews.Count} reviews",
                LogLevel.Debug
            );
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.LogException(ex, "Applying filters");
        }
    }

    private void SwitchTab(string tab)
    {
        currentTab = tab;
        selectedReviews.Clear();
        ApplyFilters();
        StateHasChanged();
    }

    private void HandleSearch()
    {
        ApplyFilters();
        StateHasChanged();
    }

    private void ClearSearch()
    {
        searchQuery = "";
        ApplyFilters();
        StateHasChanged();
    }

    private async Task RefreshReviews()
    {
        selectedReviews.Clear();
        await LoadReviews();
        ToastService.ShowInfo("Reviews refreshed");
    }

    private void ToggleReviewSelection(string reviewId)
    {
        if (selectedReviews.Contains(reviewId))
        {
            selectedReviews.Remove(reviewId);
        }
        else
        {
            selectedReviews.Add(reviewId);
        }
        StateHasChanged();
    }

    // ==================== APPROVE ACTIONS ====================

    private async Task ShowApproveDialog(ReviewAdminDto review)
    {
        try
        {
            // Get full review details
            selectedReviewForAction = await ReviewService.GetReviewByIdAsync(review.Id);
            
            if (selectedReviewForAction == null)
            {
                ToastService.ShowError("Review not found");
                return;
            }

            approveModal?.Open();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Showing approve dialog");
            ToastService.ShowError("Failed to load review details");
        }
    }

    private async Task ConfirmApproveReview()
    {
        if (selectedReviewForAction == null) return;

        try
        {
            isProcessing = true;
            StateHasChanged();

            var userId = await PermissionService.GetCurrentUserIdAsync();
            var userEmail = await PermissionService.GetCurrentUserEmailAsync();
            var approvedBy = userEmail ?? userId ?? "admin";

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Approving review: {selectedReviewForAction.Id}",
                LogLevel.Info
            );

            var success = await ReviewService.ApproveReviewAsync(
                selectedReviewForAction.Id,
                approvedBy
            );

            if (success)
            {
                ToastService.ShowSuccess("Review approved successfully");
                await LoadReviews();
                CloseApproveModal();
            }
            else
            {
                ToastService.ShowError("Failed to approve review");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Approving review");
            ToastService.ShowError("An error occurred while approving the review");
        }
        finally
        {
            isProcessing = false;
            StateHasChanged();
        }
    }

    private void CloseApproveModal()
    {
        approveModal?.Close();
        selectedReviewForAction = null;
        StateHasChanged();
    }

    // ==================== REJECT ACTIONS ====================

    private async Task ShowRejectDialog(ReviewAdminDto review)
    {
        try
        {
            selectedReviewForAction = await ReviewService.GetReviewByIdAsync(review.Id);
            
            if (selectedReviewForAction == null)
            {
                ToastService.ShowError("Review not found");
                return;
            }

            rejectionReason = "";
            rejectModal?.Open();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Showing reject dialog");
            ToastService.ShowError("Failed to load review details");
        }
    }

    private async Task ConfirmRejectReview()
    {
        if (selectedReviewForAction == null) return;

        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            ToastService.ShowWarning("Please provide a reason for rejection");
            return;
        }

        try
        {
            isProcessing = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                $"❌ Rejecting review: {selectedReviewForAction.Id}",
                LogLevel.Warning
            );

            var success = await ReviewService.RejectReviewAsync(
                selectedReviewForAction.Id,
                rejectionReason
            );

            if (success)
            {
                ToastService.ShowSuccess("Review rejected and deleted");
                await LoadReviews();
                CloseRejectModal();
            }
            else
            {
                ToastService.ShowError("Failed to reject review");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Rejecting review");
            ToastService.ShowError("An error occurred while rejecting the review");
        }
        finally
        {
            isProcessing = false;
            StateHasChanged();
        }
    }

    private void CloseRejectModal()
    {
        rejectModal?.Close();
        selectedReviewForAction = null;
        rejectionReason = "";
        StateHasChanged();
    }

    // ==================== DELETE ACTIONS ====================

    private async Task ShowDeleteDialog(ReviewAdminDto review)
    {
        try
        {
            selectedReviewForAction = await ReviewService.GetReviewByIdAsync(review.Id);
            
            if (selectedReviewForAction == null)
            {
                ToastService.ShowError("Review not found");
                return;
            }

            deleteConfirmation?.Open();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Showing delete dialog");
            ToastService.ShowError("Failed to load review details");
        }
    }

    private async Task ConfirmDeleteReview()
    {
        if (selectedReviewForAction == null) return;

        try
        {
            isProcessing = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                $"🗑️ Deleting review: {selectedReviewForAction.Id}",
                LogLevel.Warning
            );

            var success = await ReviewService.DeleteReviewAsync(selectedReviewForAction.Id);

            if (success)
            {
                ToastService.ShowSuccess("Review deleted successfully");
                await LoadReviews();
                CloseDeleteConfirmation();
            }
            else
            {
                ToastService.ShowError("Failed to delete review");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting review");
            ToastService.ShowError("An error occurred while deleting the review");
        }
        finally
        {
            isProcessing = false;
            StateHasChanged();
        }
    }

    private void CloseDeleteConfirmation()
    {
        deleteConfirmation?.Close();
        selectedReviewForAction = null;
        StateHasChanged();
    }

    // ==================== DETAILS DIALOG ====================

    private async Task ShowDetailsDialog(ReviewAdminDto review)
    {
        try
        {
            selectedReviewForAction = await ReviewService.GetReviewByIdAsync(review.Id);
            
            if (selectedReviewForAction == null)
            {
                ToastService.ShowError("Review not found");
                return;
            }

            detailsModal?.Open();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Showing details dialog");
            ToastService.ShowError("Failed to load review details");
        }
    }

    private void CloseDetailsModal()
    {
        detailsModal?.Close();
        selectedReviewForAction = null;
        StateHasChanged();
    }

    // ==================== BULK ACTIONS ====================

    private async Task ApproveAllPending()
    {
        try
        {
            var pendingReviews = filteredReviews.Where(r => !r.IsApproved).ToList();

            if (!pendingReviews.Any())
            {
                ToastService.ShowWarning("No pending reviews to approve");
                return;
            }

            isProcessing = true;
            StateHasChanged();

            var userId = await PermissionService.GetCurrentUserIdAsync();
            var userEmail = await PermissionService.GetCurrentUserEmailAsync();
            var approvedBy = userEmail ?? userId ?? "admin";

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Approving {pendingReviews.Count} pending reviews",
                LogLevel.Info
            );

            int successCount = 0;
            int failCount = 0;

            foreach (var review in pendingReviews)
            {
                try
                {
                    var success = await ReviewService.ApproveReviewAsync(review.Id, approvedBy);
                    if (success)
                        successCount++;
                    else
                        failCount++;
                }
                catch (Exception ex)
                {
                    await MID_HelperFunctions.LogExceptionAsync(ex, $"Approving review: {review.Id}");
                    failCount++;
                }
            }

            if (successCount > 0)
            {
                ToastService.ShowSuccess($"Approved {successCount} review(s)");
            }

            if (failCount > 0)
            {
                ToastService.ShowWarning($"Failed to approve {failCount} review(s)");
            }

            await LoadReviews();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Approving all pending");
            ToastService.ShowError("An error occurred while approving reviews");
        }
        finally
        {
            isProcessing = false;
            StateHasChanged();
        }
    }

    private async Task ApproveSelectedReviews()
    {
        try
        {
            if (!selectedReviews.Any())
            {
                ToastService.ShowWarning("No reviews selected");
                return;
            }

            isProcessing = true;
            StateHasChanged();

            var userId = await PermissionService.GetCurrentUserIdAsync();
            var userEmail = await PermissionService.GetCurrentUserEmailAsync();
            var approvedBy = userEmail ?? userId ?? "admin";

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Approving {selectedReviews.Count} selected reviews",
                LogLevel.Info
            );

            int successCount = 0;
            int failCount = 0;

            foreach (var reviewId in selectedReviews.ToList())
            {
                try
                {
                    var success = await ReviewService.ApproveReviewAsync(reviewId, approvedBy);
                    if (success)
                        successCount++;
                    else
                        failCount++;
                }
                catch (Exception ex)
                {
                    await MID_HelperFunctions.LogExceptionAsync(ex, $"Approving review: {reviewId}");
                    failCount++;
                }
            }

            if (successCount > 0)
            {
                ToastService.ShowSuccess($"Approved {successCount} review(s)");
            }

            if (failCount > 0)
            {
                ToastService.ShowWarning($"Failed to approve {failCount} review(s)");
            }

            selectedReviews.Clear();
            await LoadReviews();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Approving selected reviews");
            ToastService.ShowError("An error occurred while approving reviews");
        }
        finally
        {
            isProcessing = false;
            StateHasChanged();
        }
    }

    private async Task RejectSelectedReviews()
    {
        try
        {
            if (!selectedReviews.Any())
            {
                ToastService.ShowWarning("No reviews selected");
                return;
            }

            isProcessing = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                $"❌ Rejecting {selectedReviews.Count} selected reviews",
                LogLevel.Warning
            );

            int successCount = 0;
            int failCount = 0;

            foreach (var reviewId in selectedReviews.ToList())
            {
                try
                {
                    var success = await ReviewService.RejectReviewAsync(
                        reviewId,
                        "Bulk rejection by admin"
                    );
                    if (success)
                        successCount++;
                    else
                        failCount++;
                }
                catch (Exception ex)
                {
                    await MID_HelperFunctions.LogExceptionAsync(ex, $"Rejecting review: {reviewId}");
                    failCount++;
                }
            }

            if (successCount > 0)
            {
                ToastService.ShowSuccess($"Rejected {successCount} review(s)");
            }

            if (failCount > 0)
            {
                ToastService.ShowWarning($"Failed to reject {failCount} review(s)");
            }

            selectedReviews.Clear();
            await LoadReviews();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Rejecting selected reviews");
            ToastService.ShowError("An error occurred while rejecting reviews");
        }
        finally
        {
            isProcessing = false;
            StateHasChanged();
        }
    }

    // ==================== AUTO-VERIFY ACTIONS ====================

    private async Task AutoVerifyAndApprove(ReviewAdminDto review)
    {
        try
        {
            isProcessing = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                $"🔍 Auto-verifying review: {review.Id}",
                LogLevel.Info
            );

            // Get full review details
            var fullReview = await ReviewService.GetReviewByIdAsync(review.Id);
            
            if (fullReview == null)
            {
                ToastService.ShowError("Review not found");
                return;
            }

            // Check if user has received the product
            var hasReceived = await OrderService.HasUserReceivedProductAsync(
                fullReview.UserId,
                fullReview.ProductId
            );

            if (hasReceived)
            {
                var userId = await PermissionService.GetCurrentUserIdAsync();
                var userEmail = await PermissionService.GetCurrentUserEmailAsync();
                var approvedBy = $"{userEmail ?? userId ?? "admin"} (auto-verified)";

                var success = await ReviewService.ApproveReviewAsync(fullReview.Id, approvedBy);

                if (success)
                {
                    ToastService.ShowSuccess("✓ Review auto-verified and approved");
                    await LoadReviews();
                }
                else
                {
                    ToastService.ShowError("Failed to approve review");
                }
            }
            else
            {
                ToastService.ShowWarning("User has not received this product - cannot auto-verify");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Auto-verifying review");
            ToastService.ShowError("An error occurred during auto-verification");
        }
        finally
        {
            isProcessing = false;
            StateHasChanged();
        }
    }

    private async Task AutoVerifyAndApproveAll()
    {
        try
        {
            var pendingReviews = filteredReviews.Where(r => !r.IsApproved).ToList();

            if (!pendingReviews.Any())
            {
                ToastService.ShowWarning("No pending reviews to process");
                return;
            }

            isProcessing = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                $"🔍 Auto-verifying {pendingReviews.Count} pending reviews",
                LogLevel.Info
            );

            var userId = await PermissionService.GetCurrentUserIdAsync();
            var userEmail = await PermissionService.GetCurrentUserEmailAsync();
            var approvedBy = $"{userEmail ?? userId ?? "admin"} (auto-verified)";

            int verifiedCount = 0;
            int skippedCount = 0;
            int failCount = 0;

            foreach (var review in pendingReviews)
            {
                try
                {
                    var fullReview = await ReviewService.GetReviewByIdAsync(review.Id);
                    
                    if (fullReview == null)
                    {
                        failCount++;
                        continue;
                    }

                    var hasReceived = await OrderService.HasUserReceivedProductAsync(
                        fullReview.UserId,
                        fullReview.ProductId
                    );

                    if (hasReceived)
                    {
                        var success = await ReviewService.ApproveReviewAsync(fullReview.Id, approvedBy);
                        if (success)
                            verifiedCount++;
                        else
                            failCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                catch (Exception ex)
                {
                    await MID_HelperFunctions.LogExceptionAsync(ex, $"Auto-verifying review: {review.Id}");
                    failCount++;
                }
            }

            if (verifiedCount > 0)
            {
                ToastService.ShowSuccess($"✓ Auto-verified and approved {verifiedCount} review(s)");
            }

            if (skippedCount > 0)
            {
                ToastService.ShowInfo($"⊘ Skipped {skippedCount} review(s) - users haven't received products");
            }

            if (failCount > 0)
            {
                ToastService.ShowWarning($"⚠ Failed to process {failCount} review(s)");
            }

            await LoadReviews();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Auto-verifying all pending");
            ToastService.ShowError("An error occurred during bulk auto-verification");
        }
        finally
        {
            isProcessing = false;
            StateHasChanged();
        }
    }

    // ==================== HELPER METHODS ====================

    private string GetProductName(string productId)
    {
        // Check cache first
        if (productNameCache.TryGetValue(productId, out var cachedName))
        {
            return cachedName;
        }

        // Find in all reviews
        var review = allReviews.FirstOrDefault(r => r.ProductId == productId);
        var productName = review?.ProductName ?? $"Product #{productId}";

        // Cache it
        productNameCache[productId] = productName;

        return productName;
    }

    private string GetInitials(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return "?";

        var parts = userName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 1)
            return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
        
        return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
    }

    private string GetEmptyStateTitle()
    {
        return currentTab switch
        {
            "pending" => "No Pending Reviews",
            "approved" => "No Approved Reviews",
            _ => string.IsNullOrWhiteSpace(searchQuery) ? "No Reviews Yet" : "No Results Found"
        };
    }

    private string GetEmptyStateMessage()
    {
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            return "Try adjusting your search query";
        }

        return currentTab switch
        {
            "pending" => "All reviews have been processed",
            "approved" => "No reviews have been approved yet",
            _ => "Customer reviews will appear here once submitted"
        };
    }

    private int GetTotalCount()
    {
        return currentTab switch
        {
            "pending" => statusCounts.PendingReviews,
            "approved" => statusCounts.ApprovedReviews,
            _ => statusCounts.TotalReviews
        };
    }
}
