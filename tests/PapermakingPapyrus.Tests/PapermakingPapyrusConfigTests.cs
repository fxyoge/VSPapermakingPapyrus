using Xunit;

namespace PapermakingPapyrus.Tests;

public sealed class PapermakingPapyrusConfigTests
{
    [Fact]
    public void DefaultsAreValid()
    {
        var config = new PapermakingPapyrusConfig();

        config.Sanitize();

        Assert.Equal(PapermakingPapyrusConfig.DefaultCuttingDurationSeconds, config.CuttingDurationSeconds);
        Assert.Equal(PapermakingPapyrusConfig.DefaultDryStripsPerPapyrusTop, config.DryStripsPerPapyrusTop);
        Assert.Equal(PapermakingPapyrusConfig.DefaultDryingHours, config.DryingHours);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonFiniteCuttingDurationUsesDefault(float value)
    {
        var config = new PapermakingPapyrusConfig { CuttingDurationSeconds = value };

        config.Sanitize();

        Assert.Equal(PapermakingPapyrusConfig.DefaultCuttingDurationSeconds, config.CuttingDurationSeconds);
    }

    [Theory]
    [InlineData(0, PapermakingPapyrusConfig.MinCuttingDurationSeconds)]
    [InlineData(PapermakingPapyrusConfig.MinCuttingDurationSeconds, PapermakingPapyrusConfig.MinCuttingDurationSeconds)]
    [InlineData(PapermakingPapyrusConfig.DefaultCuttingDurationSeconds, PapermakingPapyrusConfig.DefaultCuttingDurationSeconds)]
    [InlineData(PapermakingPapyrusConfig.MaxCuttingDurationSeconds, PapermakingPapyrusConfig.MaxCuttingDurationSeconds)]
    [InlineData(61, PapermakingPapyrusConfig.MaxCuttingDurationSeconds)]
    public void CuttingDurationIsClamped(float value, float expected)
    {
        var config = new PapermakingPapyrusConfig { CuttingDurationSeconds = value };

        config.Sanitize();

        Assert.Equal(expected, config.CuttingDurationSeconds);
    }

    [Theory]
    [InlineData(int.MinValue, PapermakingPapyrusConfig.MinDryStripsPerPapyrusTop)]
    [InlineData(0, PapermakingPapyrusConfig.MinDryStripsPerPapyrusTop)]
    [InlineData(PapermakingPapyrusConfig.MinDryStripsPerPapyrusTop, PapermakingPapyrusConfig.MinDryStripsPerPapyrusTop)]
    [InlineData(PapermakingPapyrusConfig.DefaultDryStripsPerPapyrusTop, PapermakingPapyrusConfig.DefaultDryStripsPerPapyrusTop)]
    [InlineData(PapermakingPapyrusConfig.MaxDryStripsPerPapyrusTop, PapermakingPapyrusConfig.MaxDryStripsPerPapyrusTop)]
    [InlineData(65, PapermakingPapyrusConfig.MaxDryStripsPerPapyrusTop)]
    [InlineData(int.MaxValue, PapermakingPapyrusConfig.MaxDryStripsPerPapyrusTop)]
    public void DryStripsPerPapyrusTopIsClamped(int value, int expected)
    {
        var config = new PapermakingPapyrusConfig { DryStripsPerPapyrusTop = value };

        config.Sanitize();

        Assert.Equal(expected, config.DryStripsPerPapyrusTop);
    }

    [Theory]
    [InlineData(double.NaN, PapermakingPapyrusConfig.DefaultDryingHours)]
    [InlineData(double.PositiveInfinity, PapermakingPapyrusConfig.DefaultDryingHours)]
    [InlineData(0, PapermakingPapyrusConfig.MinDryingHours)]
    [InlineData(24, 24)]
    [InlineData(10000, PapermakingPapyrusConfig.MaxDryingHours)]
    public void DryingHoursIsSanitized(double value, double expected)
    {
        var config = new PapermakingPapyrusConfig { DryingHours = value };

        config.Sanitize();

        Assert.Equal(expected, config.DryingHours);
    }

    [Fact]
    public void ClientSettingsPacketContainsOnlyPresentationRelevantGameplayValues()
    {
        var config = new PapermakingPapyrusConfig
        {
            CuttingDurationSeconds = 4.5f,
            DryingHours = 72,
            DryStripsPerPapyrusTop = 12
        };

        var packet = PapermakingPapyrusSettingsPacket.FromConfig(config);
        var settings = packet.ToClientSettings();

        Assert.Equal(4.5f, settings.CuttingDurationSeconds);
    }

    [Fact]
    public void InvalidReceivedSettingsFallBackToSafeValues()
    {
        var packet = new PapermakingPapyrusSettingsPacket
        {
            CuttingDurationSeconds = float.NaN
        };

        var settings = packet.ToClientSettings();

        Assert.Equal(
            PapermakingPapyrusConfig.DefaultCuttingDurationSeconds,
            settings.CuttingDurationSeconds);
    }
}
