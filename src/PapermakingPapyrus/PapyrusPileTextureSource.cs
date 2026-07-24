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
        ItemStack? weight)
    {
        this.capi = capi;
        textures = new Dictionary<string, AssetLocation>
        {
            ["papyrus"] = new(PapyrusConstants.Domain, "item/resource/papyrusstrips-soaked"),
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
        if (stack?.Collectible?.Code == null)
        {
            return new AssetLocation("game:block/wood/planks/oak");
        }

        var wood = stack.Collectible.Code.Path.Split('-').Last();
        return new AssetLocation(stack.Collectible.Code.Domain, $"block/wood/planks/{wood}");
    }

    private AssetLocation ResolveStone(ItemStack? stack)
    {
        if (stack?.Collectible?.Code == null)
        {
            return new AssetLocation("game:block/stone/rock/granite");
        }

        var rock = stack.Collectible.Code.Path.Split('-').Last();
        return new AssetLocation(stack.Collectible.Code.Domain, $"block/stone/rock/{rock}");
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
