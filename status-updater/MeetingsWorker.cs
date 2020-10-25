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

        public MeetingsWorker(StatusManager statusManager, IOptions<MeetingOptions> options)
        {
            _statusManager = statusManager;
            _options = options.Value;
            _httpClient = new HttpClient();
        }

        public async Task<Calendar> LoadFromUriAsync(Uri uri)
        {
            using var response = await _httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            return Calendar.Load(result);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var calendar = await LoadFromUriAsync(new Uri(_options.CalendarUri));
                    var result = calendar.GetFreeBusy(new CalDateTime(DateTime.UtcNow), new CalDateTime(DateTime.UtcNow.AddMinutes(5)));
                    var status = result.GetFreeBusyStatus(new CalDateTime(DateTime.UtcNow));
                    if (status == FreeBusyStatus.Busy || status == FreeBusyStatus.BusyTentative || status == FreeBusyStatus.BusyUnavailable)
                    {
                        await _statusManager.SetStatusAsync(":calendar:", "In a meeting", StatusType.Meeting);
                    }
                    else
                    {
                        await _statusManager.SetStatusCompleteAsync(StatusType.Meeting);
                    }

                    Thread.Sleep(TimeSpan.FromMinutes(5));
                }
            }, stoppingToken);
            
        }
    }
}
