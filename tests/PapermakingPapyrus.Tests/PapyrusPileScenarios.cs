using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;
using Xunit;
using static PapermakingPapyrus.Tests.PapyrusPileTestHelpers;

namespace PapermakingPapyrus.Tests;

public sealed class PapyrusPileScenarios : AtlasScenarioBase
{
    private const double ProgressTolerance = 0.002;

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public Task PileContentAndOptInMaterialsAreRegistered()
    {
        var pile = World.Api.World.GetBlock(new AssetLocation(PapyrusConstants.PileCode));
        var soaked = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.SoakedStripsCode)));
        var parchment = Assert.IsAssignableFrom<Item>(
            World.Api.World.GetItem(new AssetLocation(
                PapermakingPapyrusModSystem.ActiveFinishedSheetCode)));
        var boardTag = World.Api.CollectibleTagRegistry.CreateTagSet(PapyrusConstants.PressBoardTag);
        var weightTag = World.Api.CollectibleTagRegistry.CreateTagSet(PapyrusConstants.PressWeightTag);

        Assert.IsType<BlockPapyrusPile>(pile);
        Assert.NotNull(soaked.GetCollectibleBehavior<CollectibleBehaviorPapyrusPilePlacement>(true));
        Assert.Equal(
            PapermakingPapyrusModSystem.ActiveFinishedSheetCode,
            parchment.Code.ToString());
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
            World.Api.World.GetItem(new AssetLocation(
                PapermakingPapyrusModSystem.ActiveFinishedSheetCode)));
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
    public async Task PlacementRequiresSupportAndRemovingFinalStripDestroysPile()
    {
        var player = await PrepareAdminPlayer("PilePlacement");
        var soaked = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.SoakedStripsCode)));
        var supportPos = new BlockPos(1000, 120, 1000);
        var selection = await TeleportPlayerToPileLocation(player, supportPos);
        var slot = player.Player.InventoryManager.ActiveHotbarSlot;
        var placement = Assert.IsType<CollectibleBehaviorPapyrusPilePlacement>(
            soaked.GetCollectibleBehavior<CollectibleBehaviorPapyrusPilePlacement>(true));
        slot.Itemstack = new ItemStack(soaked, 3);
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

        var firstRemovedStrip = slot.TakeOutWhole();
        slot.MarkDirty();
        Assert.True(pile.Interact(player.Player));
        Assert.Null(World.Api.World.BlockAccessor.GetBlockEntity(pilePos));
        Assert.Equal(0, World.Api.World.BlockAccessor.GetBlock(pilePos).BlockId);
        Assert.Same(soaked, firstRemovedStrip.Collectible);
        Assert.Equal(1, firstRemovedStrip.StackSize);
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

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task PlacementWithAnEmptyServerSourceDoesNotLeaveAnEmptyPile()
    {
        var player = await PrepareAdminPlayer("EmptyPlacement");
        var soaked = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.SoakedStripsCode)));
        var supportPos = new BlockPos(1100, 120, 1100);
        var selection = await TeleportPlayerToPileLocation(player, supportPos);
        var placement = Assert.IsType<CollectibleBehaviorPapyrusPilePlacement>(
            soaked.GetCollectibleBehavior<CollectibleBehaviorPapyrusPilePlacement>(true));
        var slot = player.Player.InventoryManager.ActiveHotbarSlot;
        slot.Itemstack = null;
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

        var placementPos = selection.Position.UpCopy();
        Assert.Null(World.Api.World.BlockAccessor.GetBlockEntity(placementPos));
        Assert.Equal(0, World.Api.World.BlockAccessor.GetBlock(placementPos).BlockId);
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task PlacementWithAChangedServerSourceDoesNotStoreTheUnrelatedItem()
    {
        var player = await PrepareAdminPlayer("ChangedPlacement");
        var soaked = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.SoakedStripsCode)));
        var unrelated = World.Api.World.Items.First(item =>
            item?.Code != null &&
            item != soaked &&
            item.MaxStackSize > 1);
        var supportPos = new BlockPos(1200, 120, 1200);
        var selection = await TeleportPlayerToPileLocation(player, supportPos);
        var placement = Assert.IsType<CollectibleBehaviorPapyrusPilePlacement>(
            soaked.GetCollectibleBehavior<CollectibleBehaviorPapyrusPilePlacement>(true));
        var slot = player.Player.InventoryManager.ActiveHotbarSlot;
        slot.Itemstack = new ItemStack(unrelated, 2);
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

        Assert.Same(unrelated, slot.Itemstack.Collectible);
        Assert.Equal(2, slot.StackSize);
        var placementPos = selection.Position.UpCopy();
        Assert.Null(World.Api.World.BlockAccessor.GetBlockEntity(placementPos));
        Assert.Equal(0, World.Api.World.BlockAccessor.GetBlock(placementPos).BlockId);
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task DryingSurvivesRepeatedLiveAndUnloadedChunkTransitions()
    {
        var player = await World.JoinPlayer("DryingCycles");
        var pos = new BlockPos(512, 120, 512);
        await PrepareWarmFrozenClock();
        var pile = await BuildWeightedPile(player, pos);

        await AddHoursAndWaitForProgress(1, pos, 1d / 24);
        var firstLoadedInstance = pile;

        await UnloadPileChunk(player, pos, 2048);
        Assert.False(HasDryingListener(pile));
        await AddHours(6);
        pile = await ReloadPileChunk(player, pos, 7d / 24);
        Assert.NotSame(firstLoadedInstance, pile);
        Assert.True(HasDryingListener(pile));

        await AddHoursAndWaitForProgress(1, pos, 8d / 24);
        var secondLoadedInstance = pile;

        await UnloadPileChunk(player, pos, -2048);
        Assert.False(HasDryingListener(pile));
        await AddHours(7);
        pile = await ReloadPileChunk(player, pos, 15d / 24);
        Assert.NotSame(secondLoadedInstance, pile);
        Assert.True(HasDryingListener(pile));

        for (var hour = 16; hour <= 24; hour++)
        {
            await AddHoursAndWaitForProgress(1, pos, hour / 24d);
        }
        Assert.False(World.BlockEntityAt<BlockEntityPapyrusPile>(pos)!.IsDry);

        await AddHoursAndWaitForProgress(1, pos, 25d / 24);
        Assert.True(World.BlockEntityAt<BlockEntityPapyrusPile>(pos)!.IsDry);

        await UnloadPileChunk(player, pos, 2048);
        Assert.False(HasDryingListener(pile));
        pile = await ReloadPileChunk(player, pos, 25d / 24);
        Assert.True(pile.IsDry);
        Assert.False(HasDryingListener(pile));
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task DryingListenerExistsOnlyForAnActivelyDryingPile()
    {
        var player = await World.JoinPlayer("DryingListener");
        var pos = new BlockPos(640, 120, 640);
        await player.TeleportTo(pos);
        await World.Ticks(2);

        var pile = SpawnPile(pos);
        var soaked = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.SoakedStripsCode)));
        var source = new DummySlot(new ItemStack(soaked, 8));
        pile.AddInitialStrip(source);
        Assert.False(HasDryingListener(pile));

        for (var i = 1; i < 8; i++)
        {
            player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = source.TakeOut(1);
            Assert.True(pile.Interact(player.Player));
        }
        Assert.False(HasDryingListener(pile));

        var board = FindTagged(PapyrusConstants.PressBoardTag);
        for (var i = 0; i < 2; i++)
        {
            player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = new ItemStack(board);
            Assert.True(pile.Interact(player.Player));
            Assert.False(HasDryingListener(pile));
        }

        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack =
            new ItemStack(FindTagged(PapyrusConstants.PressWeightTag));
        Assert.True(pile.Interact(player.Player));
        Assert.True(HasDryingListener(pile));

        ProcessDrying(pile);
        ProcessDrying(pile);
        Assert.True(HasDryingListener(pile));

        SetDryingState(pile, 0.99, 1);
        ProcessDrying(pile);
        Assert.True(pile.IsDry);
        Assert.False(HasDryingListener(pile));
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task BreakingPileReleasesItsDryingListenerInEveryWorkState()
    {
        var player = await World.JoinPlayer("DryingBreak");
        await PrepareWarmFrozenClock();

        var incompletePos = new BlockPos(896, 120, 896);
        await player.TeleportTo(incompletePos);
        await World.Ticks(2);
        var incomplete = SpawnPile(incompletePos);
        incomplete.AddInitialStrip(new DummySlot(new ItemStack(
            Assert.IsType<Item>(
                World.Api.World.GetItem(new AssetLocation(PapyrusConstants.SoakedStripsCode))))));
        Assert.False(HasDryingListener(incomplete));
        incomplete.OnBlockBroken(player.Player);
        Assert.False(HasDryingListener(incomplete));

        var active = await BuildWeightedPile(player, new BlockPos(897, 120, 896));
        Assert.True(HasDryingListener(active));
        active.OnBlockBroken(player.Player);
        Assert.False(HasDryingListener(active));

        var dry = await BuildWeightedPile(player, new BlockPos(898, 120, 896));
        SetDryingState(dry, 1, 0);
        ProcessDrying(dry);
        Assert.True(dry.IsDry);
        Assert.False(HasDryingListener(dry));
        dry.OnBlockBroken(player.Player);
        Assert.False(HasDryingListener(dry));
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task BreakingWeightedPileDropsEveryStoredComponentExactlyOnce()
    {
        var player = await World.JoinPlayer("PileDropCounter");
        await PrepareWarmFrozenClock();
        var pos = new BlockPos(960, 120, 960);
        var pile = await BuildWeightedPile(player, pos);
        var soaked = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.SoakedStripsCode)));
        var board = FindTagged(PapyrusConstants.PressBoardTag);
        var weight = FindTagged(PapyrusConstants.PressWeightTag);

        Assert.True(HasDryingListener(pile));
        await player.TeleportTo(pos.AddCopy(16, 0, 0));
        await World.Ticks(2);
        World.Api.World.BlockAccessor.BreakBlock(pos, player.Player);
        await World.Ticks(2);

        Assert.False(HasDryingListener(pile));
        Assert.Null(World.Api.World.BlockAccessor.GetBlockEntity(pos));
        Assert.Equal(8, CountDroppedItems(pos, soaked));
        Assert.Equal(2, CountDroppedItems(pos, board));
        Assert.Equal(1, CountDroppedItems(pos, weight));
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task SubHourElapsedTimeProcessesAcrossUnloadedAndLoadedPhases()
    {
        var player = await World.JoinPlayer("DryingBoundary");
        var pos = new BlockPos(768, 120, 768);
        await PrepareWarmFrozenClock();
        await BuildWeightedPile(player, pos);

        await UnloadPileChunk(player, pos, 2048);
        await AddHours(0.5);
        var pile = await ReloadPileChunk(player, pos, 0.5 / 24);
        AssertProgress(0.5 / 24, pile.DryingProgress);

        await AddHoursAndWaitForProgress(0.49, pos, 0.99 / 24);
        await AddHoursAndWaitForProgress(0.01, pos, 1d / 24);
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task ReloadingWithoutElapsedTimeNeverDuplicatesDryingProgress()
    {
        var player = await World.JoinPlayer("DryingNoDupes");
        var pos = new BlockPos(1024, 120, 1024);
        await PrepareWarmFrozenClock();
        await BuildWeightedPile(player, pos);
        await AddHoursAndWaitForProgress(1, pos, 1d / 24);

        foreach (var distance in new[] { 2048, -2048, 3072 })
        {
            await UnloadPileChunk(player, pos, distance);
            var pile = await ReloadPileChunk(player, pos, 1d / 24);
            AssertProgress(1d / 24, pile.DryingProgress);
        }
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task DryingOnlyQueuesVisualUpdatesWhenItsRenderedBandChanges()
    {
        var player = await World.JoinPlayer("DryingVisual");
        await PrepareWarmFrozenClock();
        var withinBand = await BuildWeightedPile(player, new BlockPos(1280, 120, 1280));
        var crossingBand = await BuildWeightedPile(player, new BlockPos(1281, 120, 1280));
        var server = Assert.IsType<ServerMain>(World.Api.World);
        Assert.Equal(
            10_000,
            new PapermakingPapyrusConfig().DryingRefreshIntervalMilliseconds);

        SetDryingState(withinBand, 0.10, 0.1);
        var dirtyBefore = server.DirtyBlocks.Count;

        ProcessDrying(withinBand);

        Assert.Equal(dirtyBefore, server.DirtyBlocks.Count);
        AssertProgress(0.10 + 0.1 / 24, withinBand.DryingProgress);

        foreach (var progress in new[] { 0.24, 0.49, 0.74 })
        {
            SetDryingState(crossingBand, progress, 0.5);
            dirtyBefore = server.DirtyBlocks.Count;
            ProcessDrying(crossingBand);

            Assert.Equal(dirtyBefore + 1, server.DirtyBlocks.Count);
            AssertProgress(progress + 0.5 / 24, crossingBand.DryingProgress);
        }

        SetDryingState(crossingBand, 0.99, 0.5);
        dirtyBefore = server.DirtyBlocks.Count;
        ProcessDrying(crossingBand);

        Assert.Equal(dirtyBefore, server.DirtyBlocks.Count);
        Assert.True(crossingBand.IsDry);
    }

    private async Task PrepareWarmFrozenClock()
    {
        Assert.True((await World.ExecuteCommand("/time setmonth jun")).Ok);
        Assert.True((await World.ExecuteCommand("/time set day")).Ok);
        Assert.True((await World.ExecuteCommand("/time speed 0")).Ok);
    }

    private void SetDryingState(
        BlockEntityPapyrusPile pile,
        double progress,
        double elapsedHours)
    {
        var tree = new TreeAttribute();
        pile.ToTreeAttributes(tree);
        tree.SetDouble("dryingProgress", progress);
        tree.SetDouble("lastProcessedTotalHours", World.Calendar.TotalHours - elapsedHours);
        pile.FromTreeAttributes(tree, World.Api.World);
    }

    private static void ProcessDrying(BlockEntityPapyrusPile pile)
    {
        var method = typeof(BlockEntityPapyrusPile).GetMethod(
            "ProcessDrying",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(pile, null);
    }

    private BlockEntityPapyrusPile SpawnPile(BlockPos pos)
    {
        var pileBlock = Assert.IsType<BlockPapyrusPile>(
            World.Api.World.GetBlock(new AssetLocation(PapyrusConstants.PileCode)));
        World.Api.World.BlockAccessor.SetBlock(pileBlock.BlockId, pos);
        World.Api.World.BlockAccessor.SpawnBlockEntity(pileBlock.EntityClass, pos);
        return Assert.IsType<BlockEntityPapyrusPile>(
            World.Api.World.BlockAccessor.GetBlockEntity(pos));
    }

    private async Task<BlockEntityPapyrusPile> BuildWeightedPile(ITestPlayer player, BlockPos pos)
    {
        await player.TeleportTo(pos);
        await World.Ticks(2);

        var soaked = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.SoakedStripsCode)));
        var board = FindTagged(PapyrusConstants.PressBoardTag);
        var weight = FindTagged(PapyrusConstants.PressWeightTag);
        var pileBlock = Assert.IsType<BlockPapyrusPile>(
            World.Api.World.GetBlock(new AssetLocation(PapyrusConstants.PileCode)));
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
        Assert.Equal(PapyrusPileWorkState.Pressing, pile.Snapshot.WorkState);
        return pile;
    }

    private async Task AddHours(double hours)
    {
        var before = World.Calendar.TotalHours;
        var result = await World.ExecuteCommand($"/time add {hours:R} hours");
        Assert.True(result.Ok, result.Message);
        Assert.Equal(hours, World.Calendar.TotalHours - before, 6);
    }

    private async Task AddHoursAndWaitForProgress(
        double hours,
        BlockPos pos,
        double expectedProgress)
    {
        await AddHours(hours);
        var pile = Assert.IsType<BlockEntityPapyrusPile>(
            World.Api.World.BlockAccessor.GetBlockEntity(pos));
        ProcessDrying(pile);
        if (expectedProgress > 0)
        {
            await World.Until(
                () => World.BlockEntityAt<BlockEntityPapyrusPile>(pos)?.DryingProgress >=
                    expectedProgress - ProgressTolerance,
                200);
        }
        else
        {
            await World.Ticks(50);
        }

        AssertProgress(
            expectedProgress,
            pile.DryingProgress);
    }

    private async Task UnloadPileChunk(ITestPlayer player, BlockPos pos, int distance)
    {
        var save = await World.ExecuteCommand("/autosavenow");
        Assert.True(save.Ok, save.Message);
        var destination = new BlockPos(pos.X + distance, pos.Y, pos.Z + distance);
        await player.TeleportTo(destination);
        var serverApi = Assert.IsAssignableFrom<ICoreServerAPI>(World.Api);
        serverApi.WorldManager.UnloadChunkColumn(
            pos.X / GlobalConstants.ChunkSize,
            pos.Z / GlobalConstants.ChunkSize);
        await World.Until(
            () => World.Api.World.BlockAccessor.GetChunkAtBlockPos(pos) == null,
            600);
        Assert.Null(World.Api.World.BlockAccessor.GetBlockEntity(pos));
        // Chunk serialization runs on the server's chunk thread. Atlas can pump several
        // game ticks before that thread publishes the dirty chunk to the database.
        await Task.Delay(100);
        await World.Ticks(1);
    }

    private async Task<BlockEntityPapyrusPile> ReloadPileChunk(
        ITestPlayer player,
        BlockPos pos,
        double expectedProgress)
    {
        await player.TeleportTo(pos);
        await World.Until(
            () => World.BlockEntityAt<BlockEntityPapyrusPile>(pos) != null,
            600);
        var pile = Assert.IsType<BlockEntityPapyrusPile>(
            World.Api.World.BlockAccessor.GetBlockEntity(pos));
        AssertProgress(expectedProgress, pile.DryingProgress);
        return pile;
    }

    private static void AssertProgress(double expected, double actual) =>
        Assert.InRange(actual, expected - ProgressTolerance, expected + ProgressTolerance);

    private Item FindTagged(string tag)
    {
        var tagSet = World.Api.CollectibleTagRegistry.CreateTagSet(tag);
        return Assert.IsAssignableFrom<Item>(
            World.Api.World.Items.First(item => item?.Code != null && item.Tags.Overlaps(tagSet)));
    }

    private async Task<ITestPlayer> PrepareAdminPlayer(string playerName)
    {
        var player = await World.JoinPlayer(playerName);
        var serverApi = Assert.IsAssignableFrom<ICoreServerAPI>(World.Api);
        serverApi.Permissions.SetRole(
            Assert.IsAssignableFrom<IServerPlayer>(player.Player),
            "admin");
        player.Player.WorldData.CurrentGameMode = EnumGameMode.Survival;
        await World.Ticks(2);

        Assert.True(player.Player.HasPrivilege("buildblockseverywhere"));
        return player;
    }

    private async Task<BlockSelection> TeleportPlayerToPileLocation(ITestPlayer player, BlockPos supportPos)
    {
        var pileBlock = Assert.IsType<BlockPapyrusPile>(
            World.Api.World.GetBlock(new AssetLocation(PapyrusConstants.PileCode)));
        var support = World.Api.World.Blocks.First(block =>
            block?.Code != null &&
            block.BlockId != 0 &&
            block.CanAttachBlockAt(
                World.Api.World.BlockAccessor,
                pileBlock,
                supportPos,
                BlockFacing.UP));
        await player.TeleportTo(supportPos.UpCopy());
        await World.Ticks(2);
        World.Api.World.BlockAccessor.SetBlock(support.BlockId, supportPos);

        Assert.Equal(
            support.BlockId,
            World.Api.World.BlockAccessor.GetBlock(supportPos).BlockId);
        Assert.True(
            World.Api.World.BlockAccessor.GetBlock(supportPos.UpCopy()).Replaceable >= 6000);
        Assert.True(support.CanAttachBlockAt(
            World.Api.World.BlockAccessor,
            pileBlock,
            supportPos,
            BlockFacing.UP));

        return new BlockSelection
        {
            Position = supportPos,
            Face = BlockFacing.UP,
            HitPosition = new Vec3d(0.5, 1, 0.5)
        };
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
