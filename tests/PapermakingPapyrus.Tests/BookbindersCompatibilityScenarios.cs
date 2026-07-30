using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace PapermakingPapyrus.Tests;

public sealed class BookbindersCompatibilityScenarios : AtlasScenarioBase
{
    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public Task BookbindersOwnsTheIntegratedMaterialsButNotThePressShortcut()
    {
        if (!World.Api.ModLoader.IsModEnabled(PapyrusConstants.BookbindersModId))
        {
            Assert.False(PapermakingPapyrusModSystem.BookbindersEnabled);
            return Task.CompletedTask;
        }

        Assert.True(PapermakingPapyrusModSystem.BookbindersEnabled);
        var tops = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.PapyrusTopsCode)));
        var dry = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(
                PapyrusConstants.BookbindersDryStripsCode)));
        var wet = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(
                PapyrusConstants.BookbindersWetStripsCode)));
        var rough = Assert.IsAssignableFrom<Item>(
            World.Api.World.GetItem(new AssetLocation(
                PapyrusConstants.BookbindersRoughPapyrusCode)));

        Assert.NotNull(
            wet.GetCollectibleBehavior<CollectibleBehaviorPapyrusPilePlacement>(true));
        Assert.Contains(
            wet.TransitionableProps,
            transition => transition.Type == EnumTransitionType.Dry &&
                transition.TransitionedStack?.Code?.ToString() ==
                    PapyrusConstants.BookbindersDryStripsCode);
        Assert.Contains(
            World.Api.World.GridRecipes,
            recipe =>
                recipe.Output?.ResolvedItemStack?.Collectible == dry &&
                recipe.ResolvedIngredients?.Any(
                    ingredient => ingredient?.ResolvedItemStack?.Collectible == tops) == true);
        Assert.DoesNotContain(
            World.Api.World.GridRecipes,
            recipe =>
                recipe.Output?.ResolvedItemStack?.Collectible == rough &&
                recipe.ResolvedIngredients?.Any(
                    ingredient => ingredient?.ResolvedItemStack?.Collectible == wet) == true);
        return Task.CompletedTask;
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task UnpressedDryStripsRequireDismantlingButAWeightedPileIsCommitted()
    {
        if (!PapermakingPapyrusModSystem.BookbindersEnabled)
        {
            return;
        }

        var player = await World.JoinPlayer("BookbindersPile");
        var wet = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(
                PapyrusConstants.BookbindersWetStripsCode)));
        var dry = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(
                PapyrusConstants.BookbindersDryStripsCode)));
        var board = FindTagged(PapyrusConstants.PressBoardTag);
        var weight = FindTagged(PapyrusConstants.PressWeightTag);
        var pos = new BlockPos(1400, 120, 1400);

        await player.TeleportTo(pos);
        await World.Ticks(2);
        var pile = SpawnPile(pos);
        var source = new DummySlot(new ItemStack(wet, 10));
        Assert.True(pile.AddInitialStrip(source));
        for (var i = 1; i < 8; i++)
        {
            player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = source.TakeOut(1);
            Assert.True(pile.Interact(player.Player));
        }

        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = new ItemStack(board);
        Assert.True(pile.Interact(player.Player));
        pile.Inventory[7].Itemstack = new ItemStack(dry);
        pile.Inventory[7].MarkDirty();

        Assert.Equal(PapyrusPileWorkState.ResoakRequired, pile.Snapshot.WorkState);
        Assert.True(pile.Snapshot.RequiresResoaking);

        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = source.TakeOut(1);
        Assert.True(pile.Interact(player.Player));
        Assert.Equal(8, pile.Snapshot.StripCount);
        Assert.Equal(1, pile.Snapshot.BoardCount);

        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = null;
        Assert.True(pile.Interact(player.Player));
        Assert.Equal(0, pile.Snapshot.BoardCount);
        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = null;
        Assert.True(pile.Interact(player.Player));
        Assert.Equal(7, pile.Snapshot.StripCount);
        Assert.False(pile.Snapshot.RequiresResoaking);

        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = source.TakeOut(1);
        Assert.True(pile.Interact(player.Player));
        for (var i = 0; i < 2; i++)
        {
            player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = new ItemStack(board);
            Assert.True(pile.Interact(player.Player));
        }
        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = new ItemStack(weight);
        Assert.True(pile.Interact(player.Player));

        pile.Inventory[0].Itemstack = new ItemStack(dry);
        pile.Inventory[0].MarkDirty();
        Assert.Equal(PapyrusPileWorkState.Pressing, pile.Snapshot.WorkState);
        Assert.False(pile.Snapshot.RequiresResoaking);
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

    private Item FindTagged(string tag)
    {
        var tagSet = World.Api.CollectibleTagRegistry.CreateTagSet(tag);
        return Assert.IsAssignableFrom<Item>(
            World.Api.World.Items.First(item => item?.Code != null && item.Tags.Overlaps(tagSet)));
    }
}
