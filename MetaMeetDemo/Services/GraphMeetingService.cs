using Microsoft.Graph;
using Microsoft.Graph.Models;
using MetaMeetDemo.Data;
using MetaMeetDemo.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace MetaMeetDemo.Services
{
    public class GraphMeetingService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<GraphMeetingService> _logger;

        public GraphMeetingService(
            GraphServiceClient graphClient,
            ApplicationDbContext db,
            ILogger<GraphMeetingService> logger)
        {
            _graphClient = graphClient;
            _db = db;
            _logger = logger;
        }

        public async Task<MeetingListResult> GetAllMeetingsAsync(DateTime startDate, DateTime endDate, bool includeArchived = false)
        {
            var eventsResponse = await _graphClient.Me.CalendarView.GetAsync(r =>
            {
                r.QueryParameters.StartDateTime = startDate.ToString("o");
                r.QueryParameters.EndDateTime = endDate.ToString("o");
                r.QueryParameters.Top = 500;
                r.Headers.Add("Prefer", "outlook.timezone=\"Europe/Warsaw\"");
                r.QueryParameters.Select = new[] {
                    "subject", "start", "end", "organizer", "attendees",
                    "isCancelled", "responseStatus", "iCalUId", "id", "onlineMeeting",
                    "categories", "webLink"
                };
            });

            var graphEvents = eventsResponse?.Value ?? new List<Event>();

            var me = await _graphClient.Me.GetAsync();
            var currentUserEmail = me?.Mail ?? me?.UserPrincipalName ?? string.Empty;

            var result = new MeetingListResult
            {
                CurrentUserEmail = currentUserEmail
            };

            var processedIds = new HashSet<string>();

            foreach (var ev in graphEvents)
            {
                if (ev.Start == null || ev.End == null) continue;

                processedIds.Add(ev.ICalUId);

                var organizerEmail = ev.Organizer?.EmailAddress?.Address ?? "";
                bool isCancelled = (ev.IsCancelled ?? false) ||
                                   (ev.Categories != null && ev.Categories.Contains("Canceled")) ||
                                   (ev.Subject != null && ev.Subject.StartsWith("Canceled:", StringComparison.OrdinalIgnoreCase));

                var info = new MeetingInfo
                {
                    Id = ev.Id,
                    ICalUId = ev.ICalUId,
                    Subject = ev.Subject ?? "Brak tematu",
                    Organizer = ev.Organizer?.EmailAddress?.Name ?? "",
                    OrganizerEmail = organizerEmail,
                    Start = DateTime.Parse(ev.Start.DateTime),
                    End = DateTime.Parse(ev.End.DateTime),
                    JoinUrl = ev.OnlineMeeting?.JoinUrl ?? "",
                    IsCancelled = isCancelled,
                    IsOrganizer = organizerEmail.Equals(currentUserEmail, StringComparison.OrdinalIgnoreCase),
                    ResponseStatus = ev.ResponseStatus?.Response?.ToString() ?? "none",
                    Attendees = ev.Attendees?.Select(a => new AttendeeInfo { Name = a.EmailAddress?.Name, Email = a.EmailAddress?.Address }).ToList() ?? new()
                };

                if (info.IsCancelled) result.Cancelled.Add(info);
                else if (info.ResponseStatus.Equals("declined", StringComparison.OrdinalIgnoreCase)) result.Rejected.Add(info);
                else if (info.IsOrganizer) result.Organized.Add(info);
                else result.Invited.Add(info);
            }

            if (includeArchived)
            {
                var dbCancelledMeetings = await _db.Meetings
                    .Where(m => m.OrganizerEmail == currentUserEmail &&
                                m.IsCancelled == true &&
                                m.Start >= startDate && m.Start <= endDate)
                    .ToListAsync();

                foreach (var dbMeeting in dbCancelledMeetings)
                {
                    if (!processedIds.Contains(dbMeeting.ICalUId))
                    {
                        var restoredAttendees = new List<AttendeeInfo>();
                        if (!string.IsNullOrEmpty(dbMeeting.AttendeesJson))
                        {
                            try
                            {
                                restoredAttendees = JsonSerializer.Deserialize<List<AttendeeInfo>>(dbMeeting.AttendeesJson)
                                                    ?? new List<AttendeeInfo>();
                            }
                            catch { }
                        }

                        var restoredInfo = new MeetingInfo
                        {
                            Id = dbMeeting.GraphEventId,
                            ICalUId = dbMeeting.ICalUId,
                            Subject = dbMeeting.Subject,
                            Start = dbMeeting.Start,
                            End = dbMeeting.End,
                            Organizer = "Ja (Archiwum)",
                            OrganizerEmail = currentUserEmail,
                            IsCancelled = true,
                            IsOrganizer = true,
                            JoinUrl = dbMeeting.JoinUrl,
                            ResponseStatus = "organizer",
                            Attendees = restoredAttendees
                        };
                        result.Cancelled.Add(restoredInfo);
                    }
                }
            }

            result.Organized = SortByDate(result.Organized);
            result.Invited = SortByDate(result.Invited);
            result.Rejected = SortByDate(result.Rejected);
            result.Cancelled = SortByDate(result.Cancelled);

            return result;
        }

        private List<MeetingInfo> SortByDate(List<MeetingInfo> list)
        {
            return list.OrderByDescending(m => m.Start).ToList();
        }

        public async Task<MeetingCreationResult> CreateTeamsMeetingAsync(
             string organizerUpn, List<string> attendeeUpns, DateTime startTime, int durationMinutes = 30,
             string subject = "Spotkanie Teams", bool allowNewTimeProposals = false, bool isMetaMeet = true,
             string htmlBody = null, string? targetCalendarId = null)
        {
            try
            {
                var endTime = startTime.AddMinutes(durationMinutes);
                if (isMetaMeet && !subject.Contains("[MetaMeet]")) subject = $"{subject} [MetaMeet]";

                var @event = new Microsoft.Graph.Models.Event
                {
                    Subject = subject,
                    Body = new ItemBody { ContentType = BodyType.Html, Content = htmlBody ?? subject },
                    Start = new DateTimeTimeZone { DateTime = startTime.ToString("o"), TimeZone = "Central European Standard Time" },
                    End = new DateTimeTimeZone { DateTime = endTime.ToString("o"), TimeZone = "Central European Standard Time" },
                    IsOnlineMeeting = true,
                    OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness,
                    Attendees = attendeeUpns.Select(upn => new Attendee { EmailAddress = new EmailAddress { Address = upn }, Type = AttendeeType.Required }).ToList()
                };

                var createdEvent = await _graphClient.Me.Events.PostAsync(@event);

                var attendeesList = attendeeUpns.Select(email => new AttendeeInfo
                {
                    Name = email,
                    Email = email,
                    Response = "none"
                }).ToList();

                string attendeesJson = JsonSerializer.Serialize(attendeesList);

                var dbEntity = new MeetingEntity
                {
                    GraphEventId = createdEvent.Id,
                    ICalUId = createdEvent.ICalUId,
                    Subject = createdEvent.Subject,

                    Start = !string.IsNullOrEmpty(createdEvent.Start?.DateTime)
                            ? DateTime.Parse(createdEvent.Start.DateTime)
                            : startTime,

                    End = !string.IsNullOrEmpty(createdEvent.End?.DateTime)
                          ? DateTime.Parse(createdEvent.End.DateTime)
                          : endTime,

                    OrganizerEmail = organizerUpn,
                    JoinUrl = createdEvent.OnlineMeeting?.JoinUrl,
                    IsCancelled = false,
                    AttendeesJson = attendeesJson
                };

                _db.Meetings.Add(dbEntity);
                await _db.SaveChangesAsync();

                return new MeetingCreationResult
                {
                    Success = true,
                    EventId = createdEvent.Id,
                    ICalUId = createdEvent.ICalUId,
                    Subject = createdEvent.Subject,
                    JoinUrl = createdEvent.OnlineMeeting?.JoinUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd tworzenia spotkania");
                return new MeetingCreationResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<bool> CancelMeetingAsync(string eventId, string myUserId)
        {
            try
            {
                var ev = await _graphClient.Me.Events[eventId].GetAsync(r =>
                {
                    r.QueryParameters.Select = new[] { "subject", "attendees" };
                });

                await _graphClient.Me.Events[eventId].Cancel.PostAsync(new Microsoft.Graph.Me.Events.Item.Cancel.CancelPostRequestBody
                {
                    Comment = "Spotkanie anulowane przez MetaMeet."
                });

                var dbMeeting = await _db.Meetings.FirstOrDefaultAsync(m => m.GraphEventId == eventId);
                if (dbMeeting != null)
                {
                    dbMeeting.IsCancelled = true;
                    if (string.IsNullOrEmpty(dbMeeting.AttendeesJson) && ev?.Attendees != null)
                    {
                        var list = ev.Attendees.Select(a => new AttendeeInfo
                        {
                            Name = a.EmailAddress?.Name,
                            Email = a.EmailAddress?.Address
                        }).ToList();
                        dbMeeting.AttendeesJson = JsonSerializer.Serialize(list);
                    }
                }

                if (ev?.Attendees != null)
                {
                    var me = await _db.AppUsers.FirstOrDefaultAsync(u => u.AzureAdUserId == myUserId);
                    var senderName = me?.DisplayName ?? "Organizator";

                    foreach (var attendee in ev.Attendees)
                    {
                        var email = attendee.EmailAddress?.Address;
                        if (string.IsNullOrEmpty(email)) continue;

                        var recipientUser = await _db.AppUsers.FirstOrDefaultAsync(u => u.UserPrincipalName.ToLower() == email.ToLower());

                        if (recipientUser != null)
                        {
                            var notif = new Notification
                            {
                                RecipientId = recipientUser.Id,
                                SenderId = me?.Id,
                                Type = "MeetingCancelled",
                                Message = $"Użytkownik {senderName} anulował spotkanie: {ev?.Subject}",
                                CreatedAt = DateTime.UtcNow,
                                IsRead = false,
                                IsHandled = false
                            };
                            _db.Notifications.Add(notif);
                        }
                    }
                }

                await _db.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd anulowania");
                var dbMeeting = await _db.Meetings.FirstOrDefaultAsync(m => m.GraphEventId == eventId);
                if (dbMeeting != null)
                {
                    dbMeeting.IsCancelled = true;
                    await _db.SaveChangesAsync();
                    return true;
                }
                return false;
            }
        }

        public async Task<(bool success, string error)> DeclineMeetingAsync(string icalUid, string myUserId)
        {
            try
            {
                var events = await _graphClient.Me.Events.GetAsync(r =>
                {
                    r.QueryParameters.Filter = $"iCalUId eq '{icalUid}'";
                    r.QueryParameters.Select = new[] { "id", "subject", "organizer" };
                });

                var ev = events?.Value?.FirstOrDefault();
                if (ev == null) return (false, "Brak spotkania");

                await _graphClient.Me.Events[ev.Id].Decline.PostAsync(new Microsoft.Graph.Me.Events.Item.Decline.DeclinePostRequestBody { SendResponse = true });

                var organizerEmail = ev.Organizer?.EmailAddress?.Address;
                if (!string.IsNullOrEmpty(organizerEmail))
                {
                    var organizerUser = await _db.AppUsers.FirstOrDefaultAsync(u => u.UserPrincipalName == organizerEmail);
                    var me = await _db.AppUsers.FirstOrDefaultAsync(u => u.AzureAdUserId == myUserId);

                    if (organizerUser != null)
                    {
                        var notif = new Notification
                        {
                            RecipientId = organizerUser.Id,
                            SenderId = me?.Id,
                            Type = "MeetingDeclined",
                            Message = $"Użytkownik {me?.DisplayName ?? "Ktoś"} odrzucił zaproszenie na: {ev.Subject}",
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false,
                            IsHandled = false
                        };
                        _db.Notifications.Add(notif);
                        await _db.SaveChangesAsync();
                    }
                }
                return (true, null);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<(bool success, string error)> AcceptMeetingAsync(string icalUid)
        {
            try
            {
                var events = await _graphClient.Me.Events.GetAsync(r => r.QueryParameters.Filter = $"iCalUId eq '{icalUid}'");
                var ev = events?.Value?.FirstOrDefault();
                if (ev == null) return (false, "Brak spotkania");
                await _graphClient.Me.Events[ev.Id].Accept.PostAsync(new Microsoft.Graph.Me.Events.Item.Accept.AcceptPostRequestBody { SendResponse = true });
                return (true, null);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public class MeetingCreationResult
        {
            public bool Success { get; set; }
            public string? EventId { get; set; }
            public string? Subject { get; set; }
            public string? JoinUrl { get; set; }
            public string? StartTime { get; set; }
            public string? EndTime { get; set; }
            public string? Error { get; set; }
            public string? Details { get; set; }
            public string ICalUId { get; set; }
        }
        public class MeetingListResult
        {
            public List<MeetingInfo> Organized { get; set; } = new();
            public List<MeetingInfo> Invited { get; set; } = new();
            public List<MeetingInfo> Rejected { get; set; } = new();
            public List<MeetingInfo> Cancelled { get; set; } = new();
            public string CurrentUserEmail { get; set; } = "";
        }
        public class MeetingInfo
        {
            public string Id { get; set; } = "";
            public string Subject { get; set; } = "";
            public string Organizer { get; set; } = "";
            public string OrganizerEmail { get; set; } = "";
            public List<AttendeeInfo> Attendees { get; set; } = new();
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public string JoinUrl { get; set; } = "";
            public bool IsCancelled { get; set; }
            public bool IsOrganizer { get; set; }
            public string ResponseStatus { get; set; }
            public string ICalUId { get; set; }
        }
        public class AttendeeInfo { public string Name { get; set; } = ""; public string Email { get; set; } = ""; public string Response { get; set; } = ""; }
    }
}