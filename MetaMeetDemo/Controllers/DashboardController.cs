using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MetaMeetDemo.Models;
using MetaMeetDemo.Services;

namespace MetaMeetDemo.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly GraphUserService _userService;
        private readonly GraphMeetingService _meetingService;

        public DashboardController(GraphUserService userService, GraphMeetingService meetingService)
        {
            _userService = userService;
            _meetingService = meetingService;
        }

        public async Task<IActionResult> Index(string date)
        {
            var model = new DashboardViewModel();
            var todayDate = DateTime.Now;
            model.WelcomeMessage = GetRandomWelcomeMessage();

            if (string.IsNullOrEmpty(date) || !DateTime.TryParse(date, out var parsedDate))
            {
                model.SelectedDate = todayDate;
            }
            else
            {
                model.SelectedDate = parsedDate.Date;
            }

            model.PrevDateStr = model.SelectedDate.AddDays(-1).ToString("yyyy-MM-dd");
            model.NextDateStr = model.SelectedDate.AddDays(1).ToString("yyyy-MM-dd");

            var culture = new CultureInfo("pl-PL");
            model.TodayDayName = todayDate.ToString("ddd", culture).ToUpper();
            model.TodayDayNumber = todayDate.Day;
            model.TodayMonthName = todayDate.ToString("MMMM", culture);

            try
            {
                var userTestResult = await _userService.GetCurrentUserInfoAsync(HttpContext);

                model.LicenseInfo = userTestResult;
                model.UserName = userTestResult.DisplayName ?? "Użytkowniku";

                var todayResult = await _meetingService.GetAllMeetingsAsync(todayDate.Date, todayDate.Date.AddDays(1).AddTicks(-1), includeArchived: false);

                var flatTodayList = todayResult.Organized
                    .Concat(todayResult.Invited)
                    .Concat(todayResult.Rejected)
                    .Concat(todayResult.Cancelled)
                    .OrderBy(m => m.Start)
                    .ToList();

                model.TodayEvents = flatTodayList.Select(MapToEventViewModel).ToList();

                List<GraphMeetingService.MeetingInfo> flatCalendarList;

                if (model.IsToday)
                {
                    flatCalendarList = flatTodayList;
                }
                else
                {
                    var calResult = await _meetingService.GetAllMeetingsAsync(model.SelectedDate, model.SelectedDate.AddDays(1).AddTicks(-1), includeArchived: false);

                    flatCalendarList = calResult.Organized
                        .Concat(calResult.Invited)
                        .OrderBy(m => m.Start)
                        .ToList();
                }

                model.CalendarEvents = flatCalendarList.Select(MapToEventViewModel).ToList();

                if (model.CalendarEvents.Any())
                {
                    var sortedEvents = model.CalendarEvents
                        .OrderBy(e => e.Start)
                        .ThenByDescending(e => e.DurationMinutes)
                        .ToList();

                    foreach (var ev in sortedEvents) { ev.ColumnIndex = 0; ev.TotalColumns = 1; }

                    var columnEndTimes = new List<DateTime>();
                    foreach (var ev in sortedEvents)
                    {
                        int assignedCol = -1;
                        for (int c = 0; c < columnEndTimes.Count; c++)
                        {
                            if (columnEndTimes[c] <= ev.Start)
                            {
                                assignedCol = c;
                                columnEndTimes[c] = ev.End;
                                break;
                            }
                        }
                        if (assignedCol == -1)
                        {
                            assignedCol = columnEndTimes.Count;
                            columnEndTimes.Add(ev.End);
                        }
                        ev.ColumnIndex = assignedCol;
                    }

                    foreach (var ev in sortedEvents)
                    {
                        var overlapping = sortedEvents.Where(x => x.Start < ev.End && x.End > ev.Start).ToList();
                        if (overlapping.Any())
                        {
                            int maxCol = overlapping.Max(x => x.ColumnIndex);
                            ev.TotalColumns = maxCol + 1;
                        }
                    }
                    model.CalendarEvents = sortedEvents;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dashboard Error: {ex.Message}");
            }

            return View(model);
        }

        private EventViewModel MapToEventViewModel(GraphMeetingService.MeetingInfo info)
        {
            return new EventViewModel
            {
                Subject = info.Subject,
                Start = info.Start,
                End = info.End,
                JoinUrl = info.JoinUrl
            };
        }

        private string GetRandomWelcomeMessage()
        {
            var messages = new List<string>
            {
                "Dobrze Cię widzieć!", "Gotów na nowe wyzwania?", "Dziś będzie dobry dzień!",
                "Krok po kroku do celu.", "Nie zapomnij się uśmiechnąć!", "Wspaniale Ci idzie!"
            };
            var random = new Random();
            return messages[random.Next(messages.Count)];
        }
    }
}