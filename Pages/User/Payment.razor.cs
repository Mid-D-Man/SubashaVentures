using Microsoft.AspNetCore.Components;
using SubashaVentures.Components.Shared.Modals;

namespace SubashaVentures.Pages.User;

public partial class Payment
{
    private List<PaymentMethodViewModel> PaymentMethods = new();
    private List<TransactionViewModel> Transactions = new();
    private PaymentMethodViewModel NewCard = new();
    private Dictionary<string, string> ValidationErrors = new();
    
    private DynamicModal? AddPaymentModal;
    private DynamicModal? TopUpModal;
    private ConfirmationPopup? DeleteConfirmPopup;
    
    private bool IsLoading = true;
    private bool IsAddPaymentModalOpen = false;
    private bool IsTopUpModalOpen = false;
    private bool IsSaving = false;
    private bool IsProcessing = false;
    private string? PaymentToDelete;
    private string TopUpAmount = "";
    private string WalletBalance = "₦0";

    protected override async Task OnInitializedAsync()
    {
        await LoadPaymentMethods();
    }

    private async Task LoadPaymentMethods()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            // TODO: Load from actual service
            await Task.Delay(500);
            
            // Mock data
            PaymentMethods = new List<PaymentMethodViewModel>
            {
                new()
                {
                    Id = "1",
                    CardType = "Visa",
                    LastFourDigits = "4242",
                    ExpiryDate = "12/25",
                    CardholderName = "John Doe",
                    IsDefault = true
                }
            };
            
            Transactions = new List<TransactionViewModel>
            {
                new()
                {
                    Id = "1",
                    Description = "Order Payment",
                    Amount = 45000,
                    Type = "Debit",
                    Date = DateTime.Now.AddDays(-2)
                },
                new()
                {
                    Id = "2",
                    Description = "Wallet Top Up",
                    Amount = 50000,
                    Type = "Credit",
                    Date = DateTime.Now.AddDays(-5)
                }
            };
            
            WalletBalance = "₦25,000";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading payment methods: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private void OpenAddPaymentModal()
    {
        NewCard = new PaymentMethodViewModel();
        ValidationErrors.Clear();
        IsAddPaymentModalOpen = true;
        StateHasChanged();
    }

    private void CloseAddPaymentModal()
    {
        IsAddPaymentModalOpen = false;
        NewCard = new();
        ValidationErrors.Clear();
        StateHasChanged();
    }

    private async Task SavePaymentMethod()
    {
        if (!ValidatePaymentMethod())
            return;

        IsSaving = true;
        StateHasChanged();

        try
        {
            // TODO: Save to actual service
            await Task.Delay(1000);

            NewCard.Id = Guid.NewGuid().ToString();
            PaymentMethods.Add(NewCard);

            if (NewCard.SetAsDefault)
            {
                foreach (var method in PaymentMethods.Where(m => m.Id != NewCard.Id))
                {
                    method.IsDefault = false;
                }
            }

            CloseAddPaymentModal();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving payment method: {ex.Message}");
        }
        finally
        {
            IsSaving = false;
            StateHasChanged();
        }
    }

    private bool ValidatePaymentMethod()
    {
        ValidationErrors.Clear();

        if (string.IsNullOrWhiteSpace(NewCard.CardholderName))
            ValidationErrors["CardholderName"] = "Cardholder name is required";

        if (string.IsNullOrWhiteSpace(NewCard.CardNumber) || NewCard.CardNumber.Length < 13)
            ValidationErrors["CardNumber"] = "Valid card number is required";

        if (string.IsNullOrWhiteSpace(NewCard.ExpiryMonth) || !int.TryParse(NewCard.ExpiryMonth, out int month) || month < 1 || month > 12)
            ValidationErrors["ExpiryMonth"] = "Valid month required (01-12)";

        if (string.IsNullOrWhiteSpace(NewCard.ExpiryYear) || NewCard.ExpiryYear.Length != 2)
            ValidationErrors["ExpiryYear"] = "Valid year required (YY)";

        if (string.IsNullOrWhiteSpace(NewCard.CVV) || NewCard.CVV.Length < 3)
            ValidationErrors["CVV"] = "Valid CVV is required";

        return !ValidationErrors.Any();
    }

    private void ShowTopUpModal()
    {
        TopUpAmount = "";
        IsTopUpModalOpen = true;
        StateHasChanged();
    }

    private void CloseTopUpModal()
    {
        IsTopUpModalOpen = false;
        TopUpAmount = "";
        StateHasChanged();
    }

    private async Task ProcessTopUp()
    {
        if (string.IsNullOrWhiteSpace(TopUpAmount) || !decimal.TryParse(TopUpAmount, out decimal amount) || amount <= 0)
            return;

        IsProcessing = true;
        StateHasChanged();

        try
        {
            // TODO: Process top up via payment gateway
            await Task.Delay(1500);

            // Update wallet balance
            var currentBalance = decimal.Parse(WalletBalance.Replace("₦", "").Replace(",", ""));
            WalletBalance = $"₦{(currentBalance + amount):N0}";

            // Add transaction
            Transactions.Insert(0, new TransactionViewModel
            {
                Id = Guid.NewGuid().ToString(),
                Description = "Wallet Top Up",
                Amount = amount,
                Type = "Credit",
                Date = DateTime.Now
            });

            CloseTopUpModal();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing top up: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
            StateHasChanged();
        }
    }

    private async Task SetDefaultPayment(string paymentId)
    {
        try
        {
            foreach (var method in PaymentMethods)
            {
                method.IsDefault = method.Id == paymentId;
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting default payment: {ex.Message}");
        }
    }

    private void ConfirmDeletePayment(string paymentId)
    {
        PaymentToDelete = paymentId;
        DeleteConfirmPopup?.Open();
    }

    private async Task ConfirmDeletePaymentMethod()
    {
        if (string.IsNullOrEmpty(PaymentToDelete))
            return;

        try
        {
            // TODO: Delete from service
            await Task.Delay(100);

            PaymentMethods.RemoveAll(p => p.Id == PaymentToDelete);
            PaymentToDelete = null;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting payment method: {ex.Message}");
        }
    }

    private string GetCardIcon(string cardType) => cardType.ToLower() switch
    {
        "visa" => "💳",
        "mastercard" => "💳",
        "amex" => "💳",
        _ => "💳"
    };

    private class PaymentMethodViewModel
    {
        public string Id { get; set; } = "";
        public string CardType { get; set; } = "";
        public string LastFourDigits { get; set; } = "";
        public string ExpiryDate { get; set; } = "";
        public string CardholderName { get; set; } = "";
        public string CardNumber { get; set; } = "";
        public string ExpiryMonth { get; set; } = "";
        public string ExpiryYear { get; set; } = "";
        public string CVV { get; set; } = "";
        public bool IsDefault { get; set; }
        public bool SetAsDefault { get; set; }
    }

    private class TransactionViewModel
    {
        public string Id { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public string Type { get; set; } = ""; // Credit or Debit
        public DateTime Date { get; set; }
    }
}
