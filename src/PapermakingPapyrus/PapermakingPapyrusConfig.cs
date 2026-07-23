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
        if (!float.IsFinite(CuttingDurationSeconds))
        {
            CuttingDurationSeconds = DefaultCuttingDurationSeconds;
        }
        else
        {
            CuttingDurationSeconds = Math.Clamp(
                CuttingDurationSeconds,
                MinCuttingDurationSeconds,
                MaxCuttingDurationSeconds);
        }

        DryStripsPerPapyrusTop = Math.Clamp(
            DryStripsPerPapyrusTop,
            MinDryStripsPerPapyrusTop,
            MaxDryStripsPerPapyrusTop);
    }
}
