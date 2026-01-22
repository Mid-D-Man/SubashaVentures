// Pages/User/Transactions.razor.cs
using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Payment;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Payment;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using System.Linq;
namespace SubashaVentures.Pages.User;

public partial class Transactions
{
    [Inject] private IWalletService WalletService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<Transactions> Logger { get; set; } = default!;

    // State - RENAMED to avoid collision with class name
    private List<WalletTransactionViewModel> TransactionsList = new();
    private List<WalletTransactionViewModel> FilteredTransactions = new();
    private bool IsLoading = true;
    private string WalletBalance = "₦0";
    private string UserId = string.Empty;
    private string FilterType = "all";
    
    // Pagination
    private int CurrentPage = 1;
    private int PageSize = 20;
    private int TotalPages = 1;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Check authentication
            if (!await PermissionService.EnsureAuthenticatedAsync())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User not authenticated, redirecting to sign in",
                    LogLevel.Warning
                );
                NavigationManager.NavigateTo("signin", true);
                return;
            }

            UserId = await PermissionService.GetCurrentUserIdAsync() ?? string.Empty;
            
            if (string.IsNullOrEmpty(UserId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User ID not found, redirecting to sign in",
                    LogLevel.Warning
                );
                NavigationManager.NavigateTo("signin", true);
                return;
            }

            await LoadData();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Transactions page initialization");
            Logger.LogError(ex, "Failed to initialize transactions page");
        }
    }

    private async Task LoadData()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loading transaction data for user: {UserId}",
                LogLevel.Info
            );

            // Get wallet balance
            var wallet = await WalletService.GetWalletAsync(UserId);
            if (wallet != null)
            {
                WalletBalance = wallet.FormattedBalance;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Wallet balance: {WalletBalance}",
                    LogLevel.Info
                );
            }

            // Get all transactions (we'll paginate client-side for now)
            TransactionsList = await WalletService.GetTransactionHistoryAsync(UserId, 0, 100);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {TransactionsList.Count} transactions",
                LogLevel.Info
            );

            ApplyFilter();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading transaction data");
            Logger.LogError(ex, "Failed to load transactions for user: {UserId}", UserId);
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private void SetFilter(string type)
    {
        FilterType = type;
        CurrentPage = 1;
        ApplyFilter();
        StateHasChanged();
    }

    private async void ApplyFilter()
    {
        // Filter transactions based on selected type
        var filtered = FilterType switch
        {
            "credit" => TransactionsList.Where(t => t.Type.Equals("credit", StringComparison.OrdinalIgnoreCase)).ToList(),
            "debit" => TransactionsList.Where(t => t.Type.Equals("debit", StringComparison.OrdinalIgnoreCase) || 
                                                     t.Type.Equals("purchase", StringComparison.OrdinalIgnoreCase)).ToList(),
            "topup" => TransactionsList.Where(t => t.Type.Equals("topup", StringComparison.OrdinalIgnoreCase) ||
                                                    t.Type.Equals("refund", StringComparison.OrdinalIgnoreCase)).ToList(),
            _ => TransactionsList.ToList()
        };

        // Calculate total pages
        TotalPages = filtered.Count > 0 
            ? (int)Math.Ceiling(filtered.Count / (double)PageSize) 
            : 1;
        
        // Apply pagination
        FilteredTransactions = filtered
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        await MID_HelperFunctions.DebugMessageAsync(
            $"Filter applied: {FilterType}, Page {CurrentPage}/{TotalPages}, Showing {FilteredTransactions.Count} transactions",
            LogLevel.Debug
        );
    }

    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            ApplyFilter();
            StateHasChanged();
        }
    }

    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            ApplyFilter();
            StateHasChanged();
        }
    }

    private void GoBack()
    {
        NavigationManager.NavigateTo("user/payment");
    }

    private void GoToPayment()
    {
        NavigationManager.NavigateTo("user/payment");
    }
}