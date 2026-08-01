using System.Text;
using Vintagestory.API.Datastructures;

namespace PapermakingPapyrus;

public sealed class PapyrusPileTags
{
    public PapyrusPileTags(ITagRegistry<TagSet> registry)
    {
        registry.TryCreateTagSet(out var soakedStrip, [PapyrusConstants.SoakedStripTag]);
        registry.TryCreateTagSet(out var papyrusStrip, [PapyrusConstants.PapyrusStripTag]);
        registry.TryCreateTagSet(
            out var airDryingPapyrusStrip,
            [PapyrusConstants.AirDryingPapyrusStripTag]);
        registry.TryCreateTagSet(out var pressBoard, [PapyrusConstants.PressBoardTag]);
        registry.TryCreateTagSet(out var pressWeight, [PapyrusConstants.PressWeightTag]);

        SoakedStrip = soakedStrip;
        PapyrusStrip = papyrusStrip;
        AirDryingPapyrusStrip = airDryingPapyrusStrip;
        PressBoard = pressBoard;
        PressWeight = pressWeight;
    }

    public TagSet SoakedStrip { get; }
    public TagSet PapyrusStrip { get; }
    public TagSet AirDryingPapyrusStrip { get; }
    public TagSet PressBoard { get; }
    public TagSet PressWeight { get; }
}
