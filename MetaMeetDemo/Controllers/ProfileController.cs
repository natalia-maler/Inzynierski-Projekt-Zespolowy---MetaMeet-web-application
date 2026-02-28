using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Text.Json;
using MetaMeetDemo.Services;

namespace MetaMeetDemo.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly GraphServiceClient _graphClient;
        private readonly ILogger<ProfileController> _logger;
        private readonly UserManagementService _userManagementService;

        public ProfileController(
            GraphServiceClient graphClient,
            ILogger<ProfileController> logger,
            UserManagementService userManagementService)
        {
            _graphClient = graphClient;
            _logger = logger;
            _userManagementService = userManagementService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var user = await _graphClient.Me.GetAsync(r =>
                {
                    r.QueryParameters.Select = new[] {
                        "displayName", "mail", "userPrincipalName", "jobTitle",
                        "assignedLicenses", "createdDateTime"
                    };
                });

                ViewBag.User = user;

                string licenseName = "Brak danych";
                string licenseStatus = "Unknown";

                try
                {
                    var subscriptions = await _graphClient.SubscribedSkus.GetAsync();
                    if (user?.AssignedLicenses != null && user.AssignedLicenses.Count > 0 && subscriptions?.Value != null)
                    {
                        var userSkuId = user.AssignedLicenses[0].SkuId;
                        var matchedSku = subscriptions.Value.FirstOrDefault(s => s.SkuId == userSkuId);
                        if (matchedSku != null)
                        {
                            licenseName = matchedSku.SkuPartNumber?.Replace("_", " ") ?? "Nieznana";
                            licenseStatus = matchedSku.CapabilityStatus ?? "Unknown";
                        }
                    }
                }
                catch { }

                ViewBag.LicenseName = licenseName;
                ViewBag.LicenseStatus = licenseStatus;

                try
                {
                    var calendars = await _graphClient.Me.Calendars.GetAsync();
                    ViewBag.CalendarCount = calendars?.Value?.Count ?? 0;
                }
                catch { ViewBag.CalendarCount = 0; }

                try
                {
                    var groups = await _graphClient.Me.MemberOf.GetAsync();
                    ViewBag.GroupsCount = groups?.Value?.Count ?? 0;
                }
                catch { ViewBag.GroupsCount = 0; }

                ViewBag.JoinDate = user?.CreatedDateTime?.Year.ToString() ?? "-";

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania profilu");
                return View("Error");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateDisplayName([FromBody] JsonElement data)
        {
            try
            {
                if (!data.TryGetProperty("newName", out var nameElement))
                    return Json(new { success = false, message = "Brak parametru newName" });

                var newName = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(newName))
                    return Json(new { success = false, message = "Nazwa nie może być pusta" });

                var me = await _graphClient.Me.GetAsync(r => r.QueryParameters.Select = new[] { "id" });

                var success = await _userManagementService.UpdateUserDisplayNameAsync(me.Id, newName.Trim());

                if (success)
                {
                    return Json(new { success = true, message = "Nazwa zaktualizowana" });
                }
                else
                {
                    return Json(new { success = false, message = "Błąd aktualizacji w serwisie." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zmiany nazwy");
                return Json(new { success = false, message = "Nieoczekiwany błąd serwera" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadPhoto(IFormFile photo)
        {
            try
            {
                if (photo == null || photo.Length == 0)
                    return Json(new { success = false, message = "Nie wybrano pliku" });

                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(photo.ContentType.ToLower()))
                    return Json(new { success = false, message = "Dozwolone tylko pliki JPG, PNG lub GIF" });

                using var stream = photo.OpenReadStream();
                await _graphClient.Me.Photo.Content.PutAsync(stream);

                return Json(new { success = true, message = "Zdjęcie zaktualizowane" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd uploadu zdjęcia");
                return Json(new { success = false, message = $"Błąd uploadu: {ex.Message}" });
            }
        }


        [HttpGet]
        [Route("/api/user-photo")]
        public async Task<IActionResult> GetUserPhoto()
        {
            try
            {
                var photoStream = await _graphClient.Me.Photos["648x648"].Content.GetAsync();

                return File(photoStream, "image/jpeg");
            }
            catch
            {
                try
                {
                    var photoStream = await _graphClient.Me.Photo.Content.GetAsync();
                    return File(photoStream, "image/jpeg");
                }
                catch
                {
                    return NotFound();
                }
            }
        }

    }
}