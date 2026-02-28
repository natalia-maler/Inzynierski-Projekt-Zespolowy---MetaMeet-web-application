using Microsoft.Graph;
using Microsoft.Graph.Models;
using MetaMeetDemo.Data;
using MetaMeetDemo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Users.Item.AssignLicense;
using Microsoft.Graph.Models.ODataErrors;

namespace MetaMeetDemo.Services
{
    public class UserManagementService
    {
        private readonly GraphServiceClient _adminGraphClient;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<UserManagementService> _logger;
        private readonly IConfiguration _config;
        private readonly LicenseService _licenseService;

        public UserManagementService(
            GraphServiceClient adminGraphClient,
            ApplicationDbContext db,
            ILogger<UserManagementService> logger,
            IConfiguration config,
            LicenseService licenseService)
        {
            _adminGraphClient = adminGraphClient;
            _db = db;
            _logger = logger;
            _config = config;
            _licenseService = licenseService;
        }

        public async Task<CreateUserResult> CreateUserAsync(string displayName, string emailPrefix, string password)
        {
            bool licenseAssigned = false;
            string? licenseError = null;
            DateTime? licenseAssignedAt = null;

            try
            {
                var domain = (_config["AzureAd:Domain"] ?? "twojadomena.onmicrosoft.com").Trim();

                emailPrefix = emailPrefix.Trim();
                if (emailPrefix.Contains("@")) emailPrefix = emailPrefix.Split('@')[0];
                string cleanPrefix = emailPrefix.Replace(" ", "").Replace(",", "").ToLower();
                var userPrincipalName = $"{cleanPrefix}@{domain}";

                _logger.LogInformation("Tworzenie użytkownika w Azure AD: {UserPrincipalName}", userPrincipalName);

                var newUser = new User
                {
                    AccountEnabled = true,
                    DisplayName = displayName,
                    MailNickname = cleanPrefix,
                    UserPrincipalName = userPrincipalName,
                    UsageLocation = "PL",
                    PasswordProfile = new PasswordProfile
                    {
                        ForceChangePasswordNextSignIn = false,
                        Password = password
                    }
                };

                var createdUser = await _adminGraphClient.Users.PostAsync(newUser);

                if (createdUser?.Id == null)
                {
                    return new CreateUserResult { Success = false, Error = "Nie udało się utworzyć użytkownika w Azure AD" };
                }

                _logger.LogInformation("✅ Użytkownik utworzony w Azure AD: {UserId}", createdUser.Id);

                try
                {
                    var businessStandardSkuId = _config["AzureAd:BusinessStandardSkuId"];
                    if (!string.IsNullOrEmpty(businessStandardSkuId))
                    {
                        var status = await _licenseService.CheckBusinessStandardLicenseAsync(businessStandardSkuId);
                        if (status.AvailableLicenses > 0)
                        {
                            try
                            {
                                var skuId = Guid.Parse(businessStandardSkuId);
                                await _adminGraphClient.Users[createdUser.Id].AssignLicense.PostAsync(new AssignLicensePostRequestBody
                                {
                                    AddLicenses = new List<AssignedLicense> { new AssignedLicense { SkuId = skuId } },
                                    RemoveLicenses = new List<Guid?>()
                                });
                                licenseAssigned = true;
                                licenseAssignedAt = DateTime.UtcNow;
                                _logger.LogInformation("🎫 SUKCES: Przypisano licencję Business Standard");
                            }
                            catch (ODataError ex)
                            {
                                licenseError = ex.Error?.Message;
                                _logger.LogError(ex, "Błąd przypisywania licencji Business Standard");
                            }
                        }
                        else
                        {
                            licenseError = "Brak wolnych licencji";
                        }
                    }

                    if (!licenseAssigned)
                    {
                        var result = await _licenseService.AssignLicenseToUserAsync(createdUser.Id);
                        licenseAssigned = result.Success;
                        licenseError = result.ErrorMessage;
                        if (result.Success) licenseAssignedAt = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Wyjątek przy licencjonowaniu");
                    licenseError = ex.Message;
                }

                var appUser = new AppUser
                {
                    AzureAdUserId = createdUser.Id,
                    UserPrincipalName = userPrincipalName,
                    DisplayName = displayName,
                    CreatedAt = DateTime.UtcNow
                };

                _db.AppUsers.Add(appUser);
                await _db.SaveChangesAsync();

                return new CreateUserResult
                {
                    Success = true,
                    AzureAdUserId = createdUser.Id,
                    UserPrincipalName = userPrincipalName,
                    DisplayName = displayName,
                    LicenseAssigned = licenseAssigned,
                    LicenseError = licenseError,
                    LicenseAssignedAt = licenseAssignedAt
                };
            }
            catch (ODataError ex)
            {
                var errorMsg = ex.Error?.Message ?? "";
                var errorCode = ex.Error?.Code ?? "";

                _logger.LogError(ex, "Błąd Graph API (OData): {Code} - {Msg}", errorCode, errorMsg);

                if (errorMsg.Contains("userPrincipalName", StringComparison.OrdinalIgnoreCase) &&
                    errorMsg.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    return new CreateUserResult
                    {
                        Success = false,
                        Error = "Ten login (prefix emaila) jest już zajęty. Spróbuj innego."
                    };
                }

                if (errorMsg.Contains("password", StringComparison.OrdinalIgnoreCase) &&
                    errorMsg.Contains("complexity", StringComparison.OrdinalIgnoreCase))
                {
                    return new CreateUserResult
                    {
                        Success = false,
                        Error = "Hasło jest zbyt słabe. Musi mieć min. 8 znaków, duże litery, małe litery i cyfry."
                    };
                }

                return new CreateUserResult { Success = false, Error = $"Błąd Azure: {errorMsg}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd");
                return new CreateUserResult { Success = false, Error = "Wystąpił nieoczekiwany błąd serwera (SQL/Inne)." };
            }
        }

        public async Task<bool> UpdateUserDisplayNameAsync(string userId, string newDisplayName)
        {
            try
            {
                _logger.LogInformation($"Aktualizacja DisplayName dla użytkownika {userId} na '{newDisplayName}'");

                var userUpdate = new User
                {
                    DisplayName = newDisplayName
                };

                await _adminGraphClient.Users[userId].PatchAsync(userUpdate);

                var localUser = await _db.AppUsers.FirstOrDefaultAsync(u => u.AzureAdUserId == userId);
                if (localUser != null)
                {
                    localUser.DisplayName = newDisplayName;
                    await _db.SaveChangesAsync();
                }

                _logger.LogInformation($"✅ Pomyślnie zaktualizowano DisplayName dla {userId}");
                return true;
            }
            catch (ServiceException ex)
            {
                _logger.LogError(ex, $"❌ Błąd Graph API podczas aktualizacji DisplayName. Status: {ex.ResponseStatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Nieoczekiwany błąd podczas aktualizacji DisplayName");
                return false;
            }
        }

        public async Task<AddFriendResult> SendFriendRequestAsync(int senderId, int recipientId)
        {
            try
            {
                if (senderId == recipientId)
                    return new AddFriendResult { Success = false, Error = "Nie można zaprosić siebie." };

                var exists = await _db.Friendships.AnyAsync(f => f.UserId == senderId && f.FriendId == recipientId);
                if (exists)
                    return new AddFriendResult { Success = false, Error = "Jesteście już znajomymi." };

                var pending = await _db.Notifications.AnyAsync(n =>
                    n.SenderId == senderId && n.RecipientId == recipientId &&
                    n.Type == "FriendRequest" && !n.IsHandled);

                if (pending)
                    return new AddFriendResult { Success = false, Error = "Zaproszenie zostało już wysłane." };

                var sender = await _db.AppUsers.FindAsync(senderId);

                var notification = new Notification
                {
                    SenderId = senderId,
                    RecipientId = recipientId,
                    Type = "FriendRequest",
                    Message = $"Użytkownik {sender?.DisplayName ?? "Nieznany"} chce dodać Cię do znajomych.",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    IsHandled = false
                };

                _db.Notifications.Add(notification);
                await _db.SaveChangesAsync();

                return new AddFriendResult { Success = true, Message = "Wysłano zaproszenie do znajomych." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd wysyłania zaproszenia");
                return new AddFriendResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(int userId)
        {
            return await _db.Notifications
                .Where(n => n.RecipientId == userId && !n.IsHandled)
                .OrderByDescending(n => n.CreatedAt)
                .Include(n => n.Sender)
                .ToListAsync();
        }

        public async Task<bool> MarkNotificationReadAsync(int notificationId)
        {
            var notif = await _db.Notifications.FindAsync(notificationId);
            if (notif == null) return false;

            notif.IsRead = true;
            notif.IsHandled = true;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<AddFriendResult> RespondToFriendRequestAsync(int notificationId, bool accept)
        {
            var notification = await _db.Notifications.FindAsync(notificationId);
            if (notification == null) return new AddFriendResult { Success = false, Error = "Nie znaleziono powiadomienia." };
            if (notification.IsHandled) return new AddFriendResult { Success = false, Error = "Już obsłużone." };

            notification.IsRead = true;
            notification.IsHandled = true;

            if (accept && notification.SenderId.HasValue)
            {
                var f1 = new Friendship { UserId = notification.RecipientId, FriendId = notification.SenderId.Value };
                var f2 = new Friendship { UserId = notification.SenderId.Value, FriendId = notification.RecipientId };
                _db.Friendships.AddRange(f1, f2);
                await _db.SaveChangesAsync();
                return new AddFriendResult { Success = true, Message = "Zaproszenie zaakceptowane!" };
            }

            await _db.SaveChangesAsync();
            return new AddFriendResult { Success = true, Message = "Zaproszenie odrzucone." };
        }

        public async Task<List<AppUser>> GetFriendsAsync(int userId)
        {
            return await _db.Friendships
                .Where(f => f.UserId == userId)
                .Include(f => f.Friend)
                .Select(f => f.Friend)
                .ToListAsync();
        }

        public async Task<bool> RemoveFriendAsync(int userId, int friendId)
        {
            try
            {
                var friendships = await _db.Friendships
                    .Where(f => (f.UserId == userId && f.FriendId == friendId) || (f.UserId == friendId && f.FriendId == userId))
                    .ToListAsync();

                if (friendships.Any())
                {
                    _db.Friendships.RemoveRange(friendships);
                    await _db.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd usuwania znajomego");
                return false;
            }
        }

        public async Task<List<AppUser>> GetAllLocalUsersAsync()
        {
            return await _db.AppUsers.OrderBy(u => u.DisplayName).ToListAsync();
        }

        public async Task<AppUser?> GetUserByUPNAsync(string userPrincipalName)
        {
            return await _db.AppUsers.FirstOrDefaultAsync(u => u.UserPrincipalName == userPrincipalName);
        }
    }

    public class CreateUserResult
    {
        public bool Success { get; set; }
        public string? AzureAdUserId { get; set; }
        public string? UserPrincipalName { get; set; }
        public string? DisplayName { get; set; }
        public bool LicenseAssigned { get; set; }
        public string? LicenseError { get; set; }
        public DateTime? LicenseAssignedAt { get; set; }
        public string? Error { get; set; }
    }

    public class AddFriendResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
    }
}