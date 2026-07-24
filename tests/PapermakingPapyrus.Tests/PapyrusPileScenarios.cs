using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace PapermakingPapyrus.Tests;

public sealed class PapyrusPileScenarios : AtlasScenarioBase
{
    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public Task PileContentAndOptInMaterialsAreRegistered()
    {
        var pile = World.Api.World.GetBlock(new AssetLocation(PapyrusConstants.PileCode));
        var soaked = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.SoakedStripsCode)));
        var parchment = Assert.IsAssignableFrom<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.ParchmentCode)));
        var boardTag = World.Api.CollectibleTagRegistry.CreateTagSet(PapyrusConstants.PressBoardTag);
        var weightTag = World.Api.CollectibleTagRegistry.CreateTagSet(PapyrusConstants.PressWeightTag);

        Assert.IsType<BlockPapyrusPile>(pile);
        Assert.NotNull(soaked.GetCollectibleBehavior<CollectibleBehaviorPapyrusPilePlacement>(true));
        Assert.Equal("game:paper-parchment", parchment.Code.ToString());
        Assert.Contains(World.Api.World.Items, item => item?.Code != null && item.Tags.Overlaps(boardTag));
        Assert.Contains(World.Api.World.Items, item => item?.Code != null && item.Tags.Overlaps(weightTag));
        return Task.CompletedTask;
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task FullConstructionPersistsExactComponentsAndLocksWhenWeighted()
    {
        var player = await World.JoinPlayer("PileBuilder");
        var soaked = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.SoakedStripsCode)));
        var board = FindTagged(PapyrusConstants.PressBoardTag);
        var weight = FindTagged(PapyrusConstants.PressWeightTag);
        var pileBlock = Assert.IsType<BlockPapyrusPile>(
            World.Api.World.GetBlock(new AssetLocation(PapyrusConstants.PileCode)));
        var pos = new BlockPos(0, 120, 0);

        await player.TeleportTo(pos);
        await World.Ticks(2);
        Assert.Equal("PapyrusPile", pileBlock.EntityClass);
        World.Api.World.BlockAccessor.SetBlock(pileBlock.BlockId, pos);
        World.Api.World.BlockAccessor.SpawnBlockEntity(pileBlock.EntityClass, pos);
        var pile = Assert.IsType<BlockEntityPapyrusPile>(
            World.Api.World.BlockAccessor.GetBlockEntity(pos));
        var source = new DummySlot(new ItemStack(soaked, 8));
        pile.AddInitialStrip(source);
        for (var i = 1; i < 8; i++)
        {
            player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = source.TakeOut(1);
            Assert.True(pile.Interact(player.Player));
        }

        for (var i = 0; i < 2; i++)
        {
            player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = new ItemStack(board);
            Assert.True(pile.Interact(player.Player));
        }

        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = new ItemStack(weight);
        Assert.True(pile.Interact(player.Player));

        Assert.Equal(
            new PapyrusPileSnapshot(8, 2, true, PapyrusPileWorkState.Pressing),
            pile.Snapshot);
        Assert.Equal(8, pile.GetNonEmptyContentStacks().Count(stack => stack.Collectible == soaked));
        Assert.Equal(2, pile.GetNonEmptyContentStacks().Count(stack => stack.Collectible == board));
        Assert.Single(pile.GetNonEmptyContentStacks(), stack => stack.Collectible == weight);

        var tree = new TreeAttribute();
        pile.ToTreeAttributes(tree);
        Assert.Equal(2, tree.GetInt("contractVersion"));
        Assert.Equal("pressing", tree.GetString("workState"));
        Assert.Equal(0, tree.GetDouble("dryingProgress"));

        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = null;
        Assert.True(pile.Interact(player.Player));
        Assert.Equal(11, pile.GetNonEmptyContentStacks().Length);
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task CompletedPileGivesParchmentAndDropsReusablePressParts()
    {
        var player = await World.JoinPlayer("PileCollector");
        var soaked = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.SoakedStripsCode)));
        var parchment = Assert.IsAssignableFrom<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.ParchmentCode)));
        var board = FindTagged(PapyrusConstants.PressBoardTag);
        var weight = FindTagged(PapyrusConstants.PressWeightTag);
        var pileBlock = Assert.IsType<BlockPapyrusPile>(
            World.Api.World.GetBlock(new AssetLocation(PapyrusConstants.PileCode)));
        var pos = new BlockPos(30, 120, 30);

        await player.TeleportTo(pos);
        await World.Ticks(2);
        var support = World.Api.World.Blocks.First(block =>
            block?.Code != null &&
            block.BlockId != 0 &&
            block.CanAttachBlockAt(
                World.Api.World.BlockAccessor,
                pileBlock,
                pos.DownCopy(),
                BlockFacing.UP));
        World.Api.World.BlockAccessor.SetBlock(support.BlockId, pos.DownCopy());
        World.Api.World.BlockAccessor.SetBlock(pileBlock.BlockId, pos);
        World.Api.World.BlockAccessor.SpawnBlockEntity(pileBlock.EntityClass, pos);
        var pile = Assert.IsType<BlockEntityPapyrusPile>(
            World.Api.World.BlockAccessor.GetBlockEntity(pos));
        var source = new DummySlot(new ItemStack(soaked, 8));
        pile.AddInitialStrip(source);
        for (var i = 1; i < 8; i++)
        {
            player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = source.TakeOut(1);
            pile.Interact(player.Player);
        }

        for (var i = 0; i < 2; i++)
        {
            player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = new ItemStack(board);
            pile.Interact(player.Player);
        }

        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = new ItemStack(weight);
        pile.Interact(player.Player);
        var tree = new TreeAttribute();
        pile.ToTreeAttributes(tree);
        tree.SetDouble("dryingProgress", 1);
        pile.FromTreeAttributes(tree, World.Api.World);

        Assert.Equal(PapyrusPileWorkState.Dry, pile.Snapshot.WorkState);
        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = null;
        pile.Interact(player.Player);

        var droppedBoards = CountDroppedItems(pos, board);
        var droppedWeights = CountDroppedItems(pos, weight);

        Assert.Null(World.Api.World.BlockAccessor.GetBlockEntity(pos));
        Assert.Equal(1, CountPlayerItems(player, parchment));
        Assert.Equal(0, CountPlayerItems(player, board));
        Assert.Equal(0, CountPlayerItems(player, weight));
        Assert.Equal(0, CountPlayerItems(player, soaked));
        Assert.Equal(2, droppedBoards);
        Assert.Equal(1, droppedWeights);
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task PlacementRequiresSupportAndPreCommitRemovalIsReverseOrdered()
    {
        var player = await World.JoinPlayer("PilePlacement");
        var soaked = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.SoakedStripsCode)));
        var pileBlock = Assert.IsType<BlockPapyrusPile>(
            World.Api.World.GetBlock(new AssetLocation(PapyrusConstants.PileCode)));
        var support = World.Api.World.Blocks.First(block =>
            block?.Code != null &&
            block.BlockId != 0 &&
            block.CanAttachBlockAt(
                World.Api.World.BlockAccessor,
                pileBlock,
                new BlockPos(1000, 120, 1000),
                BlockFacing.UP));
        var supportPos = new BlockPos(1000, 120, 1000);
        await player.TeleportTo(supportPos.UpCopy());
        await World.Ticks(2);
        var serverApi = Assert.IsAssignableFrom<ICoreServerAPI>(World.Api);
        serverApi.Permissions.SetRole(
            Assert.IsAssignableFrom<IServerPlayer>(player.Player),
            "admin");
        await World.Ticks(2);
        World.Api.World.BlockAccessor.SetBlock(support.BlockId, supportPos);
        Assert.Equal(
            support.BlockId,
            World.Api.World.BlockAccessor.GetBlock(supportPos).BlockId);
        var slot = player.Player.InventoryManager.ActiveHotbarSlot;
        var placement = Assert.IsType<CollectibleBehaviorPapyrusPilePlacement>(
            soaked.GetCollectibleBehavior<CollectibleBehaviorPapyrusPilePlacement>(true));
        slot.Itemstack = new ItemStack(soaked, 3);
        var selection = new BlockSelection
        {
            Position = supportPos,
            Face = BlockFacing.UP,
            HitPosition = new Vec3d(0.5, 1, 0.5)
        };
        Assert.True(player.Player.HasPrivilege("buildblockseverywhere"));
        Assert.True(World.Api.World.BlockAccessor.GetBlock(supportPos.UpCopy()).Replaceable >= 6000);
        Assert.True(support.CanAttachBlockAt(
            World.Api.World.BlockAccessor,
            pileBlock,
            supportPos,
            BlockFacing.UP));
        var handHandling = EnumHandHandling.NotHandled;
        var handling = EnumHandling.PassThrough;

        placement.OnHeldInteractStart(
            slot,
            player.Entity,
            selection,
            null!,
            true,
            ref handHandling,
            ref handling);
        await World.Ticks(2);

        var pilePos = supportPos.UpCopy();
        var pile = Assert.IsType<BlockEntityPapyrusPile>(
            World.Api.World.BlockAccessor.GetBlockEntity(pilePos));
        Assert.Equal(1, pile.Snapshot.StripCount);
        Assert.Equal(2, slot.StackSize);

        Assert.True(pile.Interact(player.Player));
        Assert.Equal(2, pile.Snapshot.StripCount);
        slot.Itemstack = null;
        Assert.True(pile.Interact(player.Player));
        Assert.Equal(1, pile.Snapshot.StripCount);
        Assert.Equal(1, CountPlayerItems(player, soaked));

        var unsupportedSelection = new BlockSelection
        {
            Position = new BlockPos(1008, 120, 1008),
            Face = BlockFacing.UP,
            HitPosition = new Vec3d(0.5, 1, 0.5)
        };
        slot.Itemstack = new ItemStack(soaked);
        handHandling = EnumHandHandling.NotHandled;
        handling = EnumHandling.PassThrough;
        placement.OnHeldInteractStart(
            slot,
            player.Entity,
            unsupportedSelection,
            null!,
            true,
            ref handHandling,
            ref handling);

        Assert.Null(World.Api.World.BlockAccessor.GetBlockEntity(unsupportedSelection.Position.UpCopy()));
    }

    private Item FindTagged(string tag)
    {
        var tagSet = World.Api.CollectibleTagRegistry.CreateTagSet(tag);
        return Assert.IsAssignableFrom<Item>(
            World.Api.World.Items.First(item => item?.Code != null && item.Tags.Overlaps(tagSet)));
    }

    private int CountDroppedItems(BlockPos pos, CollectibleObject collectible) =>
        World.Api.World
            .GetEntitiesAround(
                pos.ToVec3d().Add(0.5, 0.5, 0.5),
                3,
                3,
                entity => entity is EntityItem item &&
                    item.Itemstack?.Collectible == collectible)
            .OfType<EntityItem>()
            .Sum(item => item.Itemstack.StackSize);

    private static int CountPlayerItems(ITestPlayer player, CollectibleObject collectible) =>
        player.Player.InventoryManager.Inventories
            .Where(entry => !entry.Key.StartsWith("creative", StringComparison.Ordinal))
            .SelectMany(entry => entry.Value)
            .Where(slot => slot.Itemstack?.Collectible == collectible)
            .Sum(slot => slot.StackSize);
}
