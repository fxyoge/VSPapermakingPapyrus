using Xunit;

namespace PapermakingPapyrus.Tests;

public sealed class PapyrusDryingTests
{
    [Theory]
    [InlineData(0, 6, 24, 0.25)]
    [InlineData(0.5, 12, 24, 1)]
    [InlineData(0.75, 12, 24, 1.25)]
    [InlineData(1.25, 12, 24, 1.25)]
    [InlineData(0.5, -1, 24, 0.5)]
    public void DryingAdvanceIsNormalized(
        double start,
        double elapsed,
        double duration,
        double expected)
    {
        Assert.Equal(expected, PapyrusDrying.Advance(start, elapsed, duration), 8);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.249, 0)]
    [InlineData(0.25, 1)]
    [InlineData(0.5, 2)]
    [InlineData(0.75, 3)]
    [InlineData(1, 3)]
    [InlineData(1.25, 3)]
    public void VisualBandsAreStable(double progress, int expected)
    {
        Assert.Equal(expected, PapyrusDrying.VisualBand(progress));
    }
}
