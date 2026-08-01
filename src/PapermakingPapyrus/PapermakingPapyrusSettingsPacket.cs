using ProtoBuf;

namespace PapermakingPapyrus;

[ProtoContract]
public sealed class PapermakingPapyrusSettingsPacket
{
    [ProtoMember(1)]
    public float CuttingDurationSeconds { get; set; }

    public static PapermakingPapyrusSettingsPacket FromConfig(
        PapermakingPapyrusConfig config) =>
        new()
        {
            CuttingDurationSeconds = config.CuttingDurationSeconds
        };

    public PapermakingPapyrusClientSettings ToClientSettings() =>
        new(
            float.IsFinite(CuttingDurationSeconds)
                ? CuttingDurationSeconds
                : PapermakingPapyrusConfig.DefaultCuttingDurationSeconds);
}

public readonly record struct PapermakingPapyrusClientSettings(float CuttingDurationSeconds)
{
    public static PapermakingPapyrusClientSettings Default =>
        new(PapermakingPapyrusConfig.DefaultCuttingDurationSeconds);
}
