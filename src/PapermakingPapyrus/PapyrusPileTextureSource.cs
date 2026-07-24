using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PapermakingPapyrus;

internal sealed class PapyrusPileTextureSource : ITexPositionSource
{
    private readonly ICoreClientAPI capi;
    private readonly Dictionary<string, AssetLocation> textures;

    public PapyrusPileTextureSource(
        ICoreClientAPI capi,
        ItemStack? board1,
        ItemStack? board2,
        ItemStack? weight,
        int visualBand)
    {
        this.capi = capi;
        textures = new Dictionary<string, AssetLocation>
        {
            ["papyrus"] = new(PapyrusConstants.Domain, visualBand switch
            {
                1 => "item/resource/papyrussheet-damp",
                2 => "item/resource/papyrussheet-mostlydry",
                3 => "item/resource/papyrussheet-dry",
                _ => "item/resource/papyrussheet-soaked"
            }),
            ["board1"] = ResolveBoard(board1),
            ["board2"] = ResolveBoard(board2),
            ["stone"] = ResolveStone(weight)
        };
    }

    public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

    public TextureAtlasPosition this[string textureCode] => GetOrInsert(
        textures.TryGetValue(textureCode, out var location)
            ? location
            : new AssetLocation("game:block/unknown"));

    private AssetLocation ResolveBoard(ItemStack? stack)
    {
        return ResolveCollectibleTexture(
            stack,
            new AssetLocation("game:block/wood/planks/oak1"));
    }

    private AssetLocation ResolveStone(ItemStack? stack)
    {
        return ResolveCollectibleTexture(
            stack,
            new AssetLocation("game:block/stone/rock/granite1"));
    }

    private static AssetLocation ResolveCollectibleTexture(
        ItemStack? stack,
        AssetLocation fallback)
    {
        var textures = stack?.Collectible switch
        {
            Item item => item.Textures,
            Block block => block.Textures,
            _ => null
        };
        if (textures == null)
        {
            return fallback;
        }

        if (textures.TryGetValue("all", out var texture))
        {
            return texture.Baked.BakedName;
        }

        return textures.Values.FirstOrDefault()?.Baked.BakedName ?? fallback;
    }

    private TextureAtlasPosition GetOrInsert(AssetLocation texture)
    {
        var atlas = capi.BlockTextureAtlas;
        var position = atlas[texture];
        if (position != null)
        {
            return position;
        }

        var asset = capi.Assets.TryGet(
            texture.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
        if (asset != null &&
            atlas.GetOrInsertTexture(
                texture,
                out _,
                out position,
                () => asset.ToBitmap(capi)))
        {
            return position!;
        }

        PapermakingPapyrusModSystem.Logger?.Warning(
            "Pile texture {0} is unavailable; using the documented visual fallback.",
            texture);
        return atlas.UnknownTexturePosition!;
    }
}
