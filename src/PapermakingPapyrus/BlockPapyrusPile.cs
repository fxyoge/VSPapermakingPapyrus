using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PapermakingPapyrus;

public sealed class BlockPapyrusPile : Block
{
    public override bool OnBlockInteractStart(
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel) =>
        world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use) &&
        world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityPapyrusPile pile &&
        pile.Interact(byPlayer);

    public override ItemStack[] GetDrops(
        IWorldAccessor world,
        BlockPos pos,
        IPlayer byPlayer,
        float dropQuantityMultiplier = 1) => [];

    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos) =>
        blockAccessor.GetBlockEntity(pos) is BlockEntityPapyrusPile pile
            ? [new Cuboidf(0.08f, 0, 0.08f, 0.92f, pile.SelectionHeight, 0.92f)]
            : base.GetSelectionBoxes(blockAccessor, pos);

    public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos) =>
        GetSelectionBoxes(blockAccessor, pos);

    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
    {
        base.OnNeighbourBlockChange(world, pos, neibpos);
        if (neibpos.Equals(pos.DownCopy()) &&
            !world.BlockAccessor.GetBlock(neibpos).CanAttachBlockAt(
                world.BlockAccessor,
                this,
                neibpos,
                BlockFacing.UP))
        {
            world.BlockAccessor.BreakBlock(pos, null);
        }
    }
}
