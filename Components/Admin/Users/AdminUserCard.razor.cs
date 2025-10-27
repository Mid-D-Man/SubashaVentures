// Components/Admin/Users/AdminUserCard.razor.cs
using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.User;

namespace SubashaVentures.Components.Admin.Users;

public partial class AdminUserCard : ComponentBase
{
    [Parameter] public UserProfileViewModel User { get; set; } = default!;
    [Parameter] public bool AllowSelection { get; set; }
    [Parameter] public bool IsSelected { get; set; }
    
    // Callbacks
    [Parameter] public EventCallback<UserProfileViewModel> OnViewDetails { get; set; }
    [Parameter] public EventCallback<UserProfileViewModel> OnSendMessage { get; set; }
    [Parameter] public EventCallback<UserProfileViewModel> OnViewOrders { get; set; }
    [Parameter] public EventCallback<(UserProfileViewModel user, bool isSuspended)> OnToggleSuspend { get; set; }
    [Parameter] public EventCallback<UserProfileViewModel> OnDelete { get; set; }
    [Parameter] public EventCallback<(string userId, bool isSelected)> OnSelectionChanged { get; set; }
    [Parameter] public EventCallback<UserProfileViewModel> OnCardClick { get; set; }

    private string GetStatusClass()
    {
        return User.AccountStatus.ToLower() switch
        {
            "active" => "status-active",
            "suspended" => "status-suspended",
            "deleted" => "status-deleted",
            _ => "status-inactive"
        };
    }

    private string GetMembershipColor()
    {
        return User.MembershipTier switch
        {
            MembershipTier.Bronze => "#cd7f32",
            MembershipTier.Silver => "#c0c0c0",
            MembershipTier.Gold => "#ffd700",
            MembershipTier.Platinum => "#e5e4e2",
            _ => "#6b7280"
        };
    }

    private async Task HandleCardClick()
    {
        if (OnCardClick.HasDelegate)
        {
            await OnCardClick.InvokeAsync(User);
        }
    }

    private async Task HandleViewDetails()
    {
        if (OnViewDetails.HasDelegate)
        {
            await OnViewDetails.InvokeAsync(User);
        }
    }

    private async Task HandleSendMessage()
    {
        if (OnSendMessage.HasDelegate)
        {
            await OnSendMessage.InvokeAsync(User);
        }
    }

    private async Task HandleViewOrders()
    {
        if (OnViewOrders.HasDelegate)
        {
            await OnViewOrders.InvokeAsync(User);
        }
    }

    private async Task HandleToggleSuspend()
    {
        var newStatus = User.AccountStatus == "Active";
        if (OnToggleSuspend.HasDelegate)
        {
            await OnToggleSuspend.InvokeAsync((User, newStatus));
        }
    }

    private async Task HandleDelete()
    {
        if (OnDelete.HasDelegate)
        {
            await OnDelete.InvokeAsync(User);
        }
    }

    private async Task HandleSelectionChange(ChangeEventArgs e)
    {
        if (e.Value is bool isChecked && OnSelectionChanged.HasDelegate)
        {
            await OnSelectionChanged.InvokeAsync((User.Id, isChecked));
        }
    }
}
