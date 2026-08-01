using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace PapermakingPapyrus;

public sealed class PapermakingPapyrusModSystem : ModSystem
{
    public static bool BookbindersEnabled { get; private set; }

    public static string ActiveDryStripsCode =>
        BookbindersEnabled
            ? PapyrusConstants.BookbindersDryStripsCode
            : PapyrusConstants.DryStripsCode;

    public static string ActiveFinishedSheetCode =>
        BookbindersEnabled
            ? PapyrusConstants.BookbindersRoughPapyrusCode
            : PapyrusConstants.ParchmentCode;

    public static PapermakingPapyrusConfig ServerConfig { get; private set; } = new();

    public static PapermakingPapyrusClientSettings ClientSettings { get; private set; } =
        PapermakingPapyrusClientSettings.Default;

    internal static ILogger? Logger { get; private set; }

    private ICoreServerAPI? serverApi;

    public override void Start(ICoreAPI api)
    {
        Logger = Mod.Logger;
        ObjectCacheUtil.Delete(api, PapyrusConstants.MissingPileTextureWarningsCacheKey);
        ObjectCacheUtil.Delete(api, PapyrusConstants.PileTagsCacheKey);
        api.ObjectCache[PapyrusConstants.MissingPileTextureWarningsCacheKey] =
            new ConcurrentDictionary<AssetLocation, byte>();

        api.RegisterCollectibleBehaviorClass(
            "PapyrusTopCutting",
            typeof(CollectibleBehaviorPapyrusTopCutting));
        api.RegisterCollectibleBehaviorClass(
            "PapyrusPilePlacement",
            typeof(CollectibleBehaviorPapyrusPilePlacement));
        api.RegisterBlockClass("PapyrusPile", typeof(BlockPapyrusPile));
        api.RegisterBlockEntityClass("PapyrusPile", typeof(BlockEntityPapyrusPile));
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Server)
        {
            return;
        }

        var config = api.LoadModConfig<PapermakingPapyrusConfig>("papermakingpapyrus.json") ?? new();
        config.Sanitize();

        ServerConfig = config;
        api.StoreModConfig(config, "papermakingpapyrus.json");
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        ClientSettings = PapermakingPapyrusClientSettings.Default;
        api.Network.RegisterChannel(PapyrusConstants.SettingsNetworkChannel)
            .RegisterMessageType<PapermakingPapyrusSettingsPacket>()
            .SetMessageHandler<PapermakingPapyrusSettingsPacket>(
                packet => ClientSettings = packet.ToClientSettings());
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        serverApi = api;
        api.Network.RegisterChannel(PapyrusConstants.SettingsNetworkChannel)
            .RegisterMessageType<PapermakingPapyrusSettingsPacket>();
        api.Event.PlayerNowPlaying += SendSettings;
    }

    public override void Dispose()
    {
        if (serverApi != null)
        {
            serverApi.Event.PlayerNowPlaying -= SendSettings;
            serverApi = null;
        }

        base.Dispose();
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        api.ObjectCache[PapyrusConstants.PileTagsCacheKey] =
            new PapyrusPileTags(api.CollectibleTagRegistry);

        BookbindersEnabled =
            api.ModLoader.IsModEnabled(PapyrusConstants.BookbindersModId) &&
            api.World.GetItem(new AssetLocation(PapyrusConstants.BookbindersDryStripsCode)) != null &&
            api.World.GetItem(new AssetLocation(PapyrusConstants.BookbindersWetStripsCode)) != null &&
            api.World.GetItem(new AssetLocation(PapyrusConstants.BookbindersRoughPapyrusCode)) != null;

        var papyrusTops = api.World.GetItem(new AssetLocation(PapyrusConstants.PapyrusTopsCode));
        if (papyrusTops != null &&
            papyrusTops.GetCollectibleBehavior<CollectibleBehaviorPapyrusTopCutting>(true) == null)
        {
            var behavior = new CollectibleBehaviorPapyrusTopCutting(papyrusTops);
            behavior.Initialize(new JsonObject(new JObject()));
            behavior.OnLoaded(api);
            papyrusTops.CollectibleBehaviors = [.. papyrusTops.CollectibleBehaviors, behavior];
        }

        var soakedStripTag = api.CollectibleTagRegistry.CreateTagSet(
            PapyrusConstants.SoakedStripTag);
        foreach (var soakedStrips in api.World.Items.Where(
                     item => item?.Code != null && item.Tags.Overlaps(soakedStripTag)))
        {
            if (soakedStrips.GetCollectibleBehavior<CollectibleBehaviorPapyrusPilePlacement>(true) == null)
            {
                var behavior = new CollectibleBehaviorPapyrusPilePlacement(soakedStrips);
                behavior.Initialize(new JsonObject(new JObject()));
                behavior.OnLoaded(api);
                soakedStrips.CollectibleBehaviors = [.. soakedStrips.CollectibleBehaviors, behavior];
            }
        }

        if (BookbindersEnabled)
        {
            var removed = api.World.GridRecipes.RemoveAll(IsBookbindersPapyrusPressRecipe);
            Mod.Logger.Notification(
                "Bookbinders compatibility enabled; removed {0} crafting grid papyrus press recipe(s).",
                removed);
        }

        var knifeTag = api.CollectibleTagRegistry.CreateTagSet(PapyrusConstants.KnifeTag);
        var preparedKnives = 0;
        foreach (var item in api.World.Items)
        {
            if (item?.Code == null || !item.Tags.Overlaps(knifeTag))
            {
                continue;
            }

            item.StorageFlags |= EnumItemStorageFlags.Offhand;
            preparedKnives++;
        }

        Mod.Logger.Notification(
            "Prepared {0} tagged knife item type(s) for offhand cutting.",
            preparedKnives);
    }

    private static bool IsBookbindersPapyrusPressRecipe(GridRecipe recipe) =>
        recipe.Output?.ResolvedItemStack?.Collectible?.Code?.ToString() ==
            PapyrusConstants.BookbindersRoughPapyrusCode &&
        recipe.ResolvedIngredients?.Any(
            ingredient => ingredient?.ResolvedItemStack?.Collectible?.Code?.ToString() ==
                PapyrusConstants.BookbindersWetStripsCode) == true;

    private void SendSettings(IServerPlayer player)
    {
        serverApi?.Network.GetChannel(PapyrusConstants.SettingsNetworkChannel).SendPacket(
            PapermakingPapyrusSettingsPacket.FromConfig(ServerConfig),
            player);
    }
}
