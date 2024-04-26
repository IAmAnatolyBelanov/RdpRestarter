using Newtonsoft.Json;
using RdpRestarterService;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args);

        builder.UseWindowsService(config => config.ServiceName = "RdpRestarterV3");

        builder.ConfigureAppConfiguration(confBuilder =>
        {
            var conf = confBuilder
                .AddEnvironmentVariables()
                .AddEnvironmentVariables("TG_RDPRESTARTER_")
                .Build();
        });

        builder.ConfigureServices(ConfigureServices);

        var host = builder.Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var globalConfig = host.Services.GetRequiredService<GlobalConfig>();
        var conf = host.Services.GetRequiredService<IConfiguration>();
        logger.LogInformation($"{{\"GlobalConfig\":{JsonConvert.SerializeObject(globalConfig)}}}");
        logger.LogInformation($"{{\"OriginalFullConf\":{JsonConvert.SerializeObject(((IConfigurationRoot)conf).GetDebugView())}}}");

        host.Run();
    }

    public static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton<GlobalConfig>();
        services.AddTransient<ClientWrapper>();
        services.AddHostedService<Worker>();
    }
}