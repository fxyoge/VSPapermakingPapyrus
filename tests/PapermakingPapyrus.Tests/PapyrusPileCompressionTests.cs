using Xunit;

namespace PapermakingPapyrus.Tests;

public sealed class PapyrusPileCompressionTests
{
    [Theory]
    [InlineData(0, 0.04375, 1)]
    [InlineData(0, 0.04375, 0.82)]
    [InlineData(0.25, 0.75, 0.5)]
    [InlineData(-0.5, 1.5, 0.94)]
    public void PressFollowsTheTransformedStripTop(
        float bottom,
        float top,
        float scale)
    {
        var transform = PapyrusPileCompression.Calculate(bottom, top, scale);
        var transformedTop =
            transform.ScaleOriginY +
            (top - transform.ScaleOriginY) * transform.ScaleY;

        Assert.Equal(transformedTop, top + transform.PressOffsetY, 6);
    }

    [Fact]
    public void CompressionUsesTheActualStripHeight()
    {
        var shortPile = PapyrusPileCompression.Calculate(0.1f, 0.5f, 0.75f);
        var tallPile = PapyrusPileCompression.Calculate(0.1f, 0.9f, 0.75f);

        Assert.Equal(-0.1f, shortPile.PressOffsetY, 6);
        Assert.Equal(-0.2f, tallPile.PressOffsetY, 6);
        Assert.Equal(0.1f, shortPile.ScaleOriginY);
        Assert.Equal(0.1f, tallPile.ScaleOriginY);
    }

    [Theory]
    [InlineData(float.NaN, 1, 0.82)]
    [InlineData(0, float.PositiveInfinity, 0.82)]
    [InlineData(1, 1, 0.82)]
    [InlineData(2, 1, 0.82)]
    [InlineData(0, 1, float.NaN)]
    [InlineData(0, 1, 0)]
    public void InvalidGeometryOrScaleFallsBackToNoCompression(
        float bottom,
        float top,
        float scale)
    {
        Assert.Equal(
            new PapyrusPileCompressionTransform(0, 1, 0),
            PapyrusPileCompression.Calculate(bottom, top, scale));
    }
}
