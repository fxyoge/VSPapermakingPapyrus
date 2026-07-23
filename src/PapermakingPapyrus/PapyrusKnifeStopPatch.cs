using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace PapermakingPapyrus;

internal static class PapyrusKnifeStopPatch
{
    public static void Apply(Harmony harmony)
    {
        var original = AccessTools.DeclaredMethod(
            typeof(ItemKnife),
            nameof(ItemKnife.OnHeldInteractStop));
        var postfix = AccessTools.DeclaredMethod(
            typeof(PapyrusKnifeStopPatch),
            nameof(ForwardPapyrusCuttingStop));

        harmony.Patch(original, postfix: new HarmonyMethod(postfix));
    }

    private static void ForwardPapyrusCuttingStop(
        ItemKnife __instance,
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel)
    {
        var behavior =
            __instance.GetCollectibleBehavior<CollectibleBehaviorPapyrusTopCutting>(true);
        if (behavior == null)
        {
            return;
        }

        var handling = EnumHandling.PassThrough;
        behavior.OnHeldInteractStop(
            secondsUsed,
            slot,
            byEntity,
            blockSel,
            entitySel,
            ref handling);
    }
}
