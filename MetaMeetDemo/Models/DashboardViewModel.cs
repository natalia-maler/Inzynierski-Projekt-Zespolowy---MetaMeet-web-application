using System;
using System.Collections.Generic;
using MetaMeetDemo.Services;

namespace MetaMeetDemo.Models
{
    public class DashboardViewModel
    {
        public string UserName { get; set; }
        public string WelcomeMessage { get; set; }
        public string TodayDayName { get; set; }
        public int TodayDayNumber { get; set; }
        public string TodayMonthName { get; set; }

        public DateTime SelectedDate { get; set; }
        public string PrevDateStr { get; set; }
        public string NextDateStr { get; set; }
        public bool IsToday => SelectedDate.Date == DateTime.Now.Date;

        public List<EventViewModel> TodayEvents { get; set; } = new List<EventViewModel>();
        public List<EventViewModel> CalendarEvents { get; set; } = new List<EventViewModel>();

        public GraphUserService.UserTestResult LicenseInfo { get; set; }
    }

    public class EventViewModel
    {
        
        public string Subject { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string JoinUrl { get; set; }
        public bool IsPast => End < DateTime.Now;
        public double DurationMinutes => (End - Start).TotalMinutes;
        public double TopPosition => (Start.Hour * 100) + (Start.Minute * (100.0 / 60.0)) + 20;
        public double VisualHeight => DurationMinutes * (100.0 / 60.0);
        public int ColumnIndex { get; set; } = 0;
        public int TotalColumns { get; set; } = 1;
    }
}