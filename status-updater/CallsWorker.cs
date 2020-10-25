using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace status_updater
{
    public class CallsWorker : BackgroundService
    {
        private readonly StatusManager _statusManager;

        /// <summary>
        /// This is quite the hack, but seems to work.
        /// Teams has 7 processes normally but when screen sharing it has 9. Because Teams changes the MainWindowTitle if the
        /// Call window is not focused this method allows us to check if we're screen sharing. If there are internal changes
        /// to the Teams client this will break, this will likely not work cross platform.
        /// </summary>
        private const int NormalNumberOfTeamsProcesses = 7;


        public CallsWorker(StatusManager statusManager)
        {
            _statusManager = statusManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var teamsProcesses = Process.GetProcessesByName("Teams");
                    var zoomProcesses = Process.GetProcessesByName("Zoom");
                    var onZoomCall = zoomProcesses.Any(p => !string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowTitle == "Zoom Meeting");
                    var onTeamsCall = teamsProcesses.Any(p => !string.IsNullOrEmpty(p.MainWindowTitle) && (p.MainWindowTitle.StartsWith("New Window |") || p.MainWindowTitle.StartsWith("Microsoft Teams Call in progress"))) || teamsProcesses.Length > NormalNumberOfTeamsProcesses;

                    if (onZoomCall || onTeamsCall)
                    {
                        await _statusManager.SetStatusAsync(":telephone_receiver:", onZoomCall ? "On a Zoom call" : "On a Teams call", StatusType.Call);
                    }
                    else
                    {
                        await _statusManager.SetStatusCompleteAsync(StatusType.Call);
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }, stoppingToken);
        }
    }
}
