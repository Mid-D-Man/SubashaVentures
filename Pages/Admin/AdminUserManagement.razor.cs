// Pages/Admin/AdminUserManagement.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Services.Users;
using SubashaVentures.Domain.User;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Notifications;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Domain.Enums;
using System.Text;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class AdminUserManagement : ComponentBase, IAsyncDisposable
{
    [Inject] private IUserService UserService { get; set; } = default!;
    [Inject] private ILogger<AdminUserManagement> Logger { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // Component state
    private bool isLoading = true;
    private bool isUserModalOpen = false;
    private bool isEditMode = false;
    private bool isSaving = false;
    private bool showDeleteConfirmation = false;
    private bool showSuspendConfirmation = false;

    private string viewMode = "grid";
    private string searchQuery = "";
    private string selectedStatus = "";
    private string selectedVerification = "";
    private string selectedTier = "";
    private string sortBy = "newest";

    private int currentPage = 1;
    private int pageSize = 24;

    // Stats
    private UserStatistics stats = new();

    // Data
    private List<UserProfileViewModel> allUsers = new();
    private List<UserProfileViewModel> filteredUsers = new();
    private List<UserProfileViewModel> paginatedUsers = new();
    private List<string> selectedUsers = new();

    // Form state
    private UserFormData userForm = new();
    private Dictionary<string, string> validationErrors = new();

    // Delete/Suspend confirmation
    private UserProfileViewModel? userToDelete = null;
    private UserProfileViewModel? userToSuspend = null;

    // Component references
    private DynamicModal? userModal;
    private ConfirmationPopup? deleteConfirmationPopup;
    private ConfirmationPopup? suspendConfirmationPopup;
    private NotificationComponent? notificationComponent;

    private int totalPages => (int)Math.Ceiling(filteredUsers.Count / (double)pageSize);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync("AdminUserManagement initialized", LogLevel.Info);

            // Load initial data
            await Task.WhenAll(
                LoadUsersAsync(),
                LoadStatisticsAsync()
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "AdminUserManagement initialization");
            ShowErrorNotification("Failed to initialize user management");
            isLoading = false;
        }
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var users = await UserService.GetUsersAsync(0, 1000);
            allUsers = users ?? new List<UserProfileViewModel>();

            ApplyFiltersAndSort();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {allUsers.Count} users successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading users");
            Logger.LogError(ex, "Failed to load users");
            ShowErrorNotification("Failed to load users");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadStatisticsAsync()
    {
        try
        {
            stats = await UserService.GetUserStatisticsAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading statistics");
            Logger.LogError(ex, "Failed to load statistics");
        }
    }

    private void ApplyFiltersAndSort()
    {
        var tempList = allUsers.Where(u =>
            (string.IsNullOrEmpty(searchQuery) ||
             u.FullName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             u.Email.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(selectedStatus) || FilterByStatus(u, selectedStatus)) &&
            (string.IsNullOrEmpty(selectedVerification) || FilterByVerification(u, selectedVerification)) &&
            (string.IsNullOrEmpty(selectedTier) || u.MembershipTier.ToString() == selectedTier)
        ).ToList();

        filteredUsers = sortBy switch
        {
            "newest" => tempList.OrderByDescending(x => x.CreatedAt).ToList(),
            "oldest" => tempList.OrderBy(x => x.CreatedAt).ToList(),
            "name-az" => tempList.OrderBy(x => x.FullName).ToList(),
            "name-za" => tempList.OrderByDescending(x => x.FullName).ToList(),
            "orders-high" => tempList.OrderByDescending(x => x.TotalOrders).ToList(),
            "spent-high" => tempList.OrderByDescending(x => x.TotalSpent).ToList(),
            _ => tempList
        };

        currentPage = 1;
        UpdatePaginatedUsers();
    }

    private bool FilterByStatus(UserProfileViewModel user, string status)
    {
        return status.ToLower() switch
        {
            "active" => user.AccountStatus == "Active",
            "suspended" => user.AccountStatus == "Suspended",
            "deleted" => user.AccountStatus == "Deleted",
            _ => true
        };
    }

    private bool FilterByVerification(UserProfileViewModel user, string verification)
    {
        return verification.ToLower() switch
        {
            "verified" => user.IsVerified,
            "unverified" => !user.IsVerified,
            _ => true
        };
    }

    private void UpdatePaginatedUsers()
    {
        paginatedUsers = filteredUsers
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        StateHasChanged();
    }

    private void HandleSearch()
    {
        ApplyFiltersAndSort();
    }

    private void HandleStatusFilter(ChangeEventArgs e)
    {
        selectedStatus = e.Value?.ToString() ?? "";
        ApplyFiltersAndSort();
    }

    private void HandleVerificationFilter(ChangeEventArgs e)
    {
        selectedVerification = e.Value?.ToString() ?? "";
        ApplyFiltersAndSort();
    }

    private void HandleTierFilter(ChangeEventArgs e)
    {
        selectedTier = e.Value?.ToString() ?? "";
        ApplyFiltersAndSort();
    }

    private void HandleSortChange(ChangeEventArgs e)
    {
        sortBy = e.Value?.ToString() ?? "newest";
        ApplyFiltersAndSort();
    }

    private void OpenCreateUserModal()
    {
        isEditMode = false;
        userForm = new UserFormData();
        validationErrors.Clear();
        isUserModalOpen = true;
        StateHasChanged();
    }

    private void OpenEditUserModal(UserProfileViewModel user)
    {
        isEditMode = true;
        userForm = MapToFormData(user);
        validationErrors.Clear();
        isUserModalOpen = true;
        StateHasChanged();
    }

    private void CloseUserModal()
    {
        isUserModalOpen = false;
        userForm = new UserFormData();
        validationErrors.Clear();
        StateHasChanged();
    }

    private async Task HandleSaveUser()
    {
        if (!ValidateUserForm())
        {
            StateHasChanged();
            return;
        }

        try
        {
            isSaving = true;
            StateHasChanged();

            if (isEditMode)
            {
                var updateRequest = MapToUpdateRequest(userForm);
                var success = await UserService.UpdateUserProfileAsync(userForm.Id, updateRequest);

                if (success)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"User updated: {userForm.Email}",
                        LogLevel.Info
                    );
                    ShowSuccessNotification($"User '{userForm.FullName}' updated successfully!");
                }
                else
                {
                    ShowErrorNotification("Failed to update user");
                    return;
                }
            }
            else
            {
                var createRequest = MapToCreateRequest(userForm);
                var result = await UserService.CreateUserAsync(createRequest);

                if (result != null)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"User created: {userForm.Email}",
                        LogLevel.Info
                    );
                    ShowSuccessNotification($"User '{userForm.FullName}' created successfully!");
                }
                else
                {
                    ShowErrorNotification("Failed to create user");
                    return;
                }
            }

            CloseUserModal();
            await LoadUsersAsync();
            await LoadStatisticsAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving user");
            ShowErrorNotification($"Error: {ex.Message}");
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private bool ValidateUserForm()
    {
        validationErrors.Clear();

        if (string.IsNullOrWhiteSpace(userForm.FirstName))
            validationErrors["FirstName"] = "First name is required";

        if (string.IsNullOrWhiteSpace(userForm.LastName))
            validationErrors["LastName"] = "Last name is required";

        if (string.IsNullOrWhiteSpace(userForm.Email))
            validationErrors["Email"] = "Email is required";
        else if (!MID_HelperFunctions.IsValidEmail(userForm.Email))
            validationErrors["Email"] = "Invalid email format";

        if (!isEditMode && string.IsNullOrWhiteSpace(userForm.Password))
            validationErrors["Password"] = "Password is required";
        else if (!isEditMode && userForm.Password.Length < 8)
            validationErrors["Password"] = "Password must be at least 8 characters";

        return !validationErrors.Any();
    }

    private void HandleDeleteUser(UserProfileViewModel user)
    {
        userToDelete = user;
        showDeleteConfirmation = true;
        StateHasChanged();
    }

    private async Task ConfirmDeleteUser()
    {
        if (userToDelete != null)
        {
            try
            {
                var success = await UserService.DeleteUserAsync(userToDelete.Id);
                
                if (success)
                {
                    ShowSuccessNotification($"User '{userToDelete.DisplayName}' deleted successfully!");
                    await LoadUsersAsync();
                    await LoadStatisticsAsync();
                }
                else
                {
                    ShowErrorNotification("Failed to delete user");
                }
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting user");
                ShowErrorNotification($"Error deleting user: {ex.Message}");
            }
            finally
            {
                showDeleteConfirmation = false;
                userToDelete = null;
                StateHasChanged();
            }
        }
    }

    private void HandleSuspendUser(UserProfileViewModel user)
    {
        userToSuspend = user;
        showSuspendConfirmation = true;
        StateHasChanged();
    }

    private async Task ConfirmSuspendUser()
    {
        if (userToSuspend != null)
        {
            try
            {
                var success = await UserService.ToggleSuspendUserAsync(userToSuspend.Id, true, "Suspended by admin");
                
                if (success)
                {
                    ShowSuccessNotification($"User '{userToSuspend.DisplayName}' suspended successfully!");
                    await LoadUsersAsync();
                    await LoadStatisticsAsync();
                }
                else
                {
                    ShowErrorNotification("Failed to suspend user");
                }
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Suspending user");
                ShowErrorNotification($"Error suspending user: {ex.Message}");
            }
            finally
            {
                showSuspendConfirmation = false;
                userToSuspend = null;
                StateHasChanged();
            }
        }
    }

    private async Task HandleActivateUser(UserProfileViewModel user)
    {
        try
        {
            var success = await UserService.ToggleSuspendUserAsync(user.Id, false);
            
            if (success)
            {
                ShowSuccessNotification($"User '{user.DisplayName}' activated successfully!");
                await LoadUsersAsync();
                await LoadStatisticsAsync();
            }
            else
            {
                ShowErrorNotification("Failed to activate user");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Activating user");
            ShowErrorNotification($"Error activating user: {ex.Message}");
        }
    }

    private async Task HandleVerifyEmail(UserProfileViewModel user)
    {
        try
        {
            var success = await UserService.VerifyUserEmailAsync(user.Id);
            
            if (success)
            {
                ShowSuccessNotification($"Email verified for '{user.DisplayName}'!");
                await LoadUsersAsync();
                await LoadStatisticsAsync();
            }
            else
            {
                ShowErrorNotification("Failed to verify email");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Verifying email");
            ShowErrorNotification($"Error verifying email: {ex.Message}");
        }
    }

    private void HandleSelectionChanged(string userId, bool isSelected)
    {
        if (isSelected)
        {
            if (!selectedUsers.Contains(userId))
                selectedUsers.Add(userId);
        }
        else
        {
            selectedUsers.Remove(userId);
        }
        StateHasChanged();
    }

    private void HandleSelectAll(ChangeEventArgs e)
    {
        if (e.Value is bool isChecked)
        {
            if (isChecked)
            {
                selectedUsers = paginatedUsers.Select(u => u.Id).ToList();
            }
            else
            {
                selectedUsers.Clear();
            }
            StateHasChanged();
        }
    }

    private async Task HandleBulkActivate()
    {
        if (!selectedUsers.Any())
        {
            ShowWarningNotification("No users selected");
            return;
        }

        try
        {
            var success = await UserService.BulkActivateUsersAsync(selectedUsers);
            
            if (success)
            {
                ShowSuccessNotification($"{selectedUsers.Count} users activated");
                selectedUsers.Clear();
                await LoadUsersAsync();
                await LoadStatisticsAsync();
            }
            else
            {
                ShowErrorNotification("Failed to activate users");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk activate");
            ShowErrorNotification("Error activating users");
        }
    }

    private async Task HandleBulkSuspend()
    {
        if (!selectedUsers.Any())
        {
            ShowWarningNotification("No users selected");
            return;
        }

        try
        {
            var success = await UserService.BulkSuspendUsersAsync(selectedUsers, "Bulk suspension by admin");
            
            if (success)
            {
                ShowSuccessNotification($"{selectedUsers.Count} users suspended");
                selectedUsers.Clear();
                await LoadUsersAsync();
                await LoadStatisticsAsync();
            }
            else
            {
                ShowErrorNotification("Failed to suspend users");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk suspend");
            ShowErrorNotification("Error suspending users");
        }
    }

    private async Task HandleExport()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var csv = await UserService.ExportUsersAsync(
                filteredUsers.Select(u => u.Id).ToList()
            );

            if (!string.IsNullOrEmpty(csv))
            {
                var fileName = $"users_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                var csvBytes = Encoding.UTF8.GetBytes(csv);
                var base64 = Convert.ToBase64String(csvBytes);

                await JSRuntime.InvokeVoidAsync("downloadFile", fileName, base64, "text/csv");

                ShowSuccessNotification($"Exported {filteredUsers.Count} users successfully!");
            }
            else
            {
                ShowWarningNotification("No data to export");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Exporting users");
            ShowErrorNotification($"Export failed: {ex.Message}");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private string GetStatusClass(string status)
    {
        return status.ToLower() switch
        {
            "active" => "active",
            "suspended" => "suspended",
            "deleted" => "deleted",
            _ => "unknown"
        };
    }

    private string GetTierClass(MembershipTier tier)
    {
        return tier.ToString().ToLower();
    }

    private void PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            UpdatePaginatedUsers();
        }
    }

    private void NextPage()
    {
        if (currentPage < totalPages)
        {
            currentPage++;
            UpdatePaginatedUsers();
        }
    }

    private void GoToPage(int page)
    {
        if (page >= 1 && page <= totalPages)
        {
            currentPage = page;
            UpdatePaginatedUsers();
        }
    }

    // Notification helpers
    private void ShowSuccessNotification(string message)
    {
        notificationComponent?.ShowSuccess(message);
    }

    private void ShowErrorNotification(string message)
    {
        notificationComponent?.ShowError(message);
    }

    private void ShowWarningNotification(string message)
    {
        notificationComponent?.ShowWarning(message);
    }

    private UserFormData MapToFormData(UserProfileViewModel user)
    {
        return new UserFormData
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            AccountStatus = user.AccountStatus,
            SuspensionReason = user.SuspensionReason,
            MembershipTier = user.MembershipTier.ToString(),
            IsEmailVerified = user.IsEmailVerified,
            IsPhoneVerified = user.IsPhoneVerified,
            TotalOrders = user.TotalOrders,
            TotalSpent = user.TotalSpent,
            LoyaltyPoints = user.LoyaltyPoints,
            CreatedAt = user.CreatedAt
        };
    }

    private CreateUserRequest MapToCreateRequest(UserFormData form)
    {
        return new CreateUserRequest
        {
            Email = form.Email,
            Password = form.Password,
            FirstName = form.FirstName,
            LastName = form.LastName,
            PhoneNumber = form.PhoneNumber,
            SendWelcomeEmail = true
        };
    }

    private UpdateUserRequest MapToUpdateRequest(UserFormData form)
    {
        return new UpdateUserRequest
        {
            FirstName = form.FirstName,
            LastName = form.LastName,
            PhoneNumber = form.PhoneNumber
        };
    }

    public async ValueTask DisposeAsync()
    {
        await MID_HelperFunctions.DebugMessageAsync(
            "AdminUserManagement disposed",
            LogLevel.Info
        );
    }

    public class UserFormData
    {
        public string Id { get; set; } = string.Empty;
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string AccountStatus { get; set; } = "Active";
        public string? SuspensionReason { get; set; }
        public string MembershipTier { get; set; } = "Bronze";
        public bool IsEmailVerified { get; set; }
        public bool IsPhoneVerified { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSpent { get; set; }
        public int LoyaltyPoints { get; set; }
        public DateTime CreatedAt { get; set; }
        
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}
