using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace status_updater.GPMDesktopPlayer
{
    public class GPMDesktopPlayerWorker : BackgroundService
    {
        private readonly ILogger<GPMDesktopPlayerWorker> _logger;
        private readonly SlackService _slackService;
        private readonly StatusManager _statusManager;
        private readonly GPMDesktopPlayerOptions _options;

        private bool _isPlaying;
        private TrackPayload _track;
        private bool _lastNotificationFailed;
        private DateTime? _stateChanged;
        private DateTime? _lastNotificationSentToSlack;
        private readonly SemaphoreSlim _slackLock = new SemaphoreSlim(1);

        public GPMDesktopPlayerWorker(ILogger<GPMDesktopPlayerWorker> logger, IOptions<GPMDesktopPlayerOptions> options, SlackService slackService, StatusManager statusManager)
        {
            _logger = logger;
            _slackService = slackService;
            _statusManager = statusManager;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var handler = new GPMDesktopPlayerSocketHandler(new Uri("ws://localhost:5672"), _options.AuthCode);
            handler.EventOccurredAsync += HandleWebSocketMessageAsync;

#pragma warning disable 4014
            Task.Run(async () => {
#pragma warning restore 4014
                while (!stoppingToken.IsCancellationRequested)
                {
                    var firstRun = !_statusManager.LastNotificationSentToSlack.HasValue && _track != null;
                    var lastNotificationFailed = _statusManager.LastNotificationToSlackFailed && _statusManager.LastNotificationSentToSlack.HasValue &&
                                                 DateTime.UtcNow > _statusManager.LastNotificationSentToSlack.Value.AddSeconds(5);
                    var stateChanged = _stateChanged.HasValue && _statusManager.LastNotificationSentToSlack.HasValue && DateTime.UtcNow > _stateChanged.Value.AddSeconds(0.5) &&
                                       _stateChanged > _statusManager.LastNotificationSentToSlack;
                    if (firstRun || lastNotificationFailed || stateChanged)
                    {
                        await UpdateSlackStatus();
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(0.1));
                }
            }, stoppingToken);

            await handler.ConnectAsync(stoppingToken);
            await handler.RunAsync(stoppingToken);
        }

        private async Task HandleWebSocketMessageAsync(GPMDesktopPlayerSocketHandler source, GPMDesktopPlayerData e)
        {
            await _slackLock.WaitAsync();
            try
            {
                switch (e.Payload)
                {
                    case TrackPayload trackPayload when e.Channel == "track":
                        _logger.LogInformation(e.Channel);
                        _track = trackPayload;
                        _stateChanged = DateTime.UtcNow;
                        break;
                    case bool isPlaying when e.Channel == "playState":
                        _isPlaying = isPlaying;
                        _stateChanged = DateTime.UtcNow;
                        break;
                }
            }
            finally
            {
                _slackLock.Release();
            }
        }

        private static string GetStatus(string title, string artist)
        {
            const string separator = " - ";
            if (title.Length + artist.Length + separator.Length <= 100)
            {
                return $"{title}{separator}{artist}";
            }

            if (title.Length > 45 && artist.Length > 45)
            {
                return $"{title.Substring(0, 45)}...{separator}{artist.Substring(0, 45)}...";
            }

            if (title.Length > 45 && artist.Length < 45)
            {
                var extraChars = 45 - artist.Length;
                return $"{title.Substring(0, 45 + extraChars)}...{separator}{artist}";
            }

            if (artist.Length > 45 && title.Length < 45)
            {
                var extraChars = 45 - title.Length;
                return $"{title}{separator}{artist.Substring(0, 45 + extraChars)}...";
            }

            return "Something's gone wrong";
        }

        private async Task UpdateSlackStatus()
        {
            await _slackLock.WaitAsync();
            try
            {
                var status = _isPlaying && _track != null ? GetStatus(_track.Title, _track.Artist) : null;
                var emoji = _isPlaying ? ":headphones:" : null;
                bool notified;
                if (status != null)
                {
                    notified = await _statusManager.SetStatusAsync(emoji, status, StatusType.Music);
                }
                else
                {
                    notified = await _statusManager.SetStatusCompleteAsync(StatusType.Music);
                }
                _lastNotificationFailed = !notified;
                _lastNotificationSentToSlack = DateTime.UtcNow;

            }
            finally
            {
                _slackLock.Release();
            }

        }
    }
}
