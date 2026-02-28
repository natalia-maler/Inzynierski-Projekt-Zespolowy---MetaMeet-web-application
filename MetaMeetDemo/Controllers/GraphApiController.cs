using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using MetaMeetDemo.Services;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;

namespace MetaMeetDemo.Controllers
{
    [Authorize]
    [ApiController]
    [AuthorizeForScopes(ScopeKeySection = "AzureAd:Scopes")]
    public class GraphApiController : Controller
    {
        private readonly GraphUserService _userService;
        private readonly GraphCalendarService _calendarService;
        private readonly GraphMeetingService _meetingService;
        private readonly UserManagementService _dbService;
        private readonly ILogger<GraphApiController> _logger;

        public GraphApiController(
            GraphUserService userService,
            GraphCalendarService calendarService,
            GraphMeetingService meetingService,
            UserManagementService dbService,
            ILogger<GraphApiController> logger)
        {
            _userService = userService;
            _calendarService = calendarService;
            _meetingService = meetingService;
            _dbService = dbService;
            _logger = logger;
        }

        [HttpGet("api/current-user-basic")]
        public async Task<IActionResult> GetCurrentUserBasic()
        {
            var me = await _userService.GetLoggedInUserAsync();
            var localUser = await _dbService.GetUserByUPNAsync(me.UserPrincipalName);

            if (localUser == null) return NotFound(new { error = "Brak usera w bazie." });

            return Json(new
            {
                id = localUser.Id,
                azureId = me.Id,
                userPrincipalName = me.UserPrincipalName,
                displayName = me.DisplayName
            });
        }

        [HttpGet("api/user-info")]
        public async Task<IActionResult> GetUserInfo()
        {
            var result = await _userService.GetCurrentUserInfoAsync(HttpContext);
            return Json(result);
        }


        [HttpGet("api/users/{azureId}/photo")]
        public async Task<IActionResult> GetOtherUserPhoto(string azureId)
        {
            if (string.IsNullOrEmpty(azureId)) return NotFound();
            var stream = await _userService.GetOtherUserPhotoAsync(azureId);
            if (stream == null) return NotFound();
            return File(stream, "image/jpeg");
        }

        [HttpGet("api/users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return Json(users.Select(u => new
            {
                id = u.Id,
                userPrincipalName = u.UserPrincipalName,
                displayName = u.DisplayName
            }));
        }


        [HttpGet("api/calendars")]
        public async Task<IActionResult> GetCalendars()
        {
            var calendars = await _calendarService.GetAllCalendarsAsync();
            return Json(calendars.Select(c => new { id = c.Id, name = c.Name, isDefault = c.IsDefaultCalendar }));
        }

        [HttpGet("api/compare")]
        public async Task<IActionResult> CompareCalendars([FromQuery] string userA, [FromQuery] string userB, [FromQuery] string date)
        {
            if (!DateTime.TryParse(date, out var dateParsed)) return Content("Błędna data");
            try
            {
                var result = await _calendarService.CompareCalendarsAsync(userA, userB, dateParsed);
                return Content(result);
            }
            catch (Exception ex) { return Content("❌ " + ex.Message); }
        }

        [HttpGet("api/free-slots")]
        public async Task<IActionResult> GetFreeSlots([FromQuery] string userA, [FromQuery] string userB, [FromQuery] string date)
        {
            if (!DateTime.TryParse(date, out var dateParsed)) return BadRequest();
            try
            {
                return Json(await _calendarService.GetCommonFreeSlotsAsync(userA, userB, dateParsed));
            }
            catch (Exception ex) { return Content("❌ " + ex.Message); }
        }

