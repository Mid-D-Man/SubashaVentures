using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Payment;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Payment;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.User;

public partial class Transactions
{
    [Inject] private IWalletService WalletService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<Transactions> Logger { get; set; } = default!;

    private List<WalletTransactionViewModel> TransactionsList = new();
    private List<WalletTransactionViewModel> FilteredTransactions = new();
    private bool IsLoading = true;
    private string WalletBalance = "₦0";
    private string UserId = string.Empty;
    private string FilterType = "all";
    
    private int CurrentPage = 1;
    private int PageSize = 20;
    private int TotalPages = 1;

    private string BackArrowSvg = string.Empty;
    private string EmptyTransactionsIconSvg = string.Empty;
    private string ArrowRightSvg = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        try
        {
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

            await LoadSvgsAsync();
            await LoadData();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Transactions page initialization");
            Logger.LogError(ex, "Failed to initialize transactions page");
        }
    }

    private async Task LoadSvgsAsync()
    {
        try
        {
            BackArrowSvg = VisualElements.GenerateSvg(
                "<path fill='currentColor' d='M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z'/>",
                24, 24, "0 0 24 24"
            );

            EmptyTransactionsIconSvg = VisualElements.GenerateSvg(
                "<path fill='currentColor' d='M9 17H7v-7h2v7zm4 0h-2V7h2v10zm4 0h-2v-4h2v4zm2.5 2.1h-15V5h15v14.1zm0-16.1h-15c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h15c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2z'/>",
                64, 64, "0 0 24 24"
            );

            ArrowRightSvg = VisualElements.GenerateSvg(
                "<path fill='currentColor' d='M12 4l-1.41 1.41L16.17 11H4v2h12.17l-5.58 5.59L12 20l8-8z'/>",
                20, 20, "0 0 24 24"
            );

            await MID_HelperFunctions.DebugMessageAsync(
                "SVG icons loaded successfully for transactions page",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading SVG icons for transactions");
            Logger.LogError(ex, "Failed to load SVG icons for transactions page");
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

            var wallet = await WalletService.GetWalletAsync(UserId);
            if (wallet != null)
            {
                WalletBalance = wallet.FormattedBalance;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Wallet balance: {WalletBalance}",
                    LogLevel.Info
                );
            }

            TransactionsList = await WalletService.GetTransactionHistoryAsync(UserId, 0, 100);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {TransactionsList.Count} transactions",
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
        var filtered = FilterType switch
        {
            "credit" => TransactionsList.Where(t => t.Type.Equals("credit", StringComparison.OrdinalIgnoreCase)).ToList(),
            "debit" => TransactionsList.Where(t => t.Type.Equals("debit", StringComparison.OrdinalIgnoreCase) || 
                                                     t.Type.Equals("purchase", StringComparison.OrdinalIgnoreCase)).ToList(),
            "topup" => TransactionsList.Where(t => t.Type.Equals("topup", StringComparison.OrdinalIgnoreCase) ||
                                                    t.Type.Equals("refund", StringComparison.OrdinalIgnoreCase)).ToList(),
            _ => TransactionsList.ToList()
        };

        TotalPages = filtered.Count > 0 
            ? (int)Math.Ceiling(filtered.Count / (double)PageSize) 
            : 1;
        
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

    private string GetTransactionSymbol(string transactionType)
    {
        var type = transactionType.ToLower();
        return type == "credit" || type == "topup" || type == "refund" ? "+" : "-";
    }
}
