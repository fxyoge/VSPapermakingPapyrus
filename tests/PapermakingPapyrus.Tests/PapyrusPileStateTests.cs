using Xunit;

namespace PapermakingPapyrus.Tests;

public sealed class PapyrusPileStateTests
{
    [Theory]
    [InlineData(0, 0, false, PapyrusPileWorkState.Laying)]
    [InlineData(8, 0, false, PapyrusPileWorkState.Laying)]
    [InlineData(8, 1, false, PapyrusPileWorkState.Boarded)]
    [InlineData(8, 2, false, PapyrusPileWorkState.Boarded)]
    [InlineData(8, 2, true, PapyrusPileWorkState.Pressing)]
    public void ValidSnapshotsDeriveTheirState(
        int strips,
        int boards,
        bool weight,
        PapyrusPileWorkState expected)
    {
        var snapshot = new PapyrusPileSnapshot(strips, boards, weight, expected);

        Assert.True(snapshot.IsValid);
        Assert.Equal(expected, PapyrusPileSnapshot.DeriveState(strips, boards, weight));
    }

    [Theory]
    [InlineData(7, 1, false)]
    [InlineData(8, 3, false)]
    [InlineData(8, 1, true)]
    [InlineData(-1, 0, false)]
    [InlineData(9, 0, false)]
    public void StructurallyImpossibleSnapshotsAreInvalid(int strips, int boards, bool weight)
    {
        var state = PapyrusPileSnapshot.DeriveState(strips, boards, weight);
        Assert.False(new PapyrusPileSnapshot(strips, boards, weight, state).IsValid);
    }

    [Fact]
    public void ConstructionOrderAndReverseRemovalAreDeterministic()
    {
        Assert.Equal(
            PapyrusPileAction.AddStrip,
            PapyrusPileSnapshot.Empty.NextAction(false, true, false, false));
        Assert.Equal(
            PapyrusPileAction.None,
            PapyrusPileSnapshot.Empty.NextAction(false, false, true, false));

        var laid = new PapyrusPileSnapshot(8, 0, false, PapyrusPileWorkState.Laying);
        Assert.Equal(PapyrusPileAction.AddBoard, laid.NextAction(false, false, true, false));
        Assert.Equal(PapyrusPileAction.RemoveStrip, laid.NextAction(true, false, false, false));

        var boarded = new PapyrusPileSnapshot(8, 2, false, PapyrusPileWorkState.Boarded);
        Assert.Equal(PapyrusPileAction.AddWeight, boarded.NextAction(false, false, false, true));
        Assert.Equal(PapyrusPileAction.RemoveBoard, boarded.NextAction(true, false, false, false));
    }

    [Fact]
    public void CommittedPileRejectsEveryInteraction()
    {
        var pressing = new PapyrusPileSnapshot(8, 2, true, PapyrusPileWorkState.Pressing);

        Assert.True(pressing.IsCommitted);
        Assert.Equal(PapyrusPileAction.None, pressing.NextAction(true, false, false, false));
        Assert.Equal(PapyrusPileAction.None, pressing.NextAction(false, true, false, false));
        Assert.Equal(PapyrusPileAction.None, pressing.NextAction(false, false, true, false));
        Assert.Equal(PapyrusPileAction.None, pressing.NextAction(false, false, false, true));
    }

    [Theory]
    [InlineData(0, 6, 24, false, 0.25)]
    [InlineData(0.5, 12, 24, false, 1)]
    [InlineData(0.75, 12, 24, false, 1.25)]
    [InlineData(1.25, 12, 24, false, 1.25)]
    [InlineData(0.5, 12, 24, true, 0.5)]
    [InlineData(0.5, -1, 24, false, 0.5)]
    public void DryingAdvanceIsNormalizedAndFreezeAware(
        double start,
        double elapsed,
        double duration,
        bool freezing,
        double expected)
    {
        Assert.Equal(expected, PapyrusDrying.Advance(start, elapsed, duration, freezing), 8);
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

    [Fact]
    public void DrySnapshotIsValidAndRemainsCommitted()
    {
        var dry = new PapyrusPileSnapshot(8, 2, true, PapyrusPileWorkState.Dry);

        Assert.True(dry.IsValid);
        Assert.True(dry.IsCommitted);
        Assert.Equal(PapyrusPileAction.None, dry.NextAction(true, false, false, false));
    }
}