        [HttpGet("api/free-slots-multi")]
        public async Task<IActionResult> GetCommonFreeSlotsMulti(
            [FromQuery] string userA,
            [FromQuery] string attendees,
            [FromQuery] string date)
        {
            if (!DateTime.TryParse(date, out var dateParsed))
                return BadRequest(new { error = "Błędna data" });

            try
            {
                var attendeeList = attendees.Split(',')
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrEmpty(a))
                    .ToList();

                var allUsers = new List<string> { userA };
                allUsers.AddRange(attendeeList);

                var freeSlots = await _calendarService.GetCommonFreeSlotsMultiAsync(allUsers, dateParsed);
                return Json(freeSlots);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("api/calendar-html")]
        public async Task<IActionResult> GetCalendarHtml([FromQuery] string? calendarId, [FromQuery] int? year, [FromQuery] int? month)
        {
            int y = year ?? DateTime.Now.Year;
            int m = month ?? DateTime.Now.Month;

            var allEvents = await _calendarService.GetCalendarEventsAsync(calendarId, y, m);

            var events = allEvents
                .Where(e => (e.IsCancelled != true) &&
                            (!e.Subject.StartsWith("Canceled:", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var currentMonthDate = new DateTime(y, m, 1);
            var daysInMonth = DateTime.DaysInMonth(y, m);
            var firstDay = new DateTime(y, m, 1);

            var eventsByDay = events
                .Where(e => !string.IsNullOrEmpty(e.Start?.DateTime))
                .GroupBy(e => DateTime.Parse(e.Start.DateTime).Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            var html = $@"
            <style>
                .cal-table {{ width: 100%; border-collapse: collapse; background: white; box-shadow: 0 2px 10px #eee; table-layout: fixed; }}
                .cal-th {{ background: #f8f9fa; color: #555; padding: 15px; text-align: center; border-bottom: 2px solid #e9ecef; }}
                .cal-td {{ height: 120px; border: 1px solid #e9ecef; vertical-align: top; padding: 8px; font-size: 14px; position: relative; transition: background 0.2s; }}
                .cal-td:hover {{ background-color: #fafafa; }}
                .day-num {{ font-weight: bold; margin-bottom: 8px; display: block; color: #333; }}
                .day-today {{ background-color: #f0f7ff; border: 2px solid #667eea; }}
                .cal-event {{ background: #eef2ff; color: #667eea; padding: 4px 8px; margin: 2px 0; border-radius: 4px; font-size: 11px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; border-left: 3px solid #667eea; cursor: pointer; }}
                .add-event-btn {{ display: none; position: absolute; bottom: 5px; right: 5px; font-size: 18px; color: #28a745; cursor: pointer; text-decoration: none; }}
                .cal-td:hover .add-event-btn {{ display: block; }}
            </style>

            <h2 style='text-align:center; color:#333; margin-bottom:20px;'>{currentMonthDate:MMMM yyyy}</h2>
            <table class='cal-table'>
                <tr><th class='cal-th'>Pon</th><th class='cal-th'>Wt</th><th class='cal-th'>Śr</th><th class='cal-th'>Czw</th><th class='cal-th'>Pt</th><th class='cal-th'>Sob</th><th class='cal-th'>Ndz</th></tr>
                <tr>";

            int weekday = (int)firstDay.DayOfWeek;
            if (weekday == 0) weekday = 7;

            for (int i = 1; i < weekday; i++) html += "<td class='cal-td' style='background:#fcfcfc;'></td>";

            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(y, m, day);
                var isToday = date.Date == DateTime.Today ? "day-today" : "";
                var dateStr = date.ToString("yyyy-MM-dd");

                html += $"<td class='cal-td {isToday}'><span class='day-num'>{day}</span>";
                html += $"<a href='/Schedule?date={dateStr}' class='add-event-btn' title='Dodaj spotkanie'>➕</a>";

                if (eventsByDay.ContainsKey(date))
                {
                    foreach (var ev in eventsByDay[date])
                    {
                        var time = DateTime.Parse(ev.Start.DateTime).ToString("HH:mm");
                        html += $"<div class='cal-event' title='{ev.Subject}'><b>{time}</b> {ev.Subject}</div>";
                    }
                }
                html += "</td>";
                if (((weekday + day - 1) % 7) == 0) html += "</tr><tr>";
            }
            html += "</tr></table>";
            return Content(html, "text/html");
        }


        [HttpPost("api/create-teams-meeting")]
        public async Task<IActionResult> CreateTeamsMeeting(
            [FromQuery] string userA,
            [FromQuery] string? userB = null,
            [FromQuery] string? attendees = null,
            [FromQuery] string? date = null,
            [FromQuery] string? subject = null,
            [FromQuery] string? calendarId = null,
            [FromQuery] int duration = 30)
        {
            var startTime = DateTime.MinValue;

            try
            {
                _logger.LogInformation("Rozpoczęcie tworzenia spotkania: userA={UserA}, attendees={Attendees}, date={Date}",
                    userA, attendees ?? userB, date);

                if (!DateTime.TryParse(date, out startTime))
                {
                    _logger.LogWarning("Błędna data: {Date}", date);
                    return Json(new { success = false, error = "Błędna data" });
                }

                List<string> attendeeList;
                if (!string.IsNullOrEmpty(attendees))
                {
                    attendeeList = attendees.Split(',')
                        .Select(a => a.Trim())
                        .Where(a => !string.IsNullOrEmpty(a))
                        .ToList();
                }
                else if (!string.IsNullOrEmpty(userB))
                {
                    attendeeList = new List<string> { userB };
                }
                else
                {
                    _logger.LogWarning("Brak uczestników");
                    return Json(new { success = false, error = "Brak uczestników" });
                }

                _logger.LogInformation("Tworzenie spotkania z {Count} uczestnikami: {Attendees}",
                    attendeeList.Count, string.Join(", ", attendeeList));

                var meetingEnd = startTime.AddMinutes(duration);
                var allAttendees = new List<string> { userA };
                allAttendees.AddRange(attendeeList);

                var freeSlots = await _calendarService.GetCommonFreeSlotsMultiAsync(allAttendees, startTime.Date);

                bool isSlotFree = freeSlots.Any(slot => startTime >= slot.Start && meetingEnd <= slot.End);
                if (!isSlotFree)
                {
                    _logger.LogWarning("Wybrany czas {Start}-{End} jest zajęty", startTime, meetingEnd);
                    return Json(new
                    {
                        success = false,
                        error = $"Wybrany czas {startTime:HH:mm} - {meetingEnd:HH:mm} jest już zajęty przez inne spotkanie."
                    });
                }

                var result = await _meetingService.CreateTeamsMeetingAsync(
                    userA,
                    attendeeList,
                    startTime,
                    duration,
                    subject ?? "Spotkanie Teams",
                    false,
                    true,
                    null,
                    calendarId
                );

                if (result.Success)
                {
                    _logger.LogInformation("Spotkanie utworzone pomyślnie: EventId={EventId}", result.EventId);
                }
                else
                {
                    _logger.LogError("Błąd tworzenia spotkania: {Error}", result.Error);
                }

                return Json(new
                {
                    success = result.Success,
                    joinUrl = result.JoinUrl,
                    subject = result.Subject,
                    error = result.Error
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wyjątek podczas tworzenia spotkania");
                return Json(new
                {
                    success = false,
                    error = $"{ex.GetType().Name}: {ex.Message}"
                });
            }
        }

        [HttpGet("api/meetings")]
        public async Task<IActionResult> GetMeetings([FromQuery] string start, [FromQuery] string end)
        {
            if (!DateTime.TryParse(start, out var s) || !DateTime.TryParse(end, out var e)) return BadRequest();
            try
            {
                return Json(await _meetingService.GetAllMeetingsAsync(s, e));
            }
            catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
        }
    }
}