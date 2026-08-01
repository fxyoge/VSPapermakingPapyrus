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
    private const int MaxDryingCatchUpSamples = 32;
    private readonly InventoryGeneric inventory = new(11, null, null);
    private int stripCount;
    private int boardCount;
    private bool hasWeight;
    private double dryingProgress;
    private double lastProcessedTotalHours = -1;
    private int visualBand;
    private ClimateCondition? climateScratch;
    private long dryingListenerId;
    private PapyrusPileTags tags = null!;

    public override InventoryBase Inventory => inventory;

    public override string InventoryClassName => "papyruspile";

    public float Orientation { get; set; }

    public PapyrusPileSnapshot Snapshot =>
        CreateSnapshot();

    public double DryingProgress => dryingProgress;

    public bool IsDry => hasWeight && dryingProgress >= 1;

    public float SelectionHeight => hasWeight ? 0.48f : boardCount > 0 ? 0.22f : 0.035f + stripCount * 0.012f;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        tags = (PapyrusPileTags)api.ObjectCache[PapyrusConstants.PileTagsCacheKey];
        if (api.Side == EnumAppSide.Server)
        {
            ProcessDrying();
            StartDryingListenerIfNeeded();
        }
    }

    public bool AddInitialStrip(ItemSlot source, bool consume = true)
    {
        if (stripCount != 0 || !StoreOne(source, inventory[0], consume))
        {
            return false;
        }

        stripCount = 1;
        StartDryingListenerIfNeeded();
        MarkDirty(true);
        return true;
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
            PlaySound(PapyrusConstants.CollectSound);
            return true;
        }

        var consume = player.WorldData.CurrentGameMode != EnumGameMode.Creative;
        var action = Snapshot.NextAction(
            stack == null,
            HasTag(stack, tags.SoakedStrip),
            HasTag(stack, tags.PressBoard),
            HasTag(stack, tags.PressWeight));

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
            PlaySound(action switch
            {
                PapyrusPileAction.AddBoard or PapyrusPileAction.RemoveBoard =>
                    PapyrusConstants.BoardSound,
                PapyrusPileAction.AddWeight => PapyrusConstants.WeightSound,
                _ => PapyrusConstants.StripSound
            });

            if (stripCount == 0)
            {
                Api.World.BlockAccessor.SetBlock(0, Pos);
            }
            else
            {
                StartDryingListenerIfNeeded();
                MarkDirty(true);
            }
        }

        return true;
    }

    public override void OnBlockBroken(IPlayer byPlayer = null!)
    {
        StopDryingListener();
        if (Api.Side == EnumAppSide.Server)
        {
            if (IsDry)
            {
                ResolveFinishedSheet();
            }

            inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.2, 0.5));
        }

        base.OnBlockBroken(byPlayer);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        Orientation = tree.GetFloat("orientation");
        var storedProgress = tree.GetDouble("dryingProgress");
        dryingProgress = double.IsFinite(storedProgress) ? Math.Max(storedProgress, 0) : 0;
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
        if (IsDry)
        {
            dsc.AppendLine(Lang.Get("papermakingpapyrus:pile-ready"));
        }
        else if (Snapshot.RequiresResoaking)
        {
            dsc.AppendLine(Lang.Get("papermakingpapyrus:pile-resoak-required"));
        }
        else if (hasWeight)
        {
            var state = visualBand switch
            {
                0 => "soaked",
                1 => "damp",
                _ => "mostlydry"
            };
            dsc.AppendLine(Lang.Get($"papermakingpapyrus:pile-{state}"));
        }
        else
        {
            dsc.AppendLine(Lang.Get("papermakingpapyrus:pile-strips", stripCount));
            dsc.AppendLine(Lang.Get("papermakingpapyrus:pile-planks", boardCount));
            dsc.AppendLine(Lang.Get("papermakingpapyrus:pile-weight", 0));
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

        var stripElements = Enumerable.Range(1, stripCount)
            .Select(index => $"root/strip{index}")
            .ToArray();
        var pressElements = Enumerable.Range(1, boardCount)
            .Select(index => $"root/board{index}")
            .Concat(hasWeight ? ["root/weight"] : [])
            .ToArray();
        var textureSource = new PapyrusPileTextureSource(
            capi,
            inventory[8].Itemstack,
            inventory[9].Itemstack,
            inventory[10].Itemstack,
            visualBand);
        var rotation = new Vec3f(0, Orientation * GameMath.RAD2DEG, 0);

        if (!hasWeight)
        {
            tesselator.TesselateShape(
                "papyrus pile",
                shape,
                out var mesh,
                textureSource,
                rotation,
                selectiveElements: stripElements.Concat(pressElements).ToArray());
            mesher.AddMeshData(mesh);
            return true;
        }

        tesselator.TesselateShape(
            "papyrus pile strips",
            shape,
            out var stripMesh,
            textureSource,
            rotation,
            selectiveElements: stripElements);
        tesselator.TesselateShape(
            "papyrus pile press",
            shape,
            out var pressMesh,
            textureSource,
            rotation,
            selectiveElements: pressElements);

        var scaleY = visualBand switch
        {
            0 => 1f,
            1 => 0.94f,
            2 => 0.88f,
            _ => 0.82f
        };
        if (TryGetVerticalBounds(stripMesh, out var stripBottom, out var stripTop))
        {
            var compression = PapyrusPileCompression.Calculate(stripBottom, stripTop, scaleY);
            stripMesh.Scale(
                new Vec3f(0.5f, compression.ScaleOriginY, 0.5f),
                1,
                compression.ScaleY,
                1);
            pressMesh.Translate(0, compression.PressOffsetY, 0);
        }

        mesher.AddMeshData(stripMesh);
        mesher.AddMeshData(pressMesh);
        return true;
    }

    private static bool TryGetVerticalBounds(
        MeshData mesh,
        out float bottom,
        out float top)
    {
        bottom = float.PositiveInfinity;
        top = float.NegativeInfinity;
        for (var index = 1; index < mesh.GetVerticesCount() * 3; index += 3)
        {
            var y = mesh.xyz[index];
            if (!float.IsFinite(y))
            {
                return false;
            }

            bottom = Math.Min(bottom, y);
            top = Math.Max(top, y);
        }

        return float.IsFinite(bottom) && float.IsFinite(top) && top > bottom;
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
        StartDryingListenerIfNeeded();
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

        var stored = consume ? source.TakeOut(1) : source.Itemstack.Clone();
        if (stored == null)
        {
            return false;
        }

        stored.StackSize = 1;
        target.Itemstack = stored;
        if (consume)
        {
            source.MarkDirty();
        }
        target.MarkDirty();
        return target.Itemstack != null;
    }

    private static bool HasTag(ItemStack? stack, TagSet tag) =>
        stack != null && stack.Collectible.Tags.Overlaps(tag);

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

    private void StartDryingListenerIfNeeded()
    {
        if (Api.Side != EnumAppSide.Server ||
            IsDry ||
            (!hasWeight && !HasWetUnpressedStrips()) ||
            dryingListenerId != 0)
        {
            return;
        }

        dryingListenerId = RegisterGameTickListener(
            OnDryingTick,
            PapermakingPapyrusModSystem.ServerConfig.DryingRefreshIntervalMilliseconds);
    }

    private void StopDryingListener()
    {
        if (dryingListenerId == 0 || Api == null)
        {
            return;
        }

        UnregisterGameTickListener(dryingListenerId);
        dryingListenerId = 0;
    }

    private void ProcessDrying()
    {
        if (Api.Side != EnumAppSide.Server)
        {
            return;
        }

        if (!hasWeight)
        {
            ProcessIncompleteStripTransitions();
            return;
        }

        if (IsDry)
        {
            StopDryingListener();
            return;
        }

        var now = Api.World.Calendar.TotalHours;
        if (lastProcessedTotalHours < 0 || now < lastProcessedTotalHours)
        {
            lastProcessedTotalHours = now;
            MarkDirty();
            return;
        }

        var next = AdvanceAcrossCalendar(dryingProgress, lastProcessedTotalHours, now);
        lastProcessedTotalHours = now;
        var nextBand = PapyrusDrying.VisualBand(next);
        dryingProgress = next;
        if (nextBand != visualBand)
        {
            visualBand = nextBand;
            MarkDirty(true);
        }
        else if (IsDry)
        {
            MarkDirty();
        }

        if (IsDry)
        {
            StopDryingListener();
        }
    }

    private void ProcessIncompleteStripTransitions()
    {
        var requiredResoakingBefore = HasDryUnpressedStrips();
        var changed = false;
        for (var i = 0; i < stripCount; i++)
        {
            var slot = inventory[i];
            var codeBefore = slot.Itemstack?.Collectible?.Code;
            slot.Itemstack?.Collectible.UpdateAndGetTransitionStates(Api.World, slot);
            changed |= codeBefore != slot.Itemstack?.Collectible?.Code;
        }

        var requiredResoakingAfter = HasDryUnpressedStrips();
        if (changed || requiredResoakingBefore != requiredResoakingAfter)
        {
            MarkDirty(true);
        }

        if (!HasWetUnpressedStrips())
        {
            StopDryingListener();
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
            PapermakingPapyrusModSystem.ServerConfig.DryingHours,
            MaxDryingCatchUpSamples,
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
        if (ResolveFinishedSheet())
        {
            var parchment = inventory[0].TakeOutWhole();
            if (parchment != null && !player.InventoryManager.TryGiveItemstack(parchment, true))
            {
                Api.World.SpawnItemEntity(parchment, Pos.ToVec3d().Add(0.5, 0.4, 0.5));
            }
        }

        inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.2, 0.5));
        Api.World.BlockAccessor.SetBlock(0, Pos);
    }

    private bool ResolveFinishedSheet()
    {
        var outputCode = PapermakingPapyrusModSystem.ActiveFinishedSheetCode;
        var parchment = Api.World.GetItem(new AssetLocation(outputCode));
        if (parchment == null)
        {
            PapermakingPapyrusModSystem.Logger?.Error(
                "Cannot resolve completed pile at {0}: collectible {1} is missing. Returning stored inputs instead.",
                Pos,
                outputCode);
            return false;
        }

        for (var i = 0; i < 8; i++)
        {
            inventory[i].Itemstack = null;
            inventory[i].MarkDirty();
        }

        inventory[0].Itemstack = new ItemStack(parchment);
        inventory[0].MarkDirty();
        return true;
    }

    private PapyrusPileSnapshot CreateSnapshot()
    {
        var requiresResoaking = HasDryUnpressedStrips();
        return new PapyrusPileSnapshot(
            stripCount,
            boardCount,
            hasWeight,
            IsDry
                ? PapyrusPileWorkState.Dry
                : PapyrusPileSnapshot.DeriveState(
                    stripCount,
                    boardCount,
                    hasWeight,
                    requiresResoaking),
            requiresResoaking);
    }

    private bool HasDryUnpressedStrips() =>
        !hasWeight &&
        Enumerable.Range(0, stripCount).Any(
            index =>
                HasTag(inventory[index].Itemstack, tags.PapyrusStrip) &&
                !HasTag(inventory[index].Itemstack, tags.SoakedStrip));

    private bool HasWetUnpressedStrips() =>
        !hasWeight &&
        Enumerable.Range(0, stripCount).Any(
            index => HasTag(
                inventory[index].Itemstack,
                tags.AirDryingPapyrusStrip));

    public void PlayPlacementSound() =>
        PlaySound(PapyrusConstants.StripSound);

    private void PlaySound(string code) =>
        Api.World.PlaySoundAt(
            new AssetLocation(code),
            Pos,
            0,
            null,
            randomizePitch: true,
            range: 32,
            volume: 1);
}
