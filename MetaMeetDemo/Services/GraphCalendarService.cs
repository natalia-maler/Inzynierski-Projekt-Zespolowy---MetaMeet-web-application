using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.Calendar.GetSchedule;

namespace MetaMeetDemo.Services
{
    public class GraphCalendarService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly ILogger<GraphCalendarService> _logger;

        public GraphCalendarService(GraphServiceClient graphClient, ILogger<GraphCalendarService> logger)
        {
            _graphClient = graphClient;
            _logger = logger;
        }

        public async Task<List<Calendar>> GetAllCalendarsAsync()
        {
            var calendars = await _graphClient.Me.Calendars.GetAsync();
            return calendars?.Value?.ToList() ?? new List<Calendar>();
        }

        public async Task<List<Event>> GetCalendarEventsAsync(string? calendarId = null, int? year = null, int? month = null)
        {
            DateTime start;
            if (year.HasValue && month.HasValue)
                start = new DateTime(year.Value, month.Value, 1);
            else
            {
                var now = DateTime.Today;
                start = new DateTime(now.Year, now.Month, 1);
            }
            var end = start.AddMonths(1);

            try
            {
                if (string.IsNullOrEmpty(calendarId))
                {
                    var events = await _graphClient.Me.CalendarView.GetAsync(r =>
                    {
                        r.QueryParameters.StartDateTime = start.ToString("o");
                        r.QueryParameters.EndDateTime = end.ToString("o");
                        r.QueryParameters.Top = 100;
                        r.Headers.Add("Prefer", "outlook.timezone=\"Central European Standard Time\"");
                    });
                    return events?.Value?.ToList() ?? new List<Event>();
                }
                else
                {
                    var events = await _graphClient.Me.Calendars[calendarId].CalendarView.GetAsync(r =>
                    {
                        r.QueryParameters.StartDateTime = start.ToString("o");
                        r.QueryParameters.EndDateTime = end.ToString("o");
                        r.QueryParameters.Top = 100;
                        r.Headers.Add("Prefer", "outlook.timezone=\"Central European Standard Time\"");
                    });
                    return events?.Value?.ToList() ?? new List<Event>();
                }
            }
            catch
            {
                return new List<Event>();
            }
        }

        public async Task<List<Event>> GetUserCalendarOrErrorAsync(string userPrincipalName)
        {
            try
            {
                var response = await _graphClient.Users[userPrincipalName].Calendar.Events
                    .GetAsync(request =>
                    {
                        request.QueryParameters.Top = 200;
                        request.Headers.Add("Prefer", "outlook.timezone=\"Europe/Warsaw\"");
                    });
                return response.Value.ToList();
            }
            catch (Exception ex)
            {
                throw new Exception("Błąd przy odczycie kalendarza: " + ex.Message);
            }
        }

        public async Task<string> CompareCalendarsAsync(string userA, string userB, DateTime date)
        {
            List<Event> eventsA;
            List<Event> eventsB;

            try { eventsA = await GetUserCalendarOrErrorAsync(userA); }
            catch (Exception ex) { return $"❌ Błąd dla użytkownika **{userA}**:\n{ex.Message}"; }

            try { eventsB = await GetUserCalendarOrErrorAsync(userB); }
            catch (Exception ex) { return $"❌ Błąd dla użytkownika **{userB}**:\n{ex.Message}"; }

            bool busyA = eventsA.Any(e => IsOverlapping(e, date));
            bool busyB = eventsB.Any(e => IsOverlapping(e, date));

            if (!busyA && !busyB) return $"✅ Obaj użytkownicy są wolni **{date}**.";
            if (busyA && busyB) return $"❌ Obaj użytkownicy są zajęci **{date}**.";
            if (busyA) return $"❌ Użytkownik **{userA}** jest zajęty.";
            if (busyB) return $"❌ Użytkownik **{userB}** jest zajęty.";

            return "❌ Błąd.";
        }

        public async Task<List<FreeSlot>> GetCommonFreeSlotsAsync(string userA, string userB, DateTime date)
        {
            return await GetCommonFreeSlotsMultiAsync(new List<string> { userA, userB }, date);
        }

