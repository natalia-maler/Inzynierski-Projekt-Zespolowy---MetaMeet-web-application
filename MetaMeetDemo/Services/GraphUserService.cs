using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Web;
using System.Security.Claims;

namespace MetaMeetDemo.Services
{
    public class GraphUserService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly ILogger<GraphUserService> _logger;

        public GraphUserService(GraphServiceClient graphClient, ILogger<GraphUserService> logger)
        {
            _graphClient = graphClient;
            _logger = logger;
        }

        public async Task<User> GetLoggedInUserAsync()
        {
            return await _graphClient.Me.GetAsync();
        }

        public async Task<Stream?> GetUserPhotoAsync()
        {
            try
            {
                return await _graphClient.Me.Photos["48x48"].Content.GetAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<Stream?> GetOtherUserPhotoAsync(string userId)
        {
            try
            {
                return await _graphClient.Users[userId].Photos["48x48"].Content.GetAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            var response = await _graphClient.Users.GetAsync(request =>
            {
                request.QueryParameters.Top = 20;
            });
            return response.Value.ToList();
        }

        public async Task<UserTestResult> GetCurrentUserInfoAsync(HttpContext httpContext)
        {
            var start = DateTime.UtcNow;
            var result = new UserTestResult();

            try
            {
                var userEmail = httpContext.User.FindFirst(ClaimTypes.Email)?.Value
                              ?? httpContext.User.FindFirst("preferred_username")?.Value
                              ?? httpContext.User.FindFirst("upn")?.Value;

                result.UserEmail = userEmail ?? "Brak emaila";
                result.UserName = httpContext.User.FindFirst(ClaimTypes.Name)?.Value ?? "Nieznany";

                var me = await _graphClient.Me.GetAsync(cfg =>
                {
                    cfg.QueryParameters.Select = new[] { "id", "displayName", "mail", "jobTitle", "assignedLicenses" };
                });

                if (me != null)
                {
                    result.IsConnected = true;
                    result.DisplayName = me.DisplayName ?? "Brak nazwy";
                    result.Mail = me.Mail ?? me.UserPrincipalName ?? "Brak emaila";
                    result.JobTitle = me.JobTitle ?? "Brak stanowiska";

                    result.HasM365License = me.AssignedLicenses?.Any() ?? false;

                    if (result.HasM365License)
                    {
                        var firstLicense = me.AssignedLicenses.First();
                        result.LicenseStatus = "Active";

                        try
                        {
                            var skus = await _graphClient.SubscribedSkus.GetAsync();
                            var matchingSku = skus?.Value?.FirstOrDefault(s => s.SkuId == firstLicense.SkuId);
                            result.LicenseSku = matchingSku?.SkuPartNumber ?? firstLicense.SkuId.ToString();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Nie można pobrać nazwy SKU licencji");
                            result.LicenseSku = firstLicense.SkuId.ToString();
                        }
                    }
                    else
                    {
                        result.LicenseStatus = "Missing";

                        try
                        {
                            var orgLicenses = await _graphClient.SubscribedSkus.GetAsync();

                            if (orgLicenses?.Value?.Any() == true)
                            {
                                foreach (var sku in orgLicenses.Value)
                                {
                                    var available = (sku.PrepaidUnits?.Enabled ?? 0) - (sku.ConsumedUnits ?? 0);

                                    result.OrganizationLicenses.Add(new LicenseOrgInfo
                                    {
                                        SkuPartNumber = sku.SkuPartNumber ?? "Unknown",
                                        Total = sku.PrepaidUnits?.Enabled ?? 0,
                                        Used = sku.ConsumedUnits ?? 0,
                                        Available = Math.Max(0, available)
                                    });
                                }

                                var totalAvailable = result.OrganizationLicenses.Sum(l => l.Available);

                                if (result.OrganizationLicenses.Count == 0)
                                {
                                    result.LicenseIssueReason = "NO_LICENSES_IN_ORG";
                                }
                                else if (totalAvailable == 0)
                                {
                                    result.LicenseIssueReason = "ALL_LICENSES_CONSUMED";
                                }
                                else
                                {
                                    result.LicenseIssueReason = "NOT_ASSIGNED";
                                }
                            }
                            else
                            {
                                result.LicenseIssueReason = "NO_SUBSCRIPTION";
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Nie można pobrać informacji o licencjach organizacji");
                            result.LicenseIssueReason = "CANNOT_CHECK_ORG_LICENSES";
                        }
                    }
                }
                else
                {
                    result.IsConnected = false;
                    result.Error = "Brak odpowiedzi z Graph API";
                    result.LicenseStatus = "Error";
                }
            }
            catch (Exception ex)
            {
                result.IsConnected = false;
                result.Error = $"{ex.GetType().Name}: {ex.Message}";
                result.LicenseStatus = "Error";
                _logger.LogError(ex, "Błąd pobierania danych usera");
            }

            result.ResponseTime = $"{(DateTime.UtcNow - start).TotalMilliseconds:F0} ms";
            return result;
        }

        public async Task<List<LicenseOrgInfo>> GetOrganizationLicensesAsync()
        {
            var result = new List<LicenseOrgInfo>();

            try
            {
                var skus = await _graphClient.SubscribedSkus.GetAsync();

                if (skus?.Value != null)
                {
                    foreach (var sku in skus.Value)
                    {
                        var available = (sku.PrepaidUnits?.Enabled ?? 0) - (sku.ConsumedUnits ?? 0);

                        result.Add(new LicenseOrgInfo
                        {
                            SkuPartNumber = sku.SkuPartNumber ?? "Unknown",
                            Total = sku.PrepaidUnits?.Enabled ?? 0,
                            Used = sku.ConsumedUnits ?? 0,
                            Available = Math.Max(0, available)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania licencji organizacji");
            }

            return result;
        }

        public class UserTestResult
        {
            public bool IsConnected { get; set; }
            public string UserName { get; set; } = "";
            public string UserEmail { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string Mail { get; set; } = "";
            public string UserPrincipalName { get; set; } = "";
            public string JobTitle { get; set; } = "";
            public string Error { get; set; } = "";
            public string ResponseTime { get; set; } = "";
            public bool TeamsConnectionSuccess { get; set; }
            public string TeamsError { get; set; } = "";
            public List<string> TeamsNames { get; set; } = new();
            public bool CalendarConnectionSuccess { get; set; }
            public string CalendarError { get; set; } = "";
            public List<string> CalendarNames { get; set; } = new();

            public bool HasM365License { get; set; } = false;
            public string? LicenseSku { get; set; } = null;
            public string? LicenseStatus { get; set; } = null;
            public string? LicenseIssueReason { get; set; } = null;
            public int TotalLicensesInOrg { get; set; } = 0;
            public int AvailableLicensesInOrg { get; set; } = 0;
            public List<LicenseOrgInfo> OrganizationLicenses { get; set; } = new();

        }
        public class LicenseOrgInfo
        {
            public string SkuPartNumber { get; set; } = "";
            public int Total { get; set; }
            public int Used { get; set; }
            public int Available { get; set; }
        }
    }
}