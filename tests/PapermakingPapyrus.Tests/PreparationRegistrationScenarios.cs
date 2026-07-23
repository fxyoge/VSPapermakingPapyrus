using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Xunit;

namespace PapermakingPapyrus.Tests;

public sealed class PreparationRegistrationScenarios : AtlasScenarioBase
{
    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public Task PreparationContentIsRegistered()
    {
        var dry = World.Api.World.GetItem(new AssetLocation(PapyrusConstants.DryStripsCode));
        var soaked = World.Api.World.GetItem(new AssetLocation(PapyrusConstants.SoakedStripsCode));
        var tops = World.Api.World.GetItem(new AssetLocation(PapyrusConstants.PapyrusTopsCode));
        var knife = FindKnife();

        Assert.NotNull(dry);
        Assert.NotNull(soaked);
        Assert.NotNull(tops);
        Assert.NotNull(knife);
        Assert.Equal(64, dry.MaxStackSize);
        Assert.Equal(64, soaked.MaxStackSize);
        Assert.True((tops.StorageFlags & EnumItemStorageFlags.Offhand) != 0);
        Assert.NotNull(knife.GetCollectibleBehavior<CollectibleBehaviorPapyrusTopCutting>(true));
        Assert.NotNull(tops.GetCollectibleBehavior<CollectibleBehaviorGroundStorable>(true));
        Assert.Contains(
            World.Api.World.GridRecipes,
            recipe => recipe.ResolvedIngredients?.Any(
                ingredient => ingredient?.ResolvedItemStack?.Collectible?.Code == tops.Code) == true);
        var recipes = World.Api.ModLoader.GetModSystem<RecipeRegistrySystem>().BarrelRecipes;
        var soakRecipe = Assert.Single(recipes, recipe => recipe.Code == "soak-papyrusstrips");
        Assert.Equal(PapyrusConstants.SoakHours, soakRecipe.SealHours);
        Assert.Equal(
            PapyrusConstants.SoakedStripsCode,
            soakRecipe.Output?.ResolvedItemStack?.Collectible?.Code?.ToString());

        return Task.CompletedTask;
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task CuttingCompletesOnlyAfterDurationAndMutatesOnServer()
    {
        var player = await World.JoinPlayer("PapyrusCutter");
        var knife = FindKnife();
        var tops = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.PapyrusTopsCode)));

        var knifeSlot = player.Player.InventoryManager.ActiveHotbarSlot;
        knifeSlot.Itemstack = new ItemStack(knife);
        var topsSlot = new DummySlot(new ItemStack(tops, 2));
        Assert.Equal(
            2,
            topsSlot.TryPutInto(World.Api.World, player.Entity.LeftHandItemSlot, 2));

        var handling = EnumHandHandling.NotHandled;
        knife.OnHeldInteractStart(knifeSlot, player.Entity, null!, null!, true, ref handling);
        knife.OnHeldInteractStop(1.5f, knifeSlot, player.Entity, null!, null!);
        await World.Ticks(2);

        Assert.Equal(1, player.Entity.LeftHandItemSlot.StackSize);
        Assert.Equal(
            2,
            player.Player.InventoryManager.Inventories
                .Where(entry => !entry.Key.StartsWith("creative", StringComparison.Ordinal))
                .SelectMany(entry => entry.Value)
                .Where(slot => slot.Itemstack?.Collectible?.Code?.ToString() == PapyrusConstants.DryStripsCode)
                .Sum(slot => slot.StackSize));
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public async Task CancelledCuttingDoesNotMutateInputs()
    {
        var player = await World.JoinPlayer("PapyrusCanceller");
        var knife = FindKnife();
        var tops = Assert.IsType<Item>(
            World.Api.World.GetItem(new AssetLocation(PapyrusConstants.PapyrusTopsCode)));

        var knifeSlot = player.Player.InventoryManager.ActiveHotbarSlot;
        knifeSlot.Itemstack = new ItemStack(knife);
        var topsSlot = new DummySlot(new ItemStack(tops, 1));
        Assert.Equal(
            1,
            topsSlot.TryPutInto(World.Api.World, player.Entity.LeftHandItemSlot));
        var durability = knife.GetRemainingDurability(knifeSlot.Itemstack);

        knife.OnHeldInteractCancel(
            0.75f,
            knifeSlot,
            player.Entity,
            null!,
            null!,
            EnumItemUseCancelReason.ReleasedMouse);
        await World.Ticks(2);

        Assert.Equal(1, player.Entity.LeftHandItemSlot.StackSize);
        Assert.Equal(durability, knife.GetRemainingDurability(knifeSlot.Itemstack));
    }

    [AtlasScenario(TimeoutMs = 120000, FreshWorld = true)]
    public Task BarrelRecipeRequiresFullSealedTimeAndConsumesInputs()
    {
        var recipe = Assert.Single(
            World.Api.ModLoader.GetModSystem<RecipeRegistrySystem>().BarrelRecipes,
            candidate => candidate.Code == "soak-papyrusstrips");
        var slots = recipe.Ingredients!
            .Select(ingredient => new DummySlot(ingredient.ResolvedItemStack!.Clone()))
            .Cast<ItemSlot>()
            .ToArray();

        Assert.True(recipe.Matches(slots, out var outputQuantity));
        Assert.Equal(PapyrusConstants.StripsPerBatch, outputQuantity);
        Assert.False(recipe.TryCraftNow(World.Api, PapyrusConstants.SoakHours - 0.01, slots));
        Assert.True(recipe.TryCraftNow(World.Api, PapyrusConstants.SoakHours, slots));

        var output = Assert.Single(slots, slot => slot.Itemstack != null).Itemstack!;
        Assert.Equal(PapyrusConstants.SoakedStripsCode, output.Collectible.Code.ToString());
        Assert.Equal(PapyrusConstants.StripsPerBatch, output.StackSize);
        return Task.CompletedTask;
    }

    private Item FindKnife()
    {
        return Assert.IsAssignableFrom<Item>(
            World.Api.World.Items.FirstOrDefault(
                item => item?.Durability > 0 &&
                    item.GetCollectibleBehavior<CollectibleBehaviorPapyrusTopCutting>(true) != null));
    }
}
