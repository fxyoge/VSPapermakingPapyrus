using ProtoBuf;

namespace PapermakingPapyrus;

[ProtoContract]
public sealed class PapermakingPapyrusSettingsPacket
{
    [ProtoMember(1)]
    public float CuttingDurationSeconds { get; set; }

    [ProtoMember(2)]
    public double DryingHours { get; set; }

    public static PapermakingPapyrusSettingsPacket FromConfig(
        PapermakingPapyrusConfig config) =>
        new()
        {
            CuttingDurationSeconds = config.CuttingDurationSeconds,
            DryingHours = config.DryingHours
        };

    public PapermakingPapyrusClientSettings ToClientSettings() =>
        new(
            float.IsFinite(CuttingDurationSeconds)
                ? CuttingDurationSeconds
                : PapermakingPapyrusConfig.DefaultCuttingDurationSeconds,
            double.IsFinite(DryingHours)
                ? DryingHours
                : PapermakingPapyrusConfig.DefaultDryingHours);
}

public readonly record struct PapermakingPapyrusClientSettings(
    float CuttingDurationSeconds,
    double DryingHours)
{
    public static PapermakingPapyrusClientSettings Default =>
        new(
            PapermakingPapyrusConfig.DefaultCuttingDurationSeconds,
            PapermakingPapyrusConfig.DefaultDryingHours);
}
