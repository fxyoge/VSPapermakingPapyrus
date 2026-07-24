using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace PapermakingPapyrus;

public sealed class CollectibleBehaviorPapyrusPilePlacement(CollectibleObject collObj)
    : CollectibleBehavior(collObj)
{
    private ICoreAPI? api;

    public override void OnLoaded(ICoreAPI api) => this.api = api;

    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handHandling,
        ref EnumHandling handling)
    {
        if (blockSel?.Face != BlockFacing.UP || api == null)
        {
            handling = EnumHandling.PassThrough;
            return;
        }

        var placePos = blockSel.Position.UpCopy();
        if (byEntity is not EntityPlayer entityPlayer ||
            (!entityPlayer.Player.HasPrivilege("buildblockseverywhere") &&
             !byEntity.World.Claims.TryAccess(
                 entityPlayer.Player,
                 placePos,
                 EnumBlockAccessFlags.BuildOrBreak)) ||
            byEntity.World.BlockAccessor.GetBlock(placePos).Replaceable < 6000 ||
            !byEntity.World.BlockAccessor.GetBlock(blockSel.Position).CanAttachBlockAt(
                byEntity.World.BlockAccessor,
                api.World.GetBlock(new AssetLocation(PapyrusConstants.PileCode)),
                blockSel.Position,
                BlockFacing.UP))
        {
            handling = EnumHandling.PreventDefault;
            handHandling = EnumHandHandling.PreventDefault;
            return;
        }

        handling = EnumHandling.PreventDefault;
        handHandling = EnumHandHandling.PreventDefault;
        if (byEntity.World.Side != EnumAppSide.Server)
        {
            return;
        }

        var pileBlock = api.World.GetBlock(new AssetLocation(PapyrusConstants.PileCode));
        if (pileBlock == null)
        {
            return;
        }

        byEntity.World.BlockAccessor.SetBlock(pileBlock.BlockId, placePos);
        byEntity.World.BlockAccessor.SpawnBlockEntity(pileBlock.EntityClass, placePos);
        if (byEntity.World.BlockAccessor.GetBlockEntity(placePos) is not BlockEntityPapyrusPile pile)
        {
            byEntity.World.BlockAccessor.SetBlock(0, placePos);
            return;
        }

        pile.Orientation = byEntity.Pos.Yaw;
        pile.AddInitialStrip(
            slot,
            entityPlayer.Player.WorldData.CurrentGameMode != EnumGameMode.Creative);
    }
}
