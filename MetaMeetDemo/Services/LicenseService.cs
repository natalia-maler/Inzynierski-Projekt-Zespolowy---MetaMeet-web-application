using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace MetaMeetDemo.Services
{
    public class LicenseService
    {
        private readonly GraphServiceClient _adminGraphClient;
        private readonly ILogger<LicenseService> _logger;

        public LicenseService(GraphServiceClient adminGraphClient, ILogger<LicenseService> logger)
        {
            _adminGraphClient = adminGraphClient;
            _logger = logger;
        }

        public async Task<List<SubscribedSku>> GetAvailableLicensesAsync()
        {
            try
            {
                var skus = await _adminGraphClient.SubscribedSkus.GetAsync();
                return skus?.Value?.Where(s => s.PrepaidUnits.Enabled > 0).ToList() ?? new List<SubscribedSku>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania dostępnych licencji");
                return new List<SubscribedSku>();
            }
        }

        public async Task<LicenseAssignmentResult> AssignLicenseToUserAsync(string azureAdUserId, Guid? specificSkuId = null)
        {
            var result = new LicenseAssignmentResult();

            try
            {
                var availableLicenses = await GetAvailableLicensesAsync();

                if (!availableLicenses.Any())
                {
                    result.Success = false;
                    result.ErrorMessage = "Brak dostępnych licencji w organizacji";
                    result.ErrorCode = "NO_LICENSES_AVAILABLE";
                    return result;
                }

                SubscribedSku selectedSku;

                if (specificSkuId.HasValue)
                {
                    selectedSku = availableLicenses.FirstOrDefault(s => s.SkuId == specificSkuId.Value);
                    if (selectedSku == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Nie znaleziono licencji o ID: {specificSkuId}";
                        result.ErrorCode = "LICENSE_NOT_FOUND";
                        return result;
                    }
                }
                else
                {
                    selectedSku = availableLicenses
                        .Where(s => (s.PrepaidUnits.Enabled - s.ConsumedUnits) > 0)
                        .OrderByDescending(s => GetLicensePriority(s.SkuPartNumber))
                        .FirstOrDefault();

                    if (selectedSku == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Wszystkie dostępne licencje zostały wykorzystane";
                        result.ErrorCode = "ALL_LICENSES_CONSUMED";
                        result.AvailableLicenses = availableLicenses.Select(s => new LicenseInfo
                        {
                            SkuId = s.SkuId.ToString(),
                            SkuPartNumber = s.SkuPartNumber,
                            TotalLicenses = s.PrepaidUnits.Enabled ?? 0,
                            UsedLicenses = s.ConsumedUnits ?? 0,
                            AvailableLicenses = (s.PrepaidUnits.Enabled ?? 0) - (s.ConsumedUnits ?? 0)
                        }).ToList();
                        return result;
                    }
                }

                var user = await _adminGraphClient.Users[azureAdUserId].GetAsync(cfg =>
                {
                    cfg.QueryParameters.Select = new[] { "id", "displayName", "assignedLicenses" };
                });

                if (user?.AssignedLicenses?.Any() == true)
                {
                    result.Success = true;
                    result.Message = "Użytkownik już posiada licencję";
                    result.AssignedSkuId = user.AssignedLicenses.First().SkuId.ToString();
                    result.SkuPartNumber = selectedSku.SkuPartNumber;
                    result.AlreadyHadLicense = true;
                    return result;
                }

                var licensesToAssign = new AssignedLicense
                {
                    SkuId = selectedSku.SkuId
                };

                var requestBody = new Microsoft.Graph.Users.Item.AssignLicense.AssignLicensePostRequestBody
                {
                    AddLicenses = new List<AssignedLicense> { licensesToAssign },
                    RemoveLicenses = new List<Guid?>()
                };

                await _adminGraphClient.Users[azureAdUserId].AssignLicense.PostAsync(requestBody);

                result.Success = true;
                result.Message = $"Pomyślnie przypisano licencję: {selectedSku.SkuPartNumber}";
                result.AssignedSkuId = selectedSku.SkuId.ToString();
                result.SkuPartNumber = selectedSku.SkuPartNumber;
                result.AlreadyHadLicense = false;

                _logger.LogInformation($"Przypisano licencję {selectedSku.SkuPartNumber} użytkownikowi {azureAdUserId}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Błąd przypisywania licencji użytkownikowi {azureAdUserId}");

                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.ErrorCode = "ASSIGNMENT_FAILED";

                if (ex.Message.Contains("Insufficient privileges"))
                {
                    result.ErrorCode = "INSUFFICIENT_PERMISSIONS";
                    result.ErrorMessage = "Brak uprawnień do przypisywania licencji. Wymagane: User.ReadWrite.All lub Directory.ReadWrite.All";
                }
                else if (ex.Message.Contains("not found"))
                {
                    result.ErrorCode = "USER_NOT_FOUND";
                    result.ErrorMessage = "Nie znaleziono użytkownika w Azure AD";
                }

                return result;
            }
        }

        public async Task<UserLicenseStatus> GetUserLicenseStatusAsync(string azureAdUserId)
        {
            var status = new UserLicenseStatus();

            try
            {
                var user = await _adminGraphClient.Users[azureAdUserId].GetAsync(cfg =>
                {
                    cfg.QueryParameters.Select = new[] { "id", "displayName", "assignedLicenses" };
                });

                if (user?.AssignedLicenses?.Any() == true)
                {
                    status.HasLicense = true;
                    status.LicenseSkuId = user.AssignedLicenses.First().SkuId.ToString();

                    var skus = await GetAvailableLicensesAsync();
                    var matchingSku = skus.FirstOrDefault(s => s.SkuId == user.AssignedLicenses.First().SkuId);
                    status.SkuPartNumber = matchingSku?.SkuPartNumber ?? "Nieznana licencja";
                }
                else
                {
                    status.HasLicense = false;
                    status.Reason = "Użytkownik nie ma przypisanej licencji";

                    var availableLicenses = await GetAvailableLicensesAsync();
                    if (!availableLicenses.Any())
                    {
                        status.Reason = "W organizacji nie ma żadnych dostępnych licencji Microsoft 365";
                    }
                    else if (!availableLicenses.Any(s => (s.PrepaidUnits.Enabled - s.ConsumedUnits) > 0))
                    {
                        status.Reason = "Wszystkie licencje w organizacji zostały wykorzystane";
                    }
                }

                status.Success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Błąd sprawdzania statusu licencji użytkownika {azureAdUserId}");
                status.Success = false;
                status.ErrorMessage = ex.Message;

                if (ex.Message.Contains("Insufficient privileges"))
                {
                    status.Reason = "Brak uprawnień do odczytu informacji o licencjach";
                }
            }

            return status;
        }

        private int GetLicensePriority(string skuPartNumber)
        {
            return skuPartNumber?.ToUpper() switch
            {
                string s when s.Contains("E5") => 1,
                string s when s.Contains("E3") => 2,
                string s when s.Contains("E1") => 3,
                string s when s.Contains("BUSINESS_PREMIUM") => 4,
                string s when s.Contains("BUSINESS_STANDARD") => 5,
                string s when s.Contains("BUSINESS_BASIC") => 6,
                _ => 999
            };
        }

        public async Task<LicenseCheckResult> CheckBusinessStandardLicenseAsync(string skuIdString)
        {
            var result = new LicenseCheckResult();

            try
            {
                if (!Guid.TryParse(skuIdString, out var skuId))
                {
                    result.Message = "Nieprawidłowy format SKU ID";
                    result.AvailableLicenses = 0;
                    return result;
                }

                var skus = await _adminGraphClient.SubscribedSkus.GetAsync();
                var targetSku = skus?.Value?.FirstOrDefault(s => s.SkuId == skuId);

                if (targetSku == null)
                {
                    result.Message = "Nie znaleziono licencji o podanym SKU ID";
                    result.AvailableLicenses = 0;
                    return result;
                }

                var available = (targetSku.PrepaidUnits?.Enabled ?? 0) - (targetSku.ConsumedUnits ?? 0);

                result.AvailableLicenses = Math.Max(0, available);
                result.TotalLicenses = targetSku.PrepaidUnits?.Enabled ?? 0;
                result.UsedLicenses = targetSku.ConsumedUnits ?? 0;
                result.SkuPartNumber = targetSku.SkuPartNumber;

                if (available > 0)
                {
                    result.Message = $"Dostępne licencje {targetSku.SkuPartNumber}: {available}";
                }
                else
                {
                    result.Message = $"Brak wolnych licencji {targetSku.SkuPartNumber}. Wykorzystano wszystkie: {targetSku.ConsumedUnits}/{targetSku.PrepaidUnits?.Enabled}";
                }

                _logger.LogInformation(result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd sprawdzania dostępności licencji");
                result.Message = $"Błąd: {ex.Message}";
                result.AvailableLicenses = 0;
            }

            return result;
        }
    }

    public class LicenseCheckResult
    {
        public string Message { get; set; } = "";
        public int AvailableLicenses { get; set; }
        public int TotalLicenses { get; set; }
        public int UsedLicenses { get; set; }
        public string? SkuPartNumber { get; set; }
    }

    public class LicenseAssignmentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string? AssignedSkuId { get; set; }
        public string? SkuPartNumber { get; set; }
        public bool AlreadyHadLicense { get; set; }
        public string ErrorMessage { get; set; } = "";
        public string ErrorCode { get; set; } = "";
        public List<LicenseInfo> AvailableLicenses { get; set; } = new();
    }

    public class UserLicenseStatus
    {
        public bool Success { get; set; }
        public bool HasLicense { get; set; }
        public string? LicenseSkuId { get; set; }
        public string? SkuPartNumber { get; set; }
        public string Reason { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }

    public class LicenseInfo
    {
        public string SkuId { get; set; } = "";
        public string SkuPartNumber { get; set; } = "";
        public int TotalLicenses { get; set; }
        public int UsedLicenses { get; set; }
        public int AvailableLicenses { get; set; }
    }
}