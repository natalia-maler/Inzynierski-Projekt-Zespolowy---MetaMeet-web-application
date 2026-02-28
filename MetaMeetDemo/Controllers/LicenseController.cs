using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MetaMeetDemo.Services;

namespace MetaMeetDemo.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/licenses")]
    public class LicenseController : ControllerBase
    {
        private readonly LicenseService _licenseService;
        private readonly ILogger<LicenseController> _logger;

        public LicenseController(LicenseService licenseService, ILogger<LicenseController> logger)
        {
            _licenseService = licenseService;
            _logger = logger;
        }

        [HttpGet("my-status")]
        public async Task<IActionResult> GetMyLicenseStatus()
        {
            var azureUserId = User.FindFirst("oid")?.Value
                           ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

            if (string.IsNullOrEmpty(azureUserId))
            {
                return Ok(new { success = false, error = "Nie można pobrać ID użytkownika" });
            }

            var status = await _licenseService.GetUserLicenseStatusAsync(azureUserId);
            return Ok(status);
        }

        [HttpGet("user/{azureUserId}")]
        public async Task<IActionResult> GetUserLicenseStatus(string azureUserId)
        {
            var status = await _licenseService.GetUserLicenseStatusAsync(azureUserId);
            return Ok(status);
        }

        [HttpPost("assign/{azureUserId}")]
        public async Task<IActionResult> AssignLicense(string azureUserId, [FromQuery] string? specificSkuId = null)
        {
            Guid? skuGuid = null;
            if (!string.IsNullOrEmpty(specificSkuId) && Guid.TryParse(specificSkuId, out var parsedGuid))
            {
                skuGuid = parsedGuid;
            }

            var result = await _licenseService.AssignLicenseToUserAsync(azureUserId, skuGuid);
            return Ok(result);
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailableLicenses()
        {
            try
            {
                var licenses = await _licenseService.GetAvailableLicensesAsync();

                var result = licenses.Select(sku => new
                {
                    skuId = sku.SkuId.ToString(),
                    skuPartNumber = sku.SkuPartNumber,
                    totalLicenses = sku.PrepaidUnits?.Enabled ?? 0,
                    usedLicenses = sku.ConsumedUnits ?? 0,
                    availableLicenses = (sku.PrepaidUnits?.Enabled ?? 0) - (sku.ConsumedUnits ?? 0)
                });

                return Ok(new { success = true, licenses = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania dostępnych licencji");
                return Ok(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("test")]
        public async Task<IActionResult> TestLicensePermissions()
        {
            try
            {
                var licenses = await _licenseService.GetAvailableLicensesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Połączenie z Graph API działa poprawnie",
                    licensesFound = licenses.Count,
                    hasLicenses = licenses.Any(),
                    availableLicenses = licenses.Select(l => new
                    {
                        name = l.SkuPartNumber,
                        available = (l.PrepaidUnits?.Enabled ?? 0) - (l.ConsumedUnits ?? 0)
                    })
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message,
                    type = ex.GetType().Name,
                    suggestion = ex.Message.Contains("Insufficient privileges")
                        ? "Dodaj uprawnienia: Organization.Read.All, User.ReadWrite.All"
                        : "Sprawdź połączenie z Microsoft Graph API"
                });
            }
        }
    }
}