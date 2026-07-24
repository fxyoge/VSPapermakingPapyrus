using Xunit;

namespace PapermakingPapyrus.Tests;

public sealed class CalendarProgressTests
{
    private static readonly CalendarProgressPolicy Policy = new(24, 3, 24 * 108);

    private struct RecordingSampler(Func<double, bool> isActive) : ICalendarActivitySampler
    {
        public readonly List<double> Samples = [];

        public bool IsActiveAt(double totalHours)
        {
            Samples.Add(totalHours);
            return isActive(totalHours);
        }
    }

    [Fact]
    public void OrdinaryDryingUsesEightCoarseSamples()
    {
        var samples = new List<double>();
        var sampler = new RecordingSampler(_ => true);

        var progress = CalendarProgress.Accumulate(
            0,
            100,
            124,
            Policy,
            ref sampler);

        Assert.Equal(1, progress);
        Assert.Equal(8, sampler.Samples.Count);
        Assert.Equal(101.5, sampler.Samples[0]);
        Assert.Equal(122.5, sampler.Samples[^1]);
    }

    [Fact]
    public void InactiveIntervalsPauseWithoutLosingProgress()
    {
        var sampler = new RecordingSampler(sample => sample > 12);
        var progress = CalendarProgress.Accumulate(
            0.25,
            0,
            24,
            Policy,
            ref sampler);

        Assert.Equal(0.75, progress, 8);
    }

    [Fact]
    public void PartialFinalIntervalIsCountedExactly()
    {
        var sampler = new RecordingSampler(_ => true);
        var progress = CalendarProgress.Accumulate(
            0,
            0,
            4,
            Policy,
            ref sampler);

        Assert.Equal(4d / 24, progress, 8);
    }

    [Fact]
    public void CatchUpIsCappedToTheMostRecentPolicyWindow()
    {
        var policy = new CalendarProgressPolicy(10000, 3, 12);
        var sampler = new RecordingSampler(_ => true);

        CalendarProgress.Accumulate(
            0,
            0,
            100,
            policy,
            ref sampler);

        Assert.Equal([89.5, 92.5, 95.5, 98.5], sampler.Samples);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(-1, 0)]
    [InlineData(2, 1)]
    public void ProgressIsNormalized(double initial, double expected)
    {
        var sampler = new RecordingSampler(_ => true);
        Assert.Equal(
            expected,
            CalendarProgress.Accumulate(initial, 0, 0, Policy, ref sampler));
    }
}
