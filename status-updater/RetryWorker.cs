using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace status_updater
{
    public class RetryWorker : BackgroundService
    {
        private readonly StatusManager _statusManager;

        public RetryWorker(StatusManager statusManager)
        {
            _statusManager = statusManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_statusManager.LastNotificationToSlackFailed && _statusManager.LastNotificationSentToSlack.HasValue &&
                    DateTime.UtcNow > _statusManager.LastNotificationSentToSlack.Value.AddSeconds(5))
                {
                    await _statusManager.SyncStatus();
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }
    }
}
