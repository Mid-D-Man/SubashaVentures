using Microsoft.AspNetCore.Components;
using SubashaVentures.Components.Shared.Modals;

namespace SubashaVentures.Pages.User;

public partial class Reviews
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private DynamicModal ReviewModal { get; set; } = default!;
    private ConfirmationPopup DeleteConfirmation { get; set; } = default!;

    private List<ReviewViewModel> ReviewsList = new();
    private ReviewViewModel? EditingReview;
    private string ModalTitle = "";
    private bool IsLoading = true;
    private bool IsModalOpen = false;
    private string? ReviewToDelete;

    private double AverageRating => ReviewsList.Any() ? ReviewsList.Average(r => r.Rating) : 0;
    private int HelpfulCount => ReviewsList.Sum(r => r.HelpfulCount);

    protected override async Task OnInitializedAsync()
    {
        await LoadReviews();
    }

    private async Task LoadReviews()
    {
        IsLoading = true;
        try
        {
            // TODO: Load from service
            await Task.Delay(500);

            ReviewsList = new List<ReviewViewModel>
            {
                new()
                {
                    Id = "1",
                    ProductName = "Men's Cotton T-Shirt",
                    ProductImage = "/images/placeholder.jpg",
                    Rating = 5,
                    Title = "Excellent quality!",
                    Comment = "This t-shirt exceeded my expectations. The fabric is soft and comfortable, and the fit is perfect.",
                    CreatedAt = DateTime.Now.AddDays(-10),
                    HelpfulCount = 12,
                    Images = new() { "/images/review1.jpg" }
                },
                new()
                {
                    Id = "2",
                    ProductName = "Women's Summer Dress",
                    ProductImage = "/images/placeholder.jpg",
                    Rating = 4,
                    Title = "Great dress, minor issue",
                    Comment = "Love the design and color, but the sizing runs a bit small. Order one size up!",
                    CreatedAt = DateTime.Now.AddDays(-15),
                    HelpfulCount = 8,
                    Images = new()
                }
            };
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void EditReview(ReviewViewModel review)
    {
        EditingReview = new ReviewViewModel
        {
            Id = review.Id,
            ProductName = review.ProductName,
            ProductImage = review.ProductImage,
            Rating = review.Rating,
            Title = review.Title,
            Comment = review.Comment,
            Images = new(review.Images)
        };

        ModalTitle = "Edit Review";
        IsModalOpen = true;
    }

    private void SetRating(int rating)
    {
        if (EditingReview != null)
        {
            EditingReview.Rating = rating;
        }
    }

    private async Task SaveReview()
    {
        if (EditingReview == null) return;

        // TODO: Save to service
        await Task.Delay(100);

        var existingReview = ReviewsList.FirstOrDefault(r => r.Id == EditingReview.Id);
        if (existingReview != null)
        {
            existingReview.Rating = EditingReview.Rating;
            existingReview.Title = EditingReview.Title;
            existingReview.Comment = EditingReview.Comment;
        }

        CloseReviewModal();
        StateHasChanged();
    }

    private void CloseReviewModal()
    {
        IsModalOpen = false;
        EditingReview = null;
    }

    private void ConfirmDelete(string reviewId)
    {
        ReviewToDelete = reviewId;
        DeleteConfirmation.Open();
    }

    private async Task DeleteReview()
    {
        if (string.IsNullOrEmpty(ReviewToDelete)) return;

        // TODO: Delete from service
        await Task.Delay(100);

        ReviewsList.RemoveAll(r => r.Id == ReviewToDelete);
        ReviewToDelete = null;
        StateHasChanged();
    }

    private class ReviewViewModel
    {
        public string Id { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string ProductImage { get; set; } = "";
        public int Rating { get; set; }
        public string Title { get; set; } = "";
        public string Comment { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int HelpfulCount { get; set; }
        public List<string> Images { get; set; } = new();
    }
}
