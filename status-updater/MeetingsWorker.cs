using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ical.Net;
using Ical.Net.DataTypes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace status_updater
{
    public class MeetingOptions
    {
        public string CalendarUri { get; set; }
    }

    public class MeetingsWorker : BackgroundService
    {
        private readonly StatusManager _statusManager;
        private readonly MeetingOptions _options;
        private readonly HttpClient _httpClient;
        private Calendar _calendar;
        private DateTime _nextCalendarUpdate = DateTime.UtcNow;

        public MeetingsWorker(StatusManager statusManager, IOptions<MeetingOptions> options)
        {
            _statusManager = statusManager;
            _options = options.Value;
            _httpClient = new HttpClient();
        }

        private async Task<Calendar> LoadFromUriAsync(Uri uri)
        {
            using var response = await _httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            return Calendar.Load(result);
        }

        private async Task<Calendar> GetCalendarAsync(Uri uri)
        {
            if (_calendar == null || DateTime.UtcNow >= _nextCalendarUpdate)
            {
                _calendar = await LoadFromUriAsync(uri);
                _nextCalendarUpdate = DateTime.UtcNow.AddMinutes(30);
            }

            return _calendar;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var calendar = await GetCalendarAsync(new Uri(_options.CalendarUri));
                    var result = calendar.GetFreeBusy(new CalDateTime(DateTime.UtcNow.Date), new CalDateTime(DateTime.UtcNow.Date.AddDays(1)));
                    var status = result.GetFreeBusyStatus(new CalDateTime(DateTime.UtcNow));
                    if (status == FreeBusyStatus.Busy || status == FreeBusyStatus.BusyTentative || status == FreeBusyStatus.BusyUnavailable)
                    {
                        await _statusManager.SetStatusAsync(":calendar:", "In a meeting", StatusType.Meeting);
                    }
                    else
                    {
                        await _statusManager.SetStatusCompleteAsync(StatusType.Meeting);
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }, stoppingToken);
            
        }
    }
}
