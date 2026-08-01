namespace PapermakingPapyrus;

public static class PapyrusDrying
{
    public const int VisualBandCount = 4;

    public static double Advance(double progress, double elapsedHours, double durationHours)
    {
        progress = double.IsFinite(progress) ? Math.Max(progress, 0) : 0;
        if (progress >= 1 ||
            !double.IsFinite(elapsedHours) || elapsedHours <= 0 ||
            !double.IsFinite(durationHours) || durationHours <= 0)
        {
            return progress;
        }

        return progress + elapsedHours / durationHours;
    }

    public static int VisualBand(double progress) =>
        Math.Min((int)(Math.Clamp(double.IsFinite(progress) ? progress : 0, 0, 1) *
            VisualBandCount), VisualBandCount - 1);

    public static double RemainingHours(double progress, double durationHours) =>
        Math.Max(0, (1 - Math.Clamp(double.IsFinite(progress) ? progress : 0, 0, 1)) *
            durationHours);
}
