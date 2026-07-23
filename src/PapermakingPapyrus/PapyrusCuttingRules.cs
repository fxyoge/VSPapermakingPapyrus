namespace PapermakingPapyrus;

public static class PapyrusCuttingRules
{
    public static bool HasCompleted(float secondsUsed, float requiredSeconds)
    {
        return float.IsFinite(secondsUsed) &&
            float.IsFinite(requiredSeconds) &&
            requiredSeconds > 0 &&
            secondsUsed >= requiredSeconds;
    }

    public static int ProducedQuantity(int topsConsumed, int stripsPerTop)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topsConsumed);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stripsPerTop);
        return checked(topsConsumed * stripsPerTop);
    }
}

