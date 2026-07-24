namespace PapermakingPapyrus;

public readonly record struct CalendarProgressPolicy(
    double RequiredActiveHours,
    double SampleIntervalHours,
    double MaxCatchUpHours)
{
    public bool IsValid =>
        double.IsFinite(RequiredActiveHours) && RequiredActiveHours > 0 &&
        double.IsFinite(SampleIntervalHours) && SampleIntervalHours > 0 &&
        double.IsFinite(MaxCatchUpHours) && MaxCatchUpHours > 0;
}

public interface ICalendarActivitySampler
{
    bool IsActiveAt(double totalHours);
}

public static class CalendarProgress
{
    public static double Accumulate<TSampler>(
        double progress,
        double fromTotalHours,
        double toTotalHours,
        CalendarProgressPolicy policy,
        ref TSampler sampler)
        where TSampler : struct, ICalendarActivitySampler
    {
        progress = double.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 0;
        if (progress >= 1 ||
            !double.IsFinite(fromTotalHours) ||
            !double.IsFinite(toTotalHours) ||
            toTotalHours <= fromTotalHours ||
            !policy.IsValid)
        {
            return progress;
        }

        var catchUpFrom = Math.Max(fromTotalHours, toTotalHours - policy.MaxCatchUpHours);
        for (var cursor = catchUpFrom; cursor < toTotalHours && progress < 1;)
        {
            var interval = Math.Min(policy.SampleIntervalHours, toTotalHours - cursor);
            if (sampler.IsActiveAt(cursor + interval / 2))
            {
                progress = Math.Min(1, progress + interval / policy.RequiredActiveHours);
            }

            cursor += interval;
        }

        return progress;
    }
}
