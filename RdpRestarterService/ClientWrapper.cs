using System.Diagnostics;
using System.ServiceProcess;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace RdpRestarterService
{
    public class ClientWrapper : IDisposable
    {
        private readonly ILogger<ClientWrapper> _logger;
        private readonly GlobalConfig _config;
        private readonly TelegramBotClient _client;
        private readonly CancellationTokenSource _cts;

        public string BotName { get; private init; }

        public ClientWrapper(ILogger<ClientWrapper> logger, GlobalConfig config)
        {
            _logger = logger;
            _config = config;
            _cts = new();

            while (true)
            {
                try
                {
                    _client = new(_config.TgKey);

                    Task.Run(async () => await _client.TestApiAsync()).Wait();

                    BotName = Task.Run(async () => await _client.GetMyNameAsync()).Result.Name;
                    _logger.LogInformation($"Start to getting messages from bot {BotName}. Allowed chat ids: [{string.Join(",", _config.AllowedChatIds)}]");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create tg client");
                }
                Thread.Sleep(5000);
            }

            while (!_cts.IsCancellationRequested)
            {
                Task.Run(async () =>
                    await _client.ReceiveAsync(
                        updateHandler: HandleUpdateAsync,
                        pollingErrorHandler: HandlePollingErrorAsync,
                        cancellationToken: _cts.Token))
                    .Wait();
            }
        }

        private async Task HandleUpdateAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken cancellationToken)
        {
            var chatId = update.Message?.Chat?.Id;

            _logger.LogInformation($"Get request from {chatId}.");

            if (chatId is null)
                return;
            if (!_config.AllowedChatIds.Contains(chatId.Value))
                return;

            if (update.Message?.Text?.Contains("reboot", StringComparison.InvariantCultureIgnoreCase) == true)
                await RebootPc(telegramBotClient, chatId.Value, cancellationToken);
            else
                await RestartRdp(telegramBotClient, chatId.Value, ct: cancellationToken);

            await telegramBotClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Done.",
                cancellationToken: cancellationToken);

            _logger.LogInformation($"Sent ip to {chatId}.");
        }

        private async ValueTask RestartRdp(ITelegramBotClient telegramBotClient, long chatId, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            await telegramBotClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Starting to restart rdp.",
                cancellationToken: ct);

            using var service = new ServiceController("TermService");
            if (timeout == null)
                timeout = TimeSpan.FromMinutes(3);

            using var cts = new CancellationTokenSource();

            var rebootTask = Task.Run(async () =>
            {
                await Task.Delay(timeout.Value, cts.Token);

                if (cts.IsCancellationRequested)
                    return;

                await RebootPc(telegramBotClient, chatId, cts.Token);
            }, cts.Token);

            if (service.Status != ServiceControllerStatus.Stopped)
            {
                while (true)
                {
                    try
                    {
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to stop rdp service");
                    }
                }
            }

            while (true)
            {
                try
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start rdp service");
                }
            }

            cts.Cancel();
        }

        private async ValueTask RebootPc(ITelegramBotClient telegramBotClient, long chatId, CancellationToken ct)
        {
            await telegramBotClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Starting to reboot pc.",
                cancellationToken: ct);

            Process.Start("shutdown.exe", "-r -f -t 0");
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            _logger.LogError(exception, errorMessage);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }

}
