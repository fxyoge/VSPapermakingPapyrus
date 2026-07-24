namespace PapermakingPapyrus;

public readonly record struct PapyrusPileCompressionTransform(
    float ScaleOriginY,
    float ScaleY,
    float PressOffsetY);

public static class PapyrusPileCompression
{
    public static PapyrusPileCompressionTransform Calculate(
        float stripBottom,
        float stripTop,
        float scaleY)
    {
        if (!float.IsFinite(stripBottom) ||
            !float.IsFinite(stripTop) ||
            stripTop <= stripBottom ||
            !float.IsFinite(scaleY) ||
            scaleY <= 0)
        {
            return new(0, 1, 0);
        }

        var compressedTop = stripBottom + (stripTop - stripBottom) * scaleY;
        return new(stripBottom, scaleY, compressedTop - stripTop);
    }
}
