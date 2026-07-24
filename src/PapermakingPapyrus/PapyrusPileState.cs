namespace PapermakingPapyrus;

public enum PapyrusPileWorkState
{
    Laying,
    Boarded,
    Pressing
}

public enum PapyrusPileAction
{
    None,
    AddStrip,
    AddBoard,
    AddWeight,
    RemoveStrip,
    RemoveBoard
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
        WorkState == DeriveState(StripCount, BoardCount, HasWeight);

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
