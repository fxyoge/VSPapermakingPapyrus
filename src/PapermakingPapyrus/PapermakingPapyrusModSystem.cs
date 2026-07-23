using HarmonyLib;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace PapermakingPapyrus;

public sealed class PapermakingPapyrusModSystem : ModSystem
{
    private const string HarmonyId = "papermakingpapyrus.knife-stop-forwarding";
    private Harmony? harmony;

    internal static PapermakingPapyrusConfig Config { get; private set; } = new();

    public override void Start(ICoreAPI api)
    {
        api.RegisterCollectibleBehaviorClass(
            "PapyrusTopCutting",
            typeof(CollectibleBehaviorPapyrusTopCutting));

        harmony = new Harmony(HarmonyId);
        PapyrusKnifeStopPatch.Apply(harmony);
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        var config = api.LoadModConfig<PapermakingPapyrusConfig>("papermakingpapyrus.json") ?? new();
        config.Validate();
        Config = config;

        if (api.Side == EnumAppSide.Server)
        {
            api.StoreModConfig(config, "papermakingpapyrus.json");
        }
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        var knifeTag = api.CollectibleTagRegistry.CreateTagSet(PapyrusConstants.KnifeTag);
        var attached = 0;

        foreach (var item in api.World.Items)
        {
            if (item?.Code == null ||
                !item.Tags.Overlaps(knifeTag) ||
                item.GetCollectibleBehavior<CollectibleBehaviorPapyrusTopCutting>(true) != null)
            {
                continue;
            }

            var behavior = new CollectibleBehaviorPapyrusTopCutting(item);
            behavior.Initialize(new JsonObject(new JObject()));
            behavior.OnLoaded(api);
            item.CollectibleBehaviors = [.. item.CollectibleBehaviors, behavior];
            attached++;
        }

        api.Logger.Notification(
            "[Papermaking: Papyrus] Prepared {0} tagged knife item type(s) for papyrus cutting.",
            attached);
    }
}
