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
                    services.AddHostedService<GPMDesktopPlayerWorker>();
                    services.AddHostedService<RetryWorker>();
                    services.AddHostedService<MeetingsWorker>();
                    services.AddHostedService<CallsWorker>();
                    services.Configure<GPMDesktopPlayerOptions>(configuration.GetSection("gpmDesktopPlayer"));
                    services.Configure<SlackOptions>(configuration.GetSection("slack"));
                    services.Configure<MeetingOptions>(configuration.GetSection("meetings"));
                    services.AddHttpClient<SlackService>();
                    services.AddSingleton<StatusManager>();
                }).UseWindowsService();
        }
            
    }
}
