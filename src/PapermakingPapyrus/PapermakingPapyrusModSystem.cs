using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace PapermakingPapyrus;

public sealed class PapermakingPapyrusModSystem : ModSystem
{
    internal static PapermakingPapyrusConfig Config { get; private set; } = new();

    internal static ILogger? Logger { get; private set; }

    public override void Start(ICoreAPI api)
    {
        Logger = Mod.Logger;

        api.RegisterCollectibleBehaviorClass(
            "PapyrusTopCutting",
            typeof(CollectibleBehaviorPapyrusTopCutting));
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        var config = api.LoadModConfig<PapermakingPapyrusConfig>("papermakingpapyrus.json") ?? new();
        config.Sanitize();

        Config = config;

        if (api.Side == EnumAppSide.Server)
        {
            api.StoreModConfig(config, "papermakingpapyrus.json");
        }
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        var papyrusTops = api.World.GetItem(new AssetLocation(PapyrusConstants.PapyrusTopsCode));
        if (papyrusTops != null &&
            papyrusTops.GetCollectibleBehavior<CollectibleBehaviorPapyrusTopCutting>(true) == null)
        {
            var behavior = new CollectibleBehaviorPapyrusTopCutting(papyrusTops);
            behavior.Initialize(new JsonObject(new JObject()));
            behavior.OnLoaded(api);
            papyrusTops.CollectibleBehaviors = [.. papyrusTops.CollectibleBehaviors, behavior];
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
}
