namespace PapermakingPapyrus;

public sealed class PapermakingPapyrusConfig
{
    public const float DefaultCuttingDurationSeconds = 1.5f;
    public const float MinCuttingDurationSeconds = 0.25f;
    public const float MaxCuttingDurationSeconds = 60f;

    public const int DefaultDryStripsPerPapyrusTop = 2;
    public const int MinDryStripsPerPapyrusTop = 1;
    public const int MaxDryStripsPerPapyrusTop = 64;
    public const double DefaultDryingHours = 24;
    public const double MinDryingHours = 0.1;
    public const double MaxDryingHours = 24 * 365;
    public const int DefaultDryingRefreshIntervalMilliseconds = 10_000;
    public const int MinDryingRefreshIntervalMilliseconds = 1_000;
    public const int MaxDryingRefreshIntervalMilliseconds = 3_600_000;

    public float CuttingDurationSeconds { get; set; } = DefaultCuttingDurationSeconds;

    public int DryStripsPerPapyrusTop { get; set; } = DefaultDryStripsPerPapyrusTop;

    public double DryingHours { get; set; } = DefaultDryingHours;

    public int DryingRefreshIntervalMilliseconds { get; set; } =
        DefaultDryingRefreshIntervalMilliseconds;

    public void Sanitize()
    {
        var originalCuttingDurationSeconds = CuttingDurationSeconds;
        if (!float.IsFinite(CuttingDurationSeconds))
        {
            CuttingDurationSeconds = DefaultCuttingDurationSeconds;
            PapermakingPapyrusModSystem.Logger?.Warning(
                "Config value {0} must be a finite number; received {1}. Using the default value {2}.",
                nameof(CuttingDurationSeconds),
                originalCuttingDurationSeconds,
                CuttingDurationSeconds);
        }
        else
        {
            CuttingDurationSeconds = Math.Clamp(
                CuttingDurationSeconds,
                MinCuttingDurationSeconds,
                MaxCuttingDurationSeconds);

            if (!originalCuttingDurationSeconds.Equals(CuttingDurationSeconds))
            {
                PapermakingPapyrusModSystem.Logger?.Warning(
                    "Config value {0} must be between {1} and {2}; received {3}. Using {4}.",
                    nameof(CuttingDurationSeconds),
                    MinCuttingDurationSeconds,
                    MaxCuttingDurationSeconds,
                    originalCuttingDurationSeconds,
                    CuttingDurationSeconds);
            }
        }

        var originalDryStripsPerPapyrusTop = DryStripsPerPapyrusTop;
        DryStripsPerPapyrusTop = Math.Clamp(
            DryStripsPerPapyrusTop,
            MinDryStripsPerPapyrusTop,
            MaxDryStripsPerPapyrusTop);

        if (originalDryStripsPerPapyrusTop != DryStripsPerPapyrusTop)
        {
            PapermakingPapyrusModSystem.Logger?.Warning(
                "Config value {0} must be between {1} and {2}; received {3}. Using {4}.",
                nameof(DryStripsPerPapyrusTop),
                MinDryStripsPerPapyrusTop,
                MaxDryStripsPerPapyrusTop,
                originalDryStripsPerPapyrusTop,
                DryStripsPerPapyrusTop);
        }

        var originalDryingHours = DryingHours;
        DryingHours = double.IsFinite(DryingHours)
            ? Math.Clamp(DryingHours, MinDryingHours, MaxDryingHours)
            : DefaultDryingHours;
        if (!originalDryingHours.Equals(DryingHours))
        {
            PapermakingPapyrusModSystem.Logger?.Warning(
                "Config value {0} must be finite and between {1} and {2}; received {3}. Using {4}.",
                nameof(DryingHours),
                MinDryingHours,
                MaxDryingHours,
                originalDryingHours,
                DryingHours);
        }

        var originalDryingRefreshIntervalMilliseconds = DryingRefreshIntervalMilliseconds;
        DryingRefreshIntervalMilliseconds = Math.Clamp(
            DryingRefreshIntervalMilliseconds,
            MinDryingRefreshIntervalMilliseconds,
            MaxDryingRefreshIntervalMilliseconds);
        if (originalDryingRefreshIntervalMilliseconds != DryingRefreshIntervalMilliseconds)
        {
            PapermakingPapyrusModSystem.Logger?.Warning(
                "Config value {0} must be between {1} and {2}; received {3}. Using {4}.",
                nameof(DryingRefreshIntervalMilliseconds),
                MinDryingRefreshIntervalMilliseconds,
                MaxDryingRefreshIntervalMilliseconds,
                originalDryingRefreshIntervalMilliseconds,
                DryingRefreshIntervalMilliseconds);
        }
    }
}
