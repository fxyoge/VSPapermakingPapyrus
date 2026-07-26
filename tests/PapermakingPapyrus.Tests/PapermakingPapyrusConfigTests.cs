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
        Assert.Equal(
            PapermakingPapyrusConfig.DefaultDryingRefreshIntervalMilliseconds,
            config.DryingRefreshIntervalMilliseconds);
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

    [Theory]
    [InlineData(int.MinValue, PapermakingPapyrusConfig.MinDryingRefreshIntervalMilliseconds)]
    [InlineData(0, PapermakingPapyrusConfig.MinDryingRefreshIntervalMilliseconds)]
    [InlineData(
        PapermakingPapyrusConfig.DefaultDryingRefreshIntervalMilliseconds,
        PapermakingPapyrusConfig.DefaultDryingRefreshIntervalMilliseconds)]
    [InlineData(int.MaxValue, PapermakingPapyrusConfig.MaxDryingRefreshIntervalMilliseconds)]
    public void DryingRefreshIntervalIsClamped(int value, int expected)
    {
        var config = new PapermakingPapyrusConfig
        {
            DryingRefreshIntervalMilliseconds = value
        };

        config.Sanitize();

        Assert.Equal(expected, config.DryingRefreshIntervalMilliseconds);
    }
}
