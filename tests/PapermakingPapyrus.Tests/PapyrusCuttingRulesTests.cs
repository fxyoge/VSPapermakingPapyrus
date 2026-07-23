using Xunit;

namespace PapermakingPapyrus.Tests;

public sealed class PapyrusCuttingRulesTests
{
    [Theory]
    [InlineData(1.49f, 1.5f, false)]
    [InlineData(1.5f, 1.5f, true)]
    [InlineData(2f, 1.5f, true)]
    [InlineData(float.NaN, 1.5f, false)]
    public void CompletionRequiresFullFiniteDuration(float elapsed, float required, bool expected)
    {
        Assert.Equal(expected, PapyrusCuttingRules.HasCompleted(elapsed, required));
    }

    [Fact]
    public void OneTopProducesTwoStrips()
    {
        Assert.Equal(2, PapyrusCuttingRules.ProducedQuantity(1, 2));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    public void ProductionRejectsNonPositiveInputs(int tops, int strips)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PapyrusCuttingRules.ProducedQuantity(tops, strips));
    }
}

