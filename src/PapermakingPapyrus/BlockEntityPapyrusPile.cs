using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace PapermakingPapyrus;

public sealed class BlockEntityPapyrusPile : BlockEntityContainer
{
    private readonly InventoryGeneric inventory = new(11, null, null);
    private int stripCount;
    private int boardCount;
    private bool hasWeight;

    public override InventoryBase Inventory => inventory;

    public override string InventoryClassName => "papyruspile";

    public float Orientation { get; set; }

    public PapyrusPileSnapshot Snapshot =>
        new(stripCount, boardCount, hasWeight, PapyrusPileSnapshot.DeriveState(stripCount, boardCount, hasWeight));

    public float SelectionHeight => hasWeight ? 0.48f : boardCount > 0 ? 0.22f : 0.035f + stripCount * 0.012f;

    public void AddInitialStrip(ItemSlot source)
    {
        if (stripCount != 0 || !MoveOne(source, inventory[0]))
        {
            return;
        }

        stripCount = 1;
        Changed();
    }

    public bool Interact(IPlayer player)
    {
        if (Api.Side != EnumAppSide.Server)
        {
            return true;
        }

        var active = player.InventoryManager.ActiveHotbarSlot;
        var stack = active.Itemstack;
        var action = Snapshot.NextAction(
            stack == null,
            HasTag(stack, PapyrusConstants.SoakedStripTag),
            HasTag(stack, PapyrusConstants.PressBoardTag),
            HasTag(stack, PapyrusConstants.PressWeightTag));

        var changed = action switch
        {
            PapyrusPileAction.AddStrip => Add(active, stripCount, ref stripCount),
            PapyrusPileAction.AddBoard => Add(active, 8 + boardCount, ref boardCount),
            PapyrusPileAction.AddWeight => AddWeight(active),
            PapyrusPileAction.RemoveStrip => Remove(player, --stripCount),
            PapyrusPileAction.RemoveBoard => Remove(player, 8 + --boardCount),
            _ => false
        };

        if (changed)
        {
            Changed();
        }

        return true;
    }

    public override void OnBlockBroken(IPlayer byPlayer = null!)
    {
        if (Api.Side == EnumAppSide.Server)
        {
            inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.2, 0.5));
        }

        base.OnBlockRemoved();
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        Orientation = tree.GetFloat("orientation");
        RecountAndRepair();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetInt("contractVersion", 1);
        tree.SetFloat("orientation", Orientation);
        tree.SetInt("stripCount", stripCount);
        tree.SetInt("boardCount", boardCount);
        tree.SetBool("hasWeight", hasWeight);
        tree.SetString("workState", Snapshot.WorkState.ToString().ToLowerInvariant());
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        if (stripCount < 8)
        {
            dsc.AppendLine(Lang.Get("papermakingpapyrus:pile-layer", stripCount));
        }
        else if (boardCount < 2)
        {
            dsc.AppendLine(Lang.Get(
                boardCount == 0
                    ? "papermakingpapyrus:pile-needs-two-boards"
                    : "papermakingpapyrus:pile-needs-one-board"));
        }
        else
        {
            dsc.AppendLine(Lang.Get(
                hasWeight
                    ? "papermakingpapyrus:pile-pressing"
                    : "papermakingpapyrus:pile-needs-weight"));
        }
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        if (Api is not ICoreClientAPI capi)
        {
            return true;
        }

        var shape = Shape.TryGet(Api, new AssetLocation(
            PapyrusConstants.Domain,
            "shapes/block/papyruspile.json"));
        if (shape == null)
        {
            return true;
        }

        var elements = Enumerable.Range(1, stripCount)
            .Select(index => $"root/strip{index}")
            .Concat(Enumerable.Range(1, boardCount).Select(index => $"root/board{index}"))
            .Concat(hasWeight ? ["root/weight"] : [])
            .ToArray();
        tesselator.TesselateShape(
            "papyrus pile",
            shape,
            out var mesh,
            new PapyrusPileTextureSource(capi, inventory[8].Itemstack, inventory[9].Itemstack, inventory[10].Itemstack),
            new Vec3f(0, Orientation * GameMath.RAD2DEG, 0),
            selectiveElements: elements);
        mesher.AddMeshData(mesh);
        return true;
    }

    private bool Add(ItemSlot source, int targetIndex, ref int count)
    {
        if (!MoveOne(source, inventory[targetIndex]))
        {
            return false;
        }

        count++;
        return true;
    }

    private bool AddWeight(ItemSlot source)
    {
        if (!MoveOne(source, inventory[10]))
        {
            return false;
        }

        hasWeight = true;
        return true;
    }

    private bool Remove(IPlayer player, int slotIndex)
    {
        var stack = inventory[slotIndex].TakeOutWhole();
        if (stack == null)
        {
            return false;
        }

        if (!player.InventoryManager.TryGiveItemstack(stack, true))
        {
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.4, 0.5));
        }

        return true;
    }

    private static bool MoveOne(ItemSlot source, ItemSlot target)
    {
        if (source.Itemstack == null || target.Itemstack != null)
        {
            return false;
        }

        target.Itemstack = source.TakeOut(1);
        source.MarkDirty();
        target.MarkDirty();
        return target.Itemstack != null;
    }

    private bool HasTag(ItemStack? stack, string tag) =>
        stack != null && stack.Collectible.Tags.Overlaps(
            Api.CollectibleTagRegistry.CreateTagSet(tag));

    private void RecountAndRepair()
    {
        stripCount = Enumerable.Range(0, 8).TakeWhile(i => !inventory[i].Empty).Count();
        boardCount = stripCount == 8
            ? Enumerable.Range(8, 2).TakeWhile(i => !inventory[i].Empty).Count()
            : 0;
        hasWeight = stripCount == 8 && boardCount == 2 && !inventory[10].Empty;
    }

    private void Changed()
    {
        MarkDirty(true);
        Api.World.BlockAccessor.MarkBlockDirty(Pos);
    }
}
