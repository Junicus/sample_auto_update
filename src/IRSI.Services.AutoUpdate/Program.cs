using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using Coravel;
using IRSI.Services.AutoUpdate.Common.Interfaces;
using IRSI.Services.AutoUpdate.HttpClients;
using IRSI.Services.AutoUpdate.Jobs;
using IRSI.Services.AutoUpdate.Options;
using IRSI.Services.AutoUpdate.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Topshelf;
using Host = Microsoft.Extensions.Hosting.Host;

namespace IRSI.Services.AutoUpdate
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
                    s.ConstructUsing(() => new(CreateHostBuilder(args).Build()));
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
                    if (!EventLog.SourceExists(ServiceName)) EventLog.CreateEventSource(ServiceName, "Application");
                });
            });
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    var env = context.HostingEnvironment;
                    config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                    config.AddEnvironmentVariables();
                    config.AddJsonFile("appsettings.json", false, true);
                    config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", true);
                    if (env.IsDevelopment()) config.AddUserSecrets<Program>();
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
                            context.Configuration.GetSection(nameof(StoreSettings)).Get<StoreSettings>().StoreId)
                        .WriteTo.Console()
                        .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "autoupdate.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 5)
                        .WriteTo.EventLog(ServiceName, manageEventSource: true);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<StoreSettings>(settings =>
                        hostContext.Configuration.GetSection(nameof(StoreSettings)).Bind(settings));

                    services.Configure<ServiceBusSettings>(settings =>
                        hostContext.Configuration.GetSection(nameof(ServiceBusSettings)).Bind(settings));

                    services.Configure<ServiceSettings>(settings =>
                        hostContext.Configuration.GetSection(nameof(ServiceSettings)).Bind(settings));

                    services.Configure<GitHubSettings>(settings =>
                        hostContext.Configuration.GetSection(nameof(GitHubSettings)).Bind(settings));

                    services.AddScheduler();
                    services.AddQueue();

                    services.AddTransient<IEnvironmentProxy, EnvironmentProxy>();
                    services.AddTransient<IProcessProxy, ProcessProxy>();
                    services.AddTransient<IFileSystem, FileSystem>();

                    services.AddHttpClient<IGitHubHttpClient, GitHubHttpClient>((provider, client) =>
                    {
                        var githubSettings = provider.GetRequiredService<IOptions<GitHubSettings>>();
                        client.BaseAddress = new("https://api.github.com");
                        client.DefaultRequestHeaders.Authorization =
                            new("token", githubSettings.Value.GitHubToken);
                        client.DefaultRequestHeaders.UserAgent.TryParseAdd("IRSI.Services.AutoUpdate");
                    });

                    services.AddTransient<CheckUpdatesJob>();
                    services.AddTransient<InstallServiceJob>();
                    services.AddTransient<UpdateServiceJob>();
                });
        }
    }
}