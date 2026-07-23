using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace PapermakingPapyrus;

public sealed class CollectibleBehaviorPapyrusTopCutting(CollectibleObject collObj)
    : CollectibleBehavior(collObj)
{
    private ICoreAPI? api;

    public override void OnLoaded(ICoreAPI api)
    {
        this.api = api;
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
        byEntity.StartAnimation("knifestab");
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
        byEntity.StopAnimation("knifestab");

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
            api?.Logger.Error(
                "[Papermaking: Papyrus] Cannot complete cutting: {0} is not registered.",
                PapyrusConstants.DryStripsCode);
            return;
        }

        var offhand = byEntity.LeftHandItemSlot!;
        offhand.TakeOut(1);
        offhand.MarkDirty();
        slot.Itemstack!.Collectible.DamageItem(byEntity.World, byEntity, slot, 1);

        var output = new ItemStack(
            dryStrips,
            PapyrusCuttingRules.ProducedQuantity(
                1,
                PapermakingPapyrusModSystem.Config.DryStripsPerPapyrusTop));

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
        byEntity.StopAnimation("knifestab");
        return true;
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    {
        handling = EnumHandling.PassThrough;
        var papyrusTops = api?.World.GetItem(new AssetLocation(PapyrusConstants.PapyrusTopsCode));
        if (papyrusTops == null)
        {
            return [];
        }

        return
        [
            new WorldInteraction
            {
                ActionLangCode = PapyrusConstants.Domain + ":heldhelp-cut-papyrus",
                MouseButton = EnumMouseButton.Right,
                Itemstacks =
                [
                    new ItemStack(papyrusTops)
                ]
            }
        ];
    }

    private bool CanCut(ItemSlot knifeSlot, EntityAgent byEntity)
    {
        return knifeSlot.Itemstack?.Collectible == collObj &&
            collObj.GetRemainingDurability(knifeSlot.Itemstack) > 0 &&
            byEntity.LeftHandItemSlot?.Itemstack?.Collectible?.Code?.ToString() ==
                PapyrusConstants.PapyrusTopsCode;
    }
}
