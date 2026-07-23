namespace PapermakingPapyrus;

public sealed class PapermakingPapyrusConfig
{
    public float CuttingDurationSeconds { get; set; } = 1.5f;

    public int DryStripsPerPapyrusTop { get; set; } = PapyrusConstants.StripsPerTop;

    public void Validate()
    {
        if (!float.IsFinite(CuttingDurationSeconds) || CuttingDurationSeconds < 0.25f)
        {
            throw new InvalidOperationException("CuttingDurationSeconds must be finite and at least 0.25.");
        }

        if (DryStripsPerPapyrusTop is < 1 or > 64)
        {
            throw new InvalidOperationException("DryStripsPerPapyrusTop must be between 1 and 64.");
        }
    }
}

