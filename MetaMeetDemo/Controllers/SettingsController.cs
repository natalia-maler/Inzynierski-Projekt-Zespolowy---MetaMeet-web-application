using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;

namespace MetaMeetDemo.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly GraphServiceClient _graphClient;

        public SettingsController(GraphServiceClient graphClient)
        {
            _graphClient = graphClient;
        }

        public async Task<IActionResult> Index()
        {
            string licenseName = "Microsoft 365 Standard";
            string licenseStatus = "Unknown";

            try
            {
                var user = await _graphClient.Me.GetAsync(r =>
                {
                    r.QueryParameters.Select = new[] { "assignedLicenses" };
                });

                var subscriptions = await _graphClient.SubscribedSkus.GetAsync();

                if (user.AssignedLicenses != null && user.AssignedLicenses.Count > 0 && subscriptions.Value != null)
                {
                    var userSkuId = user.AssignedLicenses[0].SkuId;
                    var matchedSku = subscriptions.Value.FirstOrDefault(s => s.SkuId == userSkuId);

                    if (matchedSku != null)
                    {
                        licenseName = matchedSku.SkuPartNumber.Replace("_", " ");
                        licenseStatus = matchedSku.CapabilityStatus;
                    }
                }
            }
            catch
            {
            }

            ViewBag.LicenseName = licenseName;
            ViewBag.LicenseStatus = licenseStatus;

            return View();
        }
    }
}