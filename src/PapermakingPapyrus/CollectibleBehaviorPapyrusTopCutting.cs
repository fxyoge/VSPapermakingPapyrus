using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace PapermakingPapyrus;

public sealed class CollectibleBehaviorPapyrusTopCutting(CollectibleObject collObj)
    : CollectibleBehavior(collObj)
{
    private const string CuttingAnimation = "squeezehoneycomb";
    private static readonly AssetLocation CuttingSound =
        new(PapyrusConstants.CuttingSound);

    private ICoreAPI? api;
    private TagSet knifeTag;
    private bool knifeTagLoaded;

    public override void OnLoaded(ICoreAPI api)
    {
        this.api = api;
        knifeTag = api.CollectibleTagRegistry.CreateTagSet(PapyrusConstants.KnifeTag);
        knifeTagLoaded = true;
    }

    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handHandling,
        ref EnumHandling handling)
    {
        if (!CanCut(slot, byEntity))
        {
            handling = EnumHandling.PassThrough;
            return;
        }

        handHandling = EnumHandHandling.PreventDefault;
        handling = EnumHandling.PreventDefault;
        byEntity.StartAnimation(CuttingAnimation);
        PlayCuttingSound(byEntity);
    }

    public override bool OnHeldInteractStep(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        ref EnumHandling handling)
    {
        if (!CanCut(slot, byEntity))
        {
            handling = EnumHandling.PreventDefault;
            return false;
        }

        handling = EnumHandling.PreventDefault;
        if (byEntity.World.Rand.NextDouble() < 0.05)
        {
            PlayCuttingSound(byEntity);
        }

        return !PapyrusCuttingRules.HasCompleted(
            secondsUsed,
            PapermakingPapyrusModSystem.Config.CuttingDurationSeconds);
    }

    public override void OnHeldInteractStop(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        ref EnumHandling handling)
    {
        handling = EnumHandling.PreventDefault;
        byEntity.StopAnimation(CuttingAnimation);

        if (byEntity.World.Side != EnumAppSide.Server ||
            !PapyrusCuttingRules.HasCompleted(
                secondsUsed,
                PapermakingPapyrusModSystem.Config.CuttingDurationSeconds) ||
            !CanCut(slot, byEntity))
        {
            return;
        }

        var dryStrips = api?.World.GetItem(new AssetLocation(PapyrusConstants.DryStripsCode));
        if (dryStrips == null)
        {
            PapermakingPapyrusModSystem.Logger?.Error(
                "Cannot complete cutting: {0} is not registered.",
                PapyrusConstants.DryStripsCode);
            return;
        }

        slot.TakeOut(1);
        slot.MarkDirty();

        var knifeSlot = byEntity.LeftHandItemSlot!;
        knifeSlot.Itemstack!.Collectible.DamageItem(byEntity.World, byEntity, knifeSlot, 1);

        var output = new ItemStack(
            dryStrips,
            PapyrusCuttingRules.ProducedQuantity(
                1,
                PapermakingPapyrusModSystem.Config.DryStripsPerPapyrusTop));

        PlayCuttingSound(byEntity);
        if (byEntity is EntityPlayer entityPlayer &&
            entityPlayer.Player.InventoryManager.TryGiveItemstack(output, true))
        {
            return;
        }

        byEntity.World.SpawnItemEntity(output, byEntity.Pos.XYZ);
    }

    public override bool OnHeldInteractCancel(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        EnumItemUseCancelReason cancelReason,
        ref EnumHandling handling)
    {
        handling = EnumHandling.PreventDefault;
        byEntity.StopAnimation(CuttingAnimation);
        return true;
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    {
        handling = EnumHandling.PassThrough;
        if (api == null || !knifeTagLoaded)
        {
            return [];
        }

        var knives = api.World.Items
            .Where(item => item?.Code != null && item.Tags.Overlaps(knifeTag))
            .Select(item => new ItemStack(item))
            .ToArray();

        return knives.Length == 0
            ? []
            :
            [
                new WorldInteraction
                {
                    ActionLangCode = PapyrusConstants.Domain + ":heldhelp-cut-papyrus",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = knives
                }
            ];
    }

    private bool CanCut(ItemSlot papyrusSlot, EntityAgent byEntity)
    {
        var knifeSlot = byEntity.LeftHandItemSlot;
        return papyrusSlot.Itemstack?.Collectible == collObj &&
            knifeSlot?.Itemstack != null &&
            knifeTagLoaded &&
            knifeSlot.Itemstack.Collectible.Tags.Overlaps(knifeTag) &&
            knifeSlot.Itemstack.Collectible.GetRemainingDurability(knifeSlot.Itemstack) > 0;
    }

    private static void PlayCuttingSound(EntityAgent byEntity)
    {
        var player = (byEntity as EntityPlayer)?.Player;
        byEntity.World.PlaySoundAt(
            CuttingSound,
            byEntity,
            player,
            randomizePitch: true,
            range: 32,
            volume: 1);
    }
}
