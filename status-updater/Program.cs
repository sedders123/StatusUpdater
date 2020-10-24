using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Configuration;
using status_updater.GPMDesktopPlayer;

namespace status_updater
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddCommandLine(args)
                .AddEnvironmentVariables()
                .Build();

            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(configApp => {
                    configApp.AddConfiguration(configuration);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<GPMDesktopPlayerWorker>().Configure<EventLogSettings>(config =>
                    {
                        config.LogName = "Slack Status Updater Service";
                        config.SourceName = "Slack Status Updater Service Source";
                    });
                    services.Configure<GPMDesktopPlayerOptions>(configuration.GetSection("gpmDesktopPlayer"));
                    services.Configure<SlackOptions>(configuration.GetSection("slack"));
                    services.AddHttpClient<SlackService>();
                }).UseWindowsService();
        }
            
    }
}
