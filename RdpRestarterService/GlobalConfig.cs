namespace RdpRestarterService
{
    public class GlobalConfig
    {
        public GlobalConfig(IConfiguration configuration)
        {
            TgKey = configuration.GetValue<string>("TgKey")
                ?? throw new ArgumentOutOfRangeException("Environment variable TgKey is not set");

            AllowedChatIds = configuration.GetValue<string>("AllowedChatIds")
                ?.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?.Select(long.Parse)
                ?.ToHashSet()
                ?? [354272469];

            var reCreateBotClientPeriod = configuration.GetValue<string>("ReCreateBotClientPeriod");
            ReCreateBotClientPeriod = string.IsNullOrWhiteSpace(reCreateBotClientPeriod)
                ? TimeSpan.FromHours(2)
                : TimeSpan.Parse(reCreateBotClientPeriod);
        }

        public string TgKey { get; private init; } = default!;
        public IReadOnlySet<long> AllowedChatIds { get; private init; } = default!;
        public TimeSpan ReCreateBotClientPeriod { get; private init; }
    }
}
