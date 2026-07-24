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
    private const double DryingSampleIntervalHours = 3;
    private readonly InventoryGeneric inventory = new(11, null, null);
    private int stripCount;
    private int boardCount;
    private bool hasWeight;
    private double dryingProgress;
    private double lastProcessedTotalHours = -1;
    private int visualBand;
    private ClimateCondition? climateScratch;

    public override InventoryBase Inventory => inventory;

    public override string InventoryClassName => "papyruspile";

    public float Orientation { get; set; }

    public PapyrusPileSnapshot Snapshot =>
        new(stripCount, boardCount, hasWeight, IsDry
            ? PapyrusPileWorkState.Dry
            : PapyrusPileSnapshot.DeriveState(stripCount, boardCount, hasWeight));

    public double DryingProgress => dryingProgress;

    public bool IsDry => hasWeight && dryingProgress >= 1;

    public float SelectionHeight => hasWeight ? 0.48f : boardCount > 0 ? 0.22f : 0.035f + stripCount * 0.012f;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        if (api.Side == EnumAppSide.Server)
        {
            RegisterGameTickListener(OnDryingTick, 2000);
            ProcessDrying();
        }
    }

    public void AddInitialStrip(ItemSlot source, bool consume = true)
    {
        if (stripCount != 0 || !StoreOne(source, inventory[0], consume))
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
        if (IsDry && stack == null)
        {
            CollectFinished(player);
            return true;
        }

        var consume = player.WorldData.CurrentGameMode != EnumGameMode.Creative;
        var action = Snapshot.NextAction(
            stack == null,
            HasTag(stack, PapyrusConstants.SoakedStripTag),
            HasTag(stack, PapyrusConstants.PressBoardTag),
            HasTag(stack, PapyrusConstants.PressWeightTag));

        var changed = action switch
        {
            PapyrusPileAction.AddStrip => Add(active, stripCount, ref stripCount, consume),
            PapyrusPileAction.AddBoard => Add(active, 8 + boardCount, ref boardCount, consume),
            PapyrusPileAction.AddWeight => AddWeight(active, consume),
            PapyrusPileAction.RemoveStrip => RemoveLast(player, 0, ref stripCount),
            PapyrusPileAction.RemoveBoard => RemoveLast(player, 8, ref boardCount),
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
            if (IsDry)
            {
                ResolveFinishedSheet();
            }

            inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.2, 0.5));
        }

        base.OnBlockRemoved();
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        Orientation = tree.GetFloat("orientation");
        dryingProgress = Math.Clamp(tree.GetDouble("dryingProgress"), 0, 1);
        lastProcessedTotalHours = tree.GetDouble("lastProcessedTotalHours", -1);
        visualBand = PapyrusDrying.VisualBand(dryingProgress);
        RecountAndRepair();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetInt("contractVersion", 2);
        tree.SetFloat("orientation", Orientation);
        tree.SetInt("stripCount", stripCount);
        tree.SetInt("boardCount", boardCount);
        tree.SetBool("hasWeight", hasWeight);
        tree.SetString("workState", Snapshot.WorkState.ToString().ToLowerInvariant());
        tree.SetDouble("dryingProgress", dryingProgress);
        tree.SetDouble("lastProcessedTotalHours", lastProcessedTotalHours);
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
        else if (IsDry)
        {
            dsc.AppendLine(Lang.Get("papermakingpapyrus:pile-ready"));
        }
        else
        {
            if (hasWeight)
            {
                var remaining = PapyrusDrying.RemainingHours(
                    dryingProgress,
                    PapermakingPapyrusModSystem.Config.DryingHours);
                var remainingMinutes = (int)Math.Ceiling(remaining * 60);
                dsc.AppendLine(Lang.Get(
                    "papermakingpapyrus:pile-pressing",
                    remainingMinutes / 60,
                    remainingMinutes % 60));
            }
            else
            {
                dsc.AppendLine(Lang.Get("papermakingpapyrus:pile-needs-weight"));
            }
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
            new PapyrusPileTextureSource(
                capi,
                inventory[8].Itemstack,
                inventory[9].Itemstack,
                inventory[10].Itemstack,
                visualBand),
            new Vec3f(0, Orientation * GameMath.RAD2DEG, 0),
            selectiveElements: elements);
        if (hasWeight)
        {
            var scaleY = visualBand switch
            {
                0 => 1f,
                1 => 0.94f,
                2 => 0.88f,
                _ => 0.82f
            };
            mesh.Scale(new Vec3f(0.5f, 0, 0.5f), 1, scaleY, 1);
        }
        mesher.AddMeshData(mesh);
        return true;
    }

    private bool Add(ItemSlot source, int targetIndex, ref int count, bool consume)
    {
        if (!StoreOne(source, inventory[targetIndex], consume))
        {
            return false;
        }

        count++;
        return true;
    }

    private bool AddWeight(ItemSlot source, bool consume)
    {
        if (!StoreOne(source, inventory[10], consume))
        {
            return false;
        }

        hasWeight = true;
        lastProcessedTotalHours = Api.World.Calendar.TotalHours;
        return true;
    }

    private bool RemoveLast(IPlayer player, int offset, ref int count)
    {
        if (count <= 0)
        {
            return false;
        }

        var slotIndex = offset + count - 1;
        var stack = inventory[slotIndex].TakeOutWhole();
        if (stack == null)
        {
            return false;
        }

        count--;
        if (!player.InventoryManager.TryGiveItemstack(stack, true))
        {
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.4, 0.5));
        }

        return true;
    }

    private static bool StoreOne(ItemSlot source, ItemSlot target, bool consume)
    {
        if (source.Itemstack == null || target.Itemstack != null)
        {
            return false;
        }

        target.Itemstack = consume ? source.TakeOut(1) : source.Itemstack.Clone();
        target.Itemstack.StackSize = 1;
        if (consume)
        {
            source.MarkDirty();
        }
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
        if (!hasWeight)
        {
            dryingProgress = 0;
            lastProcessedTotalHours = -1;
            visualBand = 0;
        }
    }

    private void OnDryingTick(float deltaTime) => ProcessDrying();

    private void ProcessDrying()
    {
        if (Api.Side != EnumAppSide.Server || !hasWeight || IsDry)
        {
            return;
        }

        var now = Api.World.Calendar.TotalHours;
        if (lastProcessedTotalHours < 0 || now < lastProcessedTotalHours)
        {
            lastProcessedTotalHours = now;
            MarkDirty();
            return;
        }

        if (now - lastProcessedTotalHours < DryingSampleIntervalHours)
        {
            return;
        }

        var next = AdvanceAcrossCalendar(dryingProgress, lastProcessedTotalHours, now);
        lastProcessedTotalHours = now;
        var nextBand = PapyrusDrying.VisualBand(next);
        if (!next.Equals(dryingProgress) || nextBand != visualBand)
        {
            dryingProgress = next;
            visualBand = nextBand;
            Changed();
        }
        else
        {
            MarkDirty();
        }
    }

    private double AdvanceAcrossCalendar(double progress, double fromHours, double toHours)
    {
        var calendar = Api.World.Calendar;
        climateScratch ??= Api.World.BlockAccessor.GetClimateAt(
            Pos,
            EnumGetClimateMode.WorldGenValues,
            0);
        var sampler = new PapyrusDryingSampler(
            Api.World.BlockAccessor,
            Pos,
            climateScratch,
            calendar.HoursPerDay);
        return CalendarProgress.Accumulate(
            progress,
            fromHours,
            toHours,
            new CalendarProgressPolicy(
                PapermakingPapyrusModSystem.Config.DryingHours,
                DryingSampleIntervalHours,
                calendar.DaysPerYear * calendar.HoursPerDay),
            ref sampler);
    }

    private readonly record struct PapyrusDryingSampler(
        IBlockAccessor BlockAccessor,
        BlockPos Pos,
        ClimateCondition Climate,
        float HoursPerDay) : ICalendarActivitySampler
    {
        public bool IsActiveAt(double totalHours)
        {
            BlockAccessor.GetClimateAt(
                Pos,
                Climate,
                EnumGetClimateMode.ForSuppliedDate_TemperatureOnly,
                totalHours / HoursPerDay);
            return Climate.Temperature > 0;
        }
    }

    private void CollectFinished(IPlayer player)
    {
        ResolveFinishedSheet();
        foreach (var slot in inventory.Where(slot => !slot.Empty))
        {
            var stack = slot.TakeOutWhole();
            if (stack != null && !player.InventoryManager.TryGiveItemstack(stack, true))
            {
                Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.4, 0.5));
            }
        }

        Api.World.BlockAccessor.SetBlock(0, Pos);
    }

    private bool ResolveFinishedSheet()
    {
        var paper = Api.World.GetItem(new AssetLocation(PapyrusConstants.FinishedPapyrusCode));
        if (paper == null)
        {
            PapermakingPapyrusModSystem.Logger?.Error(
                "Cannot resolve completed papyrus at {0}: collectible {1} is missing. Returning stored inputs instead.",
                Pos,
                PapyrusConstants.FinishedPapyrusCode);
            return false;
        }

        for (var i = 0; i < 8; i++)
        {
            inventory[i].Itemstack = null;
            inventory[i].MarkDirty();
        }

        inventory[0].Itemstack = new ItemStack(paper);
        inventory[0].MarkDirty();
        return true;
    }

    private void Changed()
    {
        MarkDirty(true);
        Api.World.BlockAccessor.MarkBlockDirty(Pos);
    }
}
