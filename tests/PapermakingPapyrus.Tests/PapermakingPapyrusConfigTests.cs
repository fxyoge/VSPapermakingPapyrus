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
}