        public async Task<List<FreeSlot>> GetCommonFreeSlotsMultiAsync(List<string> userUpns, DateTime date)
        {
            if (userUpns == null || userUpns.Count == 0)
            {
                return new List<FreeSlot>();
            }

            var workStart = date.Date.AddHours(8);
            var workEnd = date.Date.AddHours(17);
            var graphTimeZone = "Central European Standard Time";

            try
            {
                var requestBody = new GetSchedulePostRequestBody
                {
                    Schedules = userUpns,
                    StartTime = new DateTimeTimeZone
                    {
                        DateTime = workStart.ToString("yyyy-MM-ddTHH:mm:ss"),
                        TimeZone = graphTimeZone
                    },
                    EndTime = new DateTimeTimeZone
                    {
                        DateTime = workEnd.ToString("yyyy-MM-ddTHH:mm:ss"),
                        TimeZone = graphTimeZone
                    },
                    AvailabilityViewInterval = 30
                };

                var result = await _graphClient.Users[userUpns[0]].Calendar.GetSchedule.PostAsync(requestBody);

                if (result?.Value == null || result.Value.Count == 0)
                {
                    _logger.LogWarning("GetSchedule nie zwrócił żadnych danych");
                    return new List<FreeSlot>();
                }

                var allBusySlots = new List<(DateTime Start, DateTime End)>();

                foreach (var schedule in result.Value)
                {
                    var busySlots = ProcessScheduleItems(schedule.ScheduleItems);
                    allBusySlots.AddRange(busySlots.Select(b => (b.Start, b.End)));
                }

                var mergedBusy = MergeBusySlots(allBusySlots);

                var freeSlots = CalculateFreeSlots(mergedBusy, workStart, workEnd);

                return freeSlots;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Błąd podczas pobierania wspólnych slotów dla {userUpns.Count} użytkowników");
                throw new Exception($"Nie można pobrać dostępności: {ex.Message}");
            }
        }

        private List<(DateTime Start, DateTime End)> MergeBusySlots(List<(DateTime Start, DateTime End)> busySlots)
        {
            if (busySlots.Count == 0)
                return new List<(DateTime Start, DateTime End)>();

            var sorted = busySlots.OrderBy(b => b.Start).ToList();
            var merged = new List<(DateTime Start, DateTime End)>();

            foreach (var busy in sorted)
            {
                if (merged.Count == 0)
                {
                    merged.Add(busy);
                }
                else
                {
                    var last = merged[^1];

                    if (busy.Start <= last.End)
                    {
                        merged[^1] = (last.Start, busy.End > last.End ? busy.End : last.End);
                    }
                    else
                    {
                        merged.Add(busy);
                    }
                }
            }

            return merged;
        }

        private List<FreeSlot> CalculateFreeSlots(
            List<(DateTime Start, DateTime End)> mergedBusy,
            DateTime workStart,
            DateTime workEnd)
        {
            var freeSlots = new List<FreeSlot>();
            var cursor = workStart;

            foreach (var busy in mergedBusy)
            {
                if (busy.Start > cursor)
                {
                    freeSlots.Add(new FreeSlot { Start = cursor, End = busy.Start });
                }

                if (busy.End > cursor)
                {
                    cursor = busy.End;
                }
            }

            if (cursor < workEnd)
            {
                freeSlots.Add(new FreeSlot { Start = cursor, End = workEnd });
            }

            return freeSlots;
        }

        private List<BusySlot> ProcessScheduleItems(List<ScheduleItem>? items)
        {
            return (items ?? new List<ScheduleItem>())
                .Where(i => i.Status != FreeBusyStatus.Free)
                .Select(i => new BusySlot { Start = ConvertToLocal(i.Start), End = ConvertToLocal(i.End) })
                .OrderBy(i => i.Start)
                .ToList();
        }

        private class BusySlot
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
        }

        private bool IsOverlapping(Event e, DateTime date)
        {
            if (e.Start == null || e.End == null) return false;
            var start = DateTime.Parse(e.Start.DateTime);
            var end = DateTime.Parse(e.End.DateTime);
            if (date == end) return false;
            return date >= start && date < end;
        }

        private DateTime ConvertToLocal(DateTimeTimeZone dtz)
        {
            return TimeZoneInfo.ConvertTime(
                DateTime.Parse(dtz.DateTime),
                TimeZoneInfo.FindSystemTimeZoneById(dtz.TimeZone ?? "UTC"),
                TimeZoneInfo.Local
            );
        }

        public class FreeSlot
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
        }
    }
}