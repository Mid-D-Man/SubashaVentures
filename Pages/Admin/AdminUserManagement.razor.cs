// Pages/Admin/AdminUserManagement.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Services.Users;
using SubashaVentures.Domain.User;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Notifications;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using SubashaVentures.Domain.Enums;
using System.Text;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class AdminUserManagement : ComponentBase, IAsyncDisposable
{
    [Inject] private IUserService UserService { get; set; } = default!;
    [Inject] private ILogger<AdminUserManagement> Logger { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // Object pools for performance
    private MID_ComponentObjectPool<List<UserProfileViewModel>>? _userListPool;
    private MID_ComponentObjectPool<UserFormData>? _formDataPool;

    // Component state
    private bool isLoading = true;
    private bool isUserModalOpen = false;
    private bool isDetailsModalOpen = false;
    private bool isEditMode = false;
    private bool isSaving = false;
    private bool showDeleteConfirmation = false;

    private string viewMode = "grid";
    private string searchQuery = "";
    private string selectedStatus = "";
    private string selectedTier = "";
    private string selectedVerification = "";
    private string sortBy = "newest";

    private int currentPage = 1;
    private int pageSize = 24;

    // Stats
    private UserStatistics userStats = new();

    // Data
    private List<UserProfileViewModel> allUsers = new();
    private List<UserProfileViewModel> filteredUsers = new();
    private List<UserProfileViewModel> paginatedUsers = new();
    private List<string> selectedUsers = new();

    // Form state
    private UserFormData userForm = new();
    private Dictionary<string, string> validationErrors = new();

    // Selected user for operations
    private UserProfileViewModel? selectedUser = null;
    private UserProfileViewModel? userToDelete = null;
    private List<string>? usersToDelete = null;

    // Component references
    private DynamicModal? userModal;
    private DynamicModal? detailsModal;
    private ConfirmationPopup? deleteConfirmationPopup;
    private NotificationComponent? notificationComponent;

    private int totalPages => (int)Math.Ceiling(filteredUsers.Count / (double)pageSize);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Initialize object pools
            _userListPool = new MID_ComponentObjectPool<List<UserProfileViewModel>>(
                () => new List<UserProfileViewModel>(),
                list => list.Clear(),
                maxPoolSize: 10
            );

            _formDataPool = new MID_ComponentObjectPool<UserFormData>(
                () => new UserFormData(),
                form => ResetFormData(form),
                maxPoolSize: 5
            );

            await MID_HelperFunctions.DebugMessageAsync("AdminUserManagement initialized", LogLevel.Info);

            // Load initial data
            await Task.WhenAll(
                LoadUsersAsync(),
                LoadUserStatisticsAsync()
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

    private async Task LoadUserStatisticsAsync()
    {
        try
        {
            userStats = await UserService.GetUserStatisticsAsync();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded user statistics: {userStats.TotalUsers} users",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading user statistics");
            ShowErrorNotification("Failed to load statistics");
        }
    }

    private void ApplyFiltersAndSort()
    {
        using var pooledList = _userListPool?.GetPooled();
        var tempList = pooledList?.Object ?? new List<UserProfileViewModel>();

        tempList.AddRange(allUsers.Where(u =>
            (string.IsNullOrEmpty(searchQuery) ||
             u.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             u.Email.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(selectedStatus) || u.AccountStatus == selectedStatus) &&
            (string.IsNullOrEmpty(selectedTier) || u.MembershipTier.ToString() == selectedTier) &&
            (string.IsNullOrEmpty(selectedVerification) || FilterByVerification(u, selectedVerification))
        ));

        filteredUsers = sortBy switch
        {
            "newest" => tempList.OrderByDescending(x => x.CreatedAt).ToList(),
            "oldest" => tempList.OrderBy(x => x.CreatedAt).ToList(),
            "name-az" => tempList.OrderBy(x => x.DisplayName).ToList(),
            "name-za" => tempList.OrderByDescending(x => x.DisplayName).ToList(),
            "spent-high" => tempList.OrderByDescending(x => x.TotalSpent).ToList(),
            "spent-low" => tempList.OrderBy(x => x.TotalSpent).ToList(),
            "orders-high" => tempList.OrderByDescending(x => x.TotalOrders).ToList(),
            _ => tempList
        };

        currentPage = 1;
        UpdatePaginatedUsers();
    }

    private bool FilterByVerification(UserProfileViewModel user, string filter)
    {
        return filter switch
        {
            "verified" => user.IsEmailVerified,
            "unverified" => !user.IsEmailVerified,
            "phone-verified" => user.IsPhoneVerified,
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

    private void HandleTierFilter(ChangeEventArgs e)
    {
        selectedTier = e.Value?.ToString() ?? "";
        ApplyFiltersAndSort();
    }

    private void HandleVerificationFilter(ChangeEventArgs e)
    {
        selectedVerification = e.Value?.ToString() ?? "";
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
        selectedUser = user;
        userForm = MapToFormData(user);
        validationErrors.Clear();
        isUserModalOpen = true;
        StateHasChanged();
    }

    private void OpenUserDetailsModal(UserProfileViewModel user)
    {
        selectedUser = user;
        isDetailsModalOpen = true;
        StateHasChanged();
    }

    private void CloseUserModal()
    {
        isUserModalOpen = false;
        userForm = new UserFormData();
        validationErrors.Clear();
        selectedUser = null;
        StateHasChanged();
    }

    private void CloseDetailsModal()
    {
        isDetailsModalOpen = false;
        selectedUser = null;
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

            if (isEditMode && selectedUser != null)
            {
                var updateRequest = MapToUpdateRequest(userForm);
                var success = await UserService.UpdateUserProfileAsync(selectedUser.Id, updateRequest);

                if (success)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"User updated: {userForm.FirstName} {userForm.LastName}",
                        LogLevel.Info
                    );
                    ShowSuccessNotification($"User '{userForm.FirstName} {userForm.LastName}' updated successfully!");
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
                        $"User created: {userForm.FirstName} {userForm.LastName}",
                        LogLevel.Info
                    );
                    ShowSuccessNotification($"User '{userForm.FirstName} {userForm.LastName}' created successfully!");
                }
                else
                {
                    ShowErrorNotification("Failed to create user");
                    return;
                }
            }

            CloseUserModal();
            await LoadUsersAsync();
            await LoadUserStatisticsAsync();
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
        {
            validationErrors["FirstName"] = "First name is required";
        }

        if (string.IsNullOrWhiteSpace(userForm.LastName))
        {
            validationErrors["LastName"] = "Last name is required";
        }

        if (string.IsNullOrWhiteSpace(userForm.Email))
        {
            validationErrors["Email"] = "Email is required";
        }
        else if (!IsValidEmail(userForm.Email))
        {
            validationErrors["Email"] = "Invalid email format";
        }

        if (!isEditMode && string.IsNullOrWhiteSpace(userForm.Password))
        {
            validationErrors["Password"] = "Password is required";
        }
        else if (!isEditMode && !IsValidPassword(userForm.Password))
        {
            validationErrors["Password"] = "Password must be at least 8 characters with uppercase, lowercase, and number";
        }

        return !validationErrors.Any();
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidPassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) return false;
        
        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        
        return hasUpper && hasLower && hasDigit;
    }

    private async Task HandleToggleSuspend(UserProfileViewModel user, bool suspend)
    {
        try
        {
            var reason = suspend ? "Suspended by administrator" : null;
            var success = await UserService.ToggleSuspendUserAsync(user.Id, suspend, reason);

            if (success)
            {
                ShowSuccessNotification($"User {(suspend ? "suspended" : "activated")} successfully");
                await LoadUsersAsync();
                await LoadUserStatisticsAsync();
            }
            else
            {
                ShowErrorNotification($"Failed to {(suspend ? "suspend" : "activate")} user");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling suspend");
            ShowErrorNotification("Error toggling user status");
        }
    }

    private void HandleDeleteUser(UserProfileViewModel user)
    {
        userToDelete = user;
        usersToDelete = null;
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
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"User deleted: {userToDelete.DisplayName}",
                        LogLevel.Info
                    );
                    ShowSuccessNotification($"User '{userToDelete.DisplayName}' deleted successfully!");
                    await LoadUsersAsync();
                    await LoadUserStatisticsAsync();
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

    private void HandleSendMessage(UserProfileViewModel user)
    {
        // TODO: Navigate to messaging page
        ShowInfoNotification($"Send message to {user.DisplayName} - Feature coming soon");
    }

    private void HandleViewOrders(UserProfileViewModel user)
    {
        // TODO: Navigate to user's orders
        ShowInfoNotification($"View orders for {user.DisplayName} - Feature coming soon");
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

    private void ClearSelection()
    {
        selectedUsers.Clear();
        StateHasChanged();
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
                await LoadUserStatisticsAsync();
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
            var success = await UserService.BulkSuspendUsersAsync(
                selectedUsers, 
                "Bulk suspension by administrator"
            );
            
            if (success)
            {
                ShowSuccessNotification($"{selectedUsers.Count} users suspended");
                selectedUsers.Clear();
                await LoadUsersAsync();
                await LoadUserStatisticsAsync();
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

    private async Task ConfirmBulkDelete()
    {
        if (usersToDelete != null && usersToDelete.Any())
        {
            try
            {
                var successCount = 0;
                foreach (var userId in usersToDelete)
                {
                    if (await UserService.DeleteUserAsync(userId))
                    {
                        successCount++;
                    }
                }
                
                ShowSuccessNotification($"{successCount} users deleted");
                selectedUsers.Clear();
                await LoadUsersAsync();
                await LoadUserStatisticsAsync();
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk delete");
                ShowErrorNotification("Error deleting users");
            }
            finally
            {
                showDeleteConfirmation = false;
                usersToDelete = null;
                StateHasChanged();
            }
        }
    }

    private async Task HandleExport()
    {
        try
        {
            var csv = await UserService.ExportUsersAsync();
            
            if (string.IsNullOrEmpty(csv))
            {
                ShowErrorNotification("No data to export");
                return;
            }

            var fileName = $"users_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            var csvBytes = Encoding.UTF8.GetBytes(csv);
            var base64 = Convert.ToBase64String(csvBytes);

            await JSRuntime.InvokeVoidAsync("downloadFile", fileName, base64, "text/csv");

            ShowSuccessNotification($"Exported {filteredUsers.Count} users successfully!");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Exporting users");
            ShowErrorNotification($"Export failed: {ex.Message}");
        }
    }

    private string GetStatusClass(string status)
    {
        return status.ToLower() switch
        {
            "active" => "status-active",
            "suspended" => "status-suspended",
            "deleted" => "status-deleted",
            _ => "status-inactive"
        };
    }

    private string GetTierColor(MembershipTier tier)
    {
        return tier switch
        {
            MembershipTier.Bronze => "#cd7f32",
            MembershipTier.Silver => "#c0c0c0",
            MembershipTier.Gold => "#ffd700",
            MembershipTier.Platinum => "#e5e4e2",
            _ => "#6b7280"
        };
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

    private void ShowInfoNotification(string message)
    {
        notificationComponent?.ShowInfo(message);
    }

    // Mapping helpers
    private UserFormData MapToFormData(UserProfileViewModel user)
    {
        return new UserFormData
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender
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
            DateOfBirth = form.DateOfBirth,
            Gender = form.Gender,
            SendWelcomeEmail = true
        };
    }

    private UpdateUserRequest MapToUpdateRequest(UserFormData form)
    {
        return new UpdateUserRequest
        {
            FirstName = form.FirstName,
            LastName = form.LastName,
            PhoneNumber = form.PhoneNumber,
            DateOfBirth = form.DateOfBirth,
            Gender = form.Gender
        };
    }

    private void ResetFormData(UserFormData form)
    {
        form.FirstName = "";
        form.LastName = "";
        form.Email = "";
        form.Password = "";
        form.PhoneNumber = null;
        form.DateOfBirth = null;
        form.Gender = null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _userListPool?.Dispose();
            _formDataPool?.Dispose();

            await MID_HelperFunctions.DebugMessageAsync(
                "AdminUserManagement disposed successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error disposing AdminUserManagement");
        }
    }

    private string GetDeleteMessage()
    {
        if (usersToDelete != null && usersToDelete.Any())
        {
            return $"Are you sure you want to delete {usersToDelete.Count} user(s)?";
        }
        
        if (userToDelete != null)
        {
            return $"Are you sure you want to delete '{userToDelete.DisplayName}'?";
        }
        
        return "Are you sure you want to proceed?";
    }

    // Form data class
    public class UserFormData
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
    }
}
