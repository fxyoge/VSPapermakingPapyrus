namespace PapermakingPapyrus;

public sealed class PapermakingPapyrusConfig
{
    public const float DefaultCuttingDurationSeconds = 1.5f;
    public const float MinCuttingDurationSeconds = 0.25f;
    public const float MaxCuttingDurationSeconds = 60f;

    public const int DefaultDryStripsPerPapyrusTop = 2;
    public const int MinDryStripsPerPapyrusTop = 1;
    public const int MaxDryStripsPerPapyrusTop = 64;

    public float CuttingDurationSeconds { get; set; } = DefaultCuttingDurationSeconds;

    public int DryStripsPerPapyrusTop { get; set; } = DefaultDryStripsPerPapyrusTop;

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
    }
}
