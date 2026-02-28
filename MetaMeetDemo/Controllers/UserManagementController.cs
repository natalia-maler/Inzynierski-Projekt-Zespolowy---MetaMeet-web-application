using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MetaMeetDemo.Services;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MetaMeetDemo.Controllers
{
    [Authorize]
    [ApiController]
    public class UserManagementController : Controller
    {
        private readonly UserManagementService _userService;

        public UserManagementController(UserManagementService userService)
        {
            _userService = userService;
        }

        [AllowAnonymous]
        [HttpPost("api/users/create")]
        public async Task<IActionResult> CreateUser(
            [FromQuery] string displayName,
            [FromQuery] string emailPrefix,
            [FromQuery] string password)
        {
            if (string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(emailPrefix) ||
                string.IsNullOrWhiteSpace(password))
            {
                return Json(new { success = false, error = "Brakuje wymaganych danych" });
            }

            var result = await _userService.CreateUserAsync(displayName, emailPrefix, password);
            return Json(result);
        }

        [HttpGet("api/users/local")]
        public async Task<IActionResult> GetLocalUsers()
        {
            var users = await _userService.GetAllLocalUsersAsync();
            return Json(users.Select(u => new
            {
                id = u.Id,
                azureAdUserId = u.AzureAdUserId,
                userPrincipalName = u.UserPrincipalName,
                displayName = u.DisplayName,
                createdAt = u.CreatedAt
            }));
        }
[HttpGet("api/users/search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string query, [FromQuery] int currentUserId)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Json(new List<object>());
            }

            query = query.ToLower();
            var users = await _userService.GetAllLocalUsersAsync();

            var filteredUsers = users
                .Where(u =>
                    (u.DisplayName.ToLower().Contains(query) || u.UserPrincipalName.ToLower().Contains(query))
                    && u.Id != currentUserId
                )
                .Take(10)
                .Select(u => new
                {
                    id = u.Id,
                    displayName = u.DisplayName,
                    userPrincipalName = u.UserPrincipalName,
                    azureAdUserId = u.AzureAdUserId
                });

            return Json(filteredUsers);
        }
        [HttpPost("api/friends/request")]
        public async Task<IActionResult> SendFriendRequest([FromQuery] int senderId, [FromQuery] int recipientId)
        {
            if (senderId <= 0 || recipientId <= 0)
                return Json(new { success = false, error = "Błędne ID użytkowników (oczekiwano int)" });

            var result = await _userService.SendFriendRequestAsync(senderId, recipientId);
            return Json(result);
        }

        [HttpGet("api/notifications/{userId}")]
        public async Task<IActionResult> GetNotifications(int userId)
        {
            var notifs = await _userService.GetUserNotificationsAsync(userId);
            return Json(notifs.Select(n => new
            {
                id = n.Id,
                senderName = n.Sender?.DisplayName ?? "Nieznany",
                senderAzureId = n.Sender?.AzureAdUserId,
                message = n.Message,
                type = n.Type,
                isRead = n.IsRead,
                createdAt = n.CreatedAt
            }));
        }

        [HttpPost("api/notifications/respond")]
        public async Task<IActionResult> RespondToNotification([FromQuery] int notificationId, [FromQuery] bool accept)
        {
            var result = await _userService.RespondToFriendRequestAsync(notificationId, accept);
            return Json(result);
        }

        [HttpGet("api/friends/{userId}")]
        public async Task<IActionResult> GetFriends(int userId)
        {
            var friends = await _userService.GetFriendsAsync(userId);
            return Json(friends.Select(f => new
            {
                id = f.Id,
                azureAdUserId = f.AzureAdUserId,
                userPrincipalName = f.UserPrincipalName,
                displayName = f.DisplayName
            }));
        }

        [HttpPost("api/notifications/mark-read")]
        public async Task<IActionResult> MarkNotificationRead(int notificationId)
        {
            var success = await _userService.MarkNotificationReadAsync(notificationId);
            if (!success) return NotFound();
            return Json(new { success = true });
        }

        [HttpPost("api/friends/remove")]
        public async Task<IActionResult> RemoveFriend([FromQuery] int userId, [FromQuery] int friendId)
        {
            var result = await _userService.RemoveFriendAsync(userId, friendId);
            if (result) return Json(new { success = true });
            return Json(new { success = false, error = "Nie udało się usunąć znajomego lub relacja nie istnieje." });
        }
    }
}