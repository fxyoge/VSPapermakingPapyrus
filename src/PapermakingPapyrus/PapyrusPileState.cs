namespace PapermakingPapyrus;

public enum PapyrusPileWorkState
{
    Laying,
    Boarded,
    Pressing,
    Dry
}

public enum PapyrusPileAction
{
    None,
    AddStrip,
    AddBoard,
    AddWeight,
    RemoveStrip,
    RemoveBoard,
    Collect
}

public readonly record struct PapyrusPileSnapshot(
    int StripCount,
    int BoardCount,
    bool HasWeight,
    PapyrusPileWorkState WorkState)
{
    public static PapyrusPileSnapshot Empty => new(0, 0, false, PapyrusPileWorkState.Laying);

    public bool IsCommitted => HasWeight;

    public bool IsValid =>
        StripCount is >= 0 and <= 8 &&
        BoardCount is >= 0 and <= 2 &&
        (!HasWeight || StripCount == 8 && BoardCount == 2) &&
        (BoardCount == 0 || StripCount == 8) &&
        (WorkState == DeriveState(StripCount, BoardCount, HasWeight) ||
         WorkState == PapyrusPileWorkState.Dry && HasWeight);

    public static PapyrusPileWorkState DeriveState(int strips, int boards, bool weight) =>
        weight ? PapyrusPileWorkState.Pressing :
        boards > 0 ? PapyrusPileWorkState.Boarded :
        PapyrusPileWorkState.Laying;

    public PapyrusPileAction NextAction(bool emptyHand, bool strip, bool board, bool weight)
    {
        if (HasWeight)
        {
            return PapyrusPileAction.None;
        }

        if (emptyHand)
        {
            return BoardCount > 0 ? PapyrusPileAction.RemoveBoard :
                StripCount > 0 ? PapyrusPileAction.RemoveStrip :
                PapyrusPileAction.None;
        }

        if (StripCount < 8)
        {
            return strip ? PapyrusPileAction.AddStrip : PapyrusPileAction.None;
        }

        if (BoardCount < 2)
        {
            return board ? PapyrusPileAction.AddBoard : PapyrusPileAction.None;
        }

        return weight ? PapyrusPileAction.AddWeight : PapyrusPileAction.None;
    }
}

public static class PapyrusDrying
{
    public const int VisualBandCount = 4;

    public static double Advance(double progress, double elapsedHours, double durationHours, bool freezing)
    {
        progress = double.IsFinite(progress) ? Math.Max(progress, 0) : 0;
        if (progress >= 1 ||
            freezing || !double.IsFinite(elapsedHours) || elapsedHours <= 0 ||
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
