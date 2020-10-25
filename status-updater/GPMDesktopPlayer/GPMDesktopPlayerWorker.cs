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
        private readonly StatusManager _statusManager;
        private readonly GPMDesktopPlayerOptions _options;

        private bool _isPlaying;
        private TrackPayload _track;
        private DateTime? _stateChanged;
        private DateTime? _lastNotificationSentToStatusManager;
        private readonly SemaphoreSlim _statusLock = new SemaphoreSlim(1);

        public GPMDesktopPlayerWorker(ILogger<GPMDesktopPlayerWorker> logger, IOptions<GPMDesktopPlayerOptions> options, StatusManager statusManager)
        {
            _logger = logger;
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
                    var firstRun = !_lastNotificationSentToStatusManager.HasValue && _track != null;
                    var stateChanged = _stateChanged.HasValue && _lastNotificationSentToStatusManager.HasValue && DateTime.UtcNow > _stateChanged.Value.AddSeconds(0.5) &&
                                       _stateChanged > _lastNotificationSentToStatusManager;
                    if (firstRun || stateChanged)
                    {
                        await UpdateStatusAsync();
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(0.1));
                }
            }, stoppingToken);

            await handler.ConnectAsync(stoppingToken);
            await handler.RunAsync(stoppingToken);
        }

        private async Task HandleWebSocketMessageAsync(GPMDesktopPlayerSocketHandler source, GPMDesktopPlayerData e)
        {
            await _statusLock.WaitAsync();
            try
            {
                switch (e.Payload)
                {
                    case TrackPayload trackPayload when e.Channel == "track":
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
                _statusLock.Release();
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

        private async Task UpdateStatusAsync()
        {
            await _statusLock.WaitAsync();
            try
            {
                var status = _isPlaying && _track != null ? GetStatus(_track.Title, _track.Artist) : null;
                var emoji = _isPlaying ? ":headphones:" : null;
                if (status != null)
                {
                    await _statusManager.SetStatusAsync(emoji, status, StatusType.Music);
                }
                else
                {
                    await _statusManager.SetStatusCompleteAsync(StatusType.Music);
                }
                _lastNotificationSentToStatusManager = DateTime.UtcNow;

            }
            finally
            {
                _statusLock.Release();
            }

        }
    }
}
