namespace RdpRestarterService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly GlobalConfig _globalConfig;

        public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider, GlobalConfig globalConfig)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _globalConfig = globalConfig;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var client = scope.ServiceProvider.GetRequiredService<ClientWrapper>();
                            _logger.LogInformation($"Start work for bot {client.BotName}");

                            await Task.Delay(_globalConfig.ReCreateBotClientPeriod, stoppingToken);
                        }
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to do work");
                    if (stoppingToken.IsCancellationRequested)
                        break;
                }

                await Task.Delay(30_000);
            }
        }
    }
}
