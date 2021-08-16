using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IRSI.Services.AudoUpdate.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Topshelf;
using Host = Microsoft.Extensions.Hosting.Host;

namespace IRSI.Services.AudoUpdate
{
    public class Program
    {
        private const string ServiceName = "IRSI.Services.Polling";

        public static void Main(string[] args)
        {
            HostFactory.Run(x =>
            {
                x.Service<AutoUpdateService>(s =>
                {
                    s.ConstructUsing(() => new AutoUpdateService(CreateHostBuilder(args).Build()));
                    s.WhenStarted(service => service.Start());
                    s.WhenStopped(service => service.Stop());
                });
                x.StartAutomatically();
                x.RunAsLocalService();

                x.SetServiceName(ServiceName);
                x.SetDisplayName(ServiceName);
                x.SetDescription("Service for auto updating other IRSI Services");

                x.DependsOnEventLog();
                x.BeforeInstall(() =>
                {
                    if (!EventLog.SourceExists(ServiceName))
                    {
                        EventLog.CreateEventSource(ServiceName, "Application");
                    }
                });
            });
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    var env = context.HostingEnvironment;
                    config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                    config.AddEnvironmentVariables();
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);
                    if (env.IsDevelopment())
                    {
                        config.AddUserSecrets<Program>();
                    }
                })
                .UseSerilog((context, configuration) =>
                {
                    if (context.HostingEnvironment.IsProduction())
                        configuration.MinimumLevel.Information();
                    else
                        configuration.MinimumLevel.Debug();

                    configuration.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        .Enrich.WithProperty("Application", ServiceName)
                        .Enrich.WithProperty("StoreId",
                            context.Configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>().StoreId)
                        .WriteTo.Console()
                        .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "autoupdate.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 5)
                        .WriteTo.EventLog(ServiceName, manageEventSource: true);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<ServiceSettings>(settings =>
                        hostContext.Configuration.GetSection(nameof(ServiceSettings)).Bind(settings));
                });
    }
}