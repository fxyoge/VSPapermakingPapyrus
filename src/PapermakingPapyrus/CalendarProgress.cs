namespace PapermakingPapyrus;

public interface ICalendarActivitySampler
{
    bool IsActiveAt(double totalHours);
}

public static class CalendarProgress
{
    private const double SampleIntervalHours = 3;

    public static double Accumulate<TSampler>(
        double progress,
        double fromTotalHours,
        double toTotalHours,
        double requiredActiveHours,
        int maxSamples,
        ref TSampler sampler)
        where TSampler : struct, ICalendarActivitySampler
    {
        progress = double.IsFinite(progress) ? Math.Max(progress, 0) : 0;
        if (progress >= 1 ||
            !double.IsFinite(fromTotalHours) ||
            !double.IsFinite(toTotalHours) ||
            toTotalHours <= fromTotalHours ||
            !double.IsFinite(requiredActiveHours) ||
            requiredActiveHours <= 0 ||
            maxSamples <= 0)
        {
            return progress;
        }

        var catchUpFrom = Math.Max(
            fromTotalHours,
            toTotalHours - SampleIntervalHours * maxSamples);
        for (var cursor = catchUpFrom;
             cursor < toTotalHours && progress < 1;
             cursor += SampleIntervalHours)
        {
            var interval = Math.Min(SampleIntervalHours, toTotalHours - cursor);
            if (sampler.IsActiveAt(cursor + interval / 2))
            {
                progress += interval / requiredActiveHours;
            }
        }

        return progress;
    }
}
