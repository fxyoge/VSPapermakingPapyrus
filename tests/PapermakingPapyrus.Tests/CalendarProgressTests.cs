using Xunit;

namespace PapermakingPapyrus.Tests;

public sealed class CalendarProgressTests
{
    private const int UnboundedForTest = 864;

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
            24,
            UnboundedForTest,
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
            24,
            UnboundedForTest,
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
            24,
            UnboundedForTest,
            ref sampler);

        Assert.Equal(4d / 24, progress, 8);
    }

    [Fact]
    public void ProgressMayOvershootAndThenStopsAccumulating()
    {
        var sampler = new RecordingSampler(_ => true);
        var progress = CalendarProgress.Accumulate(
            23.5 / 24,
            0,
            6,
            24,
            UnboundedForTest,
            ref sampler);

        Assert.Equal(26.5 / 24, progress, 8);
        Assert.Single(sampler.Samples);

        var completedProgress = CalendarProgress.Accumulate(
            progress,
            6,
            12,
            24,
            UnboundedForTest,
            ref sampler);

        Assert.Equal(progress, completedProgress);
        Assert.Single(sampler.Samples);
    }

    [Fact]
    public void CatchUpIsCappedToTheMostRecentPolicySamples()
    {
        var sampler = new RecordingSampler(_ => true);

        CalendarProgress.Accumulate(
            0,
            0,
            100,
            10000,
            4,
            ref sampler);

        Assert.Equal([89.5, 92.5, 95.5, 98.5], sampler.Samples);
    }

    [Fact]
    public void CatchUpNeverExceedsTheSampleCapWithAPartialInterval()
    {
        var sampler = new RecordingSampler(_ => true);

        CalendarProgress.Accumulate(
            0,
            0,
            100.5,
            10000,
            32,
            ref sampler);

        Assert.Equal(32, sampler.Samples.Count);
        Assert.Equal(6, sampler.Samples[0]);
        Assert.Equal(99, sampler.Samples[^1]);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(-1, 0)]
    [InlineData(2, 2)]
    public void ProgressIsNormalized(double initial, double expected)
    {
        var sampler = new RecordingSampler(_ => true);
        Assert.Equal(
            expected,
            CalendarProgress.Accumulate(
                initial,
                0,
                0,
                24,
                UnboundedForTest,
                ref sampler));
    }
}
