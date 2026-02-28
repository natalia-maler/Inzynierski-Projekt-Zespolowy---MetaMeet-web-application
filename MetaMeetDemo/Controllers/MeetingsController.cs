using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MetaMeetDemo.Services;
using Microsoft.Identity.Web;
using System.Threading.Tasks;
using System;

namespace MetaMeetDemo.Controllers
{
    [Authorize]
    [AuthorizeForScopes(Scopes = new[] { "Calendars.Read", "Calendars.ReadWrite" })]
    public class MeetingsController : Controller
    {
        private readonly GraphMeetingService _meetingService;

        public MeetingsController(GraphMeetingService meetingService)
        {
            _meetingService = meetingService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("api/meetings/list")]
        public async Task<IActionResult> GetMeetings(DateTime start, DateTime end)
        {
            try
            {
                var result = await _meetingService.GetAllMeetingsAsync(start, end, includeArchived: true);
                return Json(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("api/meetings/cancel")]
        public async Task<IActionResult> CancelMeeting(string eventId)
        {
            var myAzureId = User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                            ?? User.FindFirst("oid")?.Value;

            if (string.IsNullOrEmpty(myAzureId)) return Json(new { success = false, error = "Nie rozpoznano użytkownika." });

            var success = await _meetingService.CancelMeetingAsync(eventId, myAzureId);
            return Json(new { success });
        }

        [HttpPost("api/meetings/decline")]
        public async Task<IActionResult> DeclineMeeting(string icalUid)
        {
            var myAzureId = User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                            ?? User.FindFirst("oid")?.Value;

            if (string.IsNullOrEmpty(myAzureId)) return Json(new { success = false, error = "Nie rozpoznano użytkownika." });

            var (success, error) = await _meetingService.DeclineMeetingAsync(icalUid, myAzureId);
            if (success) return Json(new { success = true });
            return Json(new { success = false, error });
        }

        [HttpPost("api/meetings/accept")]
        public async Task<IActionResult> AcceptMeeting(string icalUid)
        {
            var (success, error) = await _meetingService.AcceptMeetingAsync(icalUid);
            return Json(success ? new { success = true } : new { success = false, error });
        }
    }
}