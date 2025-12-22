using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace SubashaVentures.Pages.User;

public partial class Support
{
    private bool IsLoading = true;
    private bool ShowContactModal = false;
    private bool IsSubmitting = false;
    
    private SupportCategory SelectedCategory = SupportCategory.General;
    private string? ExpandedFaqId = null;
    
    private string ContactSubject = "";
    private SupportCategory ContactCategory = SupportCategory.General;
    private string ContactMessage = "";
    
    private List<FaqViewModel> AllFaqs = new();
    private List<SupportTicketViewModel> SupportTickets = new();

    private List<FaqViewModel> FilteredFaqs => 
        AllFaqs.Where(f => f.Category == SelectedCategory).ToList();

    protected override async Task OnInitializedAsync()
    {
        await LoadSupportData();
    }

    private async Task LoadSupportData()
    {
        IsLoading = true;
        try
        {
            // TODO: Load from actual service
            await Task.Delay(500);

            AllFaqs = new()
            {
                // General
                new() { Id = "1", Category = SupportCategory.General, Question = "How do I create an account?", Answer = "Click on 'Sign Up' at the top right corner, enter your details including email, phone number, and create a password. You'll receive a verification code to confirm your account." },
                new() { Id = "2", Category = SupportCategory.General, Question = "Is my personal information secure?", Answer = "Yes, we use industry-standard encryption to protect your data. We never share your information with third parties without your consent." },
                
                // Orders
                new() { Id = "3", Category = SupportCategory.Orders, Question = "How can I track my order?", Answer = "Go to 'My Orders' section in your account. Click on any order to see real-time tracking information and estimated delivery date." },
                new() { Id = "4", Category = SupportCategory.Orders, Question = "Can I cancel my order?", Answer = "Yes, you can cancel orders before they're shipped. Go to 'My Orders', select the order, and click 'Cancel Order'. Refunds are processed within 5-7 business days." },
                new() { Id = "5", Category = SupportCategory.Orders, Question = "What if I receive a damaged item?", Answer = "Contact support immediately with photos of the damaged item. We'll arrange a replacement or full refund within 24 hours." },
                
                // Payments
                new() { Id = "6", Category = SupportCategory.Payments, Question = "What payment methods do you accept?", Answer = "We accept debit/credit cards (Visa, Mastercard, Verve), bank transfers, USSD, and wallet payments. All transactions are secured." },
                new() { Id = "7", Category = SupportCategory.Payments, Question = "When will I receive my refund?", Answer = "Refunds are processed within 5-7 business days to your original payment method. Wallet refunds are instant." },
                new() { Id = "8", Category = SupportCategory.Payments, Question = "Is it safe to save my card details?", Answer = "Yes, card details are encrypted and stored securely. We never store your CVV. You can remove saved cards anytime." },
                
                // Delivery
                new() { Id = "9", Category = SupportCategory.Delivery, Question = "What are the delivery charges?", Answer = "Delivery within Lagos is ₦1,500. Other states range from ₦2,000-₦4,000. Free delivery on orders above ₦50,000." },
                new() { Id = "10", Category = SupportCategory.Delivery, Question = "How long does delivery take?", Answer = "Lagos: 1-3 business days. Other cities: 3-7 business days. Express delivery available for additional fee." },
                new() { Id = "11", Category = SupportCategory.Delivery, Question = "Can I change my delivery address?", Answer = "Yes, you can update delivery address before the order is shipped. Contact support if the order is already in transit." },
                
                // Returns
                new() { Id = "12", Category = SupportCategory.Returns, Question = "What is your return policy?", Answer = "We offer 7-day returns for most items. Products must be unused, in original packaging with tags. Some items like underwear and cosmetics are non-returnable." },
                new() { Id = "13", Category = SupportCategory.Returns, Question = "How do I initiate a return?", Answer = "Go to 'My Orders', select the item, click 'Return Item', choose reason, and submit. We'll arrange pickup within 48 hours." },
                new() { Id = "14", Category = SupportCategory.Returns, Question = "Who pays for return shipping?", Answer = "If item is defective/damaged, we cover shipping. For change of mind returns, customer covers return shipping cost." },
                
                // Account
                new() { Id = "15", Category = SupportCategory.Account, Question = "How do I reset my password?", Answer = "Click 'Forgot Password' on login page. Enter your email/phone, receive a reset code, and create a new password." },
                new() { Id = "16", Category = SupportCategory.Account, Question = "Can I delete my account?", Answer = "Yes, go to Settings > Account Security > Delete Account. Note: This action is permanent and cannot be undone." },
                new() { Id = "17", Category = SupportCategory.Account, Question = "How do I update my profile?", Answer = "Go to Settings > Profile, edit your information, and click 'Save Changes'. You can update name, phone, email, and profile picture." }
            };

            SupportTickets = new()
            {
                new() { Id = "T-2024-001", Subject = "Order not received", Category = SupportCategory.Delivery, Status = TicketStatus.Open, LastMessage = "I haven't received my order yet, tracking shows delivered but I didn't get it.", CreatedDate = DateTime.Now.AddDays(-2) },
                new() { Id = "T-2024-002", Subject = "Refund status inquiry", Category = SupportCategory.Payments, Status = TicketStatus.InProgress, LastMessage = "Following up on refund for order #12345", CreatedDate = DateTime.Now.AddDays(-5) }
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading support data: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SelectCategory(SupportCategory category)
    {
        SelectedCategory = category;
        ExpandedFaqId = null;
    }

    private void ToggleFaq(string faqId)
    {
        ExpandedFaqId = ExpandedFaqId == faqId ? null : faqId;
    }

    private string GetCategoryName(SupportCategory category) => category switch
    {
        SupportCategory.General => "General",
        SupportCategory.Orders => "Orders",
        SupportCategory.Payments => "Payments",
        SupportCategory.Delivery => "Delivery",
        SupportCategory.Returns => "Returns & Refunds",
        SupportCategory.Account => "Account",
        _ => "General"
    };

    private void OpenContactModal()
    {
        ShowContactModal = true;
    }

    private void CloseContactModal()
    {
        ShowContactModal = false;
        ContactSubject = "";
        ContactMessage = "";
        ContactCategory = SupportCategory.General;
    }

    private async Task SubmitContactForm()
    {
        if (string.IsNullOrWhiteSpace(ContactSubject) || string.IsNullOrWhiteSpace(ContactMessage))
        {
            // TODO: Show validation error
            return;
        }

        IsSubmitting = true;
        try
        {
            // TODO: Submit to actual service
            await Task.Delay(1000);
            Console.WriteLine($"Support ticket submitted: {ContactSubject}");
            CloseContactModal();
            // TODO: Show success message and refresh tickets
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error submitting ticket: {ex.Message}");
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private async Task HandleFileUpload(InputFileChangeEventArgs e)
    {
        // TODO: Implement file upload
        var file = e.File;
        Console.WriteLine($"File selected: {file.Name}");
        await Task.CompletedTask;
    }

    private void ViewTicket(string ticketId)
    {
        // TODO: Navigate to ticket details
        Console.WriteLine($"Viewing ticket: {ticketId}");
    }

    private void ReportScam()
    {
        ContactSubject = "Report Suspicious Activity";
        ContactCategory = SupportCategory.General;
        OpenContactModal();
    }

    private enum SupportCategory
    {
        General,
        Orders,
        Payments,
        Delivery,
        Returns,
        Account
    }

    private enum TicketStatus
    {
        Open,
        InProgress,
        Resolved,
        Closed
    }

    private class FaqViewModel
    {
        public string Id { get; set; } = "";
        public SupportCategory Category { get; set; }
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
    }

    private class SupportTicketViewModel
    {
        public string Id { get; set; } = "";
        public string Subject { get; set; } = "";
        public SupportCategory Category { get; set; }
        public TicketStatus Status { get; set; }
        public string LastMessage { get; set; } = "";
        public DateTime CreatedDate { get; set; }
    }
}
