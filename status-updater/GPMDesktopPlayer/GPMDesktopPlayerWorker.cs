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
        private readonly GPMDesktopPlayerOptions _options;

        private bool _isPlaying;
        private TrackPayload _track;
        private bool _lastNotificationFailed;
        private DateTime? _stateChanged;
        private DateTime? _lastNotificationSentToSlack;
        private readonly SemaphoreSlim _slackLock = new SemaphoreSlim(1);

        public GPMDesktopPlayerWorker(ILogger<GPMDesktopPlayerWorker> logger, IOptions<GPMDesktopPlayerOptions> options, SlackService slackService)
        {
            _logger = logger;
            _slackService = slackService;
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
                    var firstRun = !_lastNotificationSentToSlack.HasValue && _track != null;
                    var lastNotificationFailed = _lastNotificationFailed && _lastNotificationSentToSlack.HasValue &&
                                                 DateTime.UtcNow > _lastNotificationSentToSlack.Value.AddSeconds(5);
                    var stateChanged = _stateChanged.HasValue && _lastNotificationSentToSlack.HasValue && DateTime.UtcNow > _stateChanged.Value.AddSeconds(0.5) &&
                                       _stateChanged > _lastNotificationSentToSlack;
                    if (firstRun || lastNotificationFailed || stateChanged)
                    {
                        await UpdateSlackStatus();
                    }
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

        private async Task UpdateSlackStatus()
        {
            await _slackLock.WaitAsync();
            try
            {
                var status = _isPlaying && _track != null ? $"{_track.Title} - {_track.Artist}" : null;
                var emoji = _isPlaying ? ":headphones:" : null;
                var notified = await _slackService.SetUserStatusAsync(emoji, status);

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
