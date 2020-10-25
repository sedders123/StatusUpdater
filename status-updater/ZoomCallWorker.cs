using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace status_updater
{
    public class ZoomCallWorker : BackgroundService
    {
        private readonly StatusManager _statusManager;

        public ZoomCallWorker(StatusManager statusManager)
        {
            _statusManager = statusManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var zoomProcesses = Process.GetProcessesByName("Zoom");
                    var onZoomCall = zoomProcesses.Any(p => !string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowTitle == "Zoom Meeting");

                    if (onZoomCall)
                    {
                        await _statusManager.SetStatusAsync(":telephone_receiver:", "On a Zoom call", StatusType.Call);
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
