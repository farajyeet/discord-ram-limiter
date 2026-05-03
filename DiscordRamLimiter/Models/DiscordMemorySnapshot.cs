namespace DiscordRamLimiter.Models;

public sealed record DiscordMemorySnapshot(
    long CurrentWorkingSetBytes,
    long InitialWorkingSetBytes,
    int ProcessCount,
    bool IsLimiterActive,
    DateTimeOffset CapturedAt);
