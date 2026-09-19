using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace FACore;

public sealed class BlockEntityFADecodingTable : BlockEntity
{
    public const float HoldSeconds = 3f;
    private const double CooldownHours = 5d;
    private const float DyeLitresPerAttempt = 0.2f;
    private const int MaxFailureCount = 4;
    private const string DropStateKey = "facoreDecodingTableState";
    private static readonly AssetLocation CoreSchematicCode = new("facore", "fa-core-schematic");
    private static readonly AssetLocation ParchmentCode = new("game", "paper-parchment");
    private static readonly AssetLocation InkAndQuillCode = new("game", "inkandquill");
    private static readonly AssetLocation DecodingSoundAsset = new("facore", "sounds/decodingstation/decoding.ogg");
    private static readonly AssetLocation DecodingSound = new("facore", "sounds/decodingstation/decoding");
    private static readonly string[] SlotTreeKeys = ["decodingStack", "coreSchematicStack", "bowlStack", "storedQuillStack"];
    private static readonly string[] SlotActions = ["Decoding", "Lectern", "Bowl", "Quill"];
    // Parchment and decoded schematics use perpendicular native model planes, so they need
    // independent poses even though both occupy the decoding surface.
    private static readonly SlotPose DecodingParchmentPose = new(0.85f, -40f, 180f, 0f, 0f, 0f, -0.72f, -0.07f);
    private static readonly SlotPose DecodedSchematicPose = new(0.8f, 47f, 0f, 0f, 0f, 0f, -0.75f, -0.03f);

    // The current provisional model supplies point-like interaction anchors. Keep the remaining
    // item-facing choices together here; station yaw is composed at render time for every orientation.
    private static readonly SlotPose[] SlotPoses =
    [
        // Scale, RotationX, RotationY, RotationZ, FacingYaw, OffsetX, OffsetY, OffsetZ
        DecodingParchmentPose,
        new(1f, 80f, 0f, 0f, 0f, 0f, -0.87f, 0f),
        new(0.34f, 0f, 0f, 0f, 0f, 0f, -0.12f, 0f),
        new(0.34f, 0f, 28f, 0f, 0f, 0f, -0.17f, 0f)
    ];

    private readonly ItemStack?[] slots = new ItemStack?[4];
    private DecodingRenderSnapshot renderSnapshot = DecodingRenderSnapshot.Empty;
    private int failureCount;
    private double retryAllowedAtTotalHours;
    private long relevantInputRevision;
    private long nextSessionToken;
    private ActiveSession? activeSession;
    private ItemStack?[]? pendingBreakRecovery;
    private bool breakHandled;
    private bool serverHasOutputs;
    private bool soundAssetAvailable;

    private enum SlotRole
    {
        Decoding,
        Lectern,
        Bowl,
        Quill
    }

    private readonly record struct SlotPose(
        float MaxSize,
        float PitchDegrees,
        float YawDegrees,
        float RollDegrees,
        float FacingYawDegrees,
        float OffsetX,
        float OffsetY,
        float OffsetZ);

    private sealed record ActiveSession(
        string PlayerUid,
        long Token,
        long Revision,
        ItemStack Parchment,
        ItemStack CoreSchematic,
        ItemStack Bowl);

    private sealed class DecodingRenderSnapshot
    {
        public static readonly DecodingRenderSnapshot Empty = new();
        public ItemStack?[] Stacks { get; init; } = new ItemStack?[4];
        public TextureAtlasPosition? BowlContentTexture { get; init; }
        public int BowlContentTextureSubId { get; init; }
    }

    public int ChancePercent => ChanceForFailureCount(failureCount);
    public bool IsCoolingDown => Api?.World?.Calendar != null && Api.World.Calendar.TotalHours < retryAllowedAtTotalHours;
    public bool HasOutputPool => serverHasOutputs;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        if (api.Side == EnumAppSide.Server)
        {
            serverHasOutputs = DecodingSchematicRegistry.Count > 0;
            soundAssetAvailable = api.Assets.TryGet(DecodingSoundAsset, true) != null;
            if (Block is BlockFAStation station) station.EnsureStationStructure(api.World, Pos);
        }
        else
        {
            PublishRenderSnapshot();
        }
    }

    public static bool IsInkAndQuill(ItemStack? stack) => stack?.Collectible?.Code?.Equals(InkAndQuillCode) == true;

    public bool CanClientBegin(IPlayer player, string actionName)
    {
        return actionName == "Decoding"
            && IsInkAndQuill(player.InventoryManager?.ActiveHotbarSlot?.Itemstack)
            && !IsCoolingDown
            && serverHasOutputs
            && ValidateIngredients(out _);
    }

    public bool TryBegin(IPlayer player, string actionName, out long token, out string failure)
    {
        using var languageScope = FaText.ForPlayer(player);
        token = 0;
        failure = "";
        if (Api.Side != EnumAppSide.Server || actionName != "Decoding") return false;
        if (activeSession != null)
        {
            failure = FaText.GetKey("facore:decoding-busy");
            return false;
        }
        if (!Api.World.Claims.TryAccess(player, Pos, EnumBlockAccessFlags.Use))
        {
            failure = FaText.GetKey("protected-blockinteract");
            return false;
        }
        if (!IsInkAndQuill(player.InventoryManager?.ActiveHotbarSlot?.Itemstack))
        {
            failure = FaText.GetKey("facore:decoding-missing-tool");
            return false;
        }
        if (IsCoolingDown)
        {
            failure = FaText.GetKey("facore:decoding-cooldown");
            return false;
        }
        serverHasOutputs = DecodingSchematicRegistry.Count > 0;
        if (!serverHasOutputs)
        {
            failure = FaText.GetKey("facore:decoding-no-results");
            return false;
        }
        if (!ValidateIngredients(out failure)) return false;

        token = ++nextSessionToken;
        activeSession = new ActiveSession(
            player.PlayerUID,
            token,
            relevantInputRevision,
            slots[(int)SlotRole.Decoding]!.Clone(),
            slots[(int)SlotRole.Lectern]!.Clone(),
            slots[(int)SlotRole.Bowl]!.Clone());
        if (soundAssetAvailable) Api.World.PlaySoundAt(DecodingSound, Pos, 0.5, null, true, 16f, 1f);
        return true;
    }

    public bool CanContinue(IPlayer player, long token)
    {
        using var languageScope = FaText.ForPlayer(player);
        ActiveSession? session = activeSession;
        return session != null
            && session.Token == token
            && session.PlayerUid == player.PlayerUID
            && session.Revision == relevantInputRevision
            && IsInkAndQuill(player.InventoryManager?.ActiveHotbarSlot?.Itemstack)
            && Api.World.Claims.TryAccess(player, Pos, EnumBlockAccessFlags.Use)
            && InputsMatch(session)
            && !IsCoolingDown;
    }

    public bool TryComplete(IPlayer player, long token, double elapsedSeconds)
    {
        using var languageScope = FaText.ForPlayer(player);
        ActiveSession? session = activeSession;
        if (Api.Side != EnumAppSide.Server
            || session == null
            || elapsedSeconds < HoldSeconds
            || !CanContinue(player, token))
        {
            Cancel(player.PlayerUID, token);
            return false;
        }

        ItemStack[] outputPool = DecodingSchematicRegistry.GetSnapshot();
        if (outputPool.Length == 0 || outputPool.Any(stack => !IsEligibleOutput(stack)) || !ValidateIngredients(out _))
        {
            Cancel(player.PlayerUID, token);
            return false;
        }

        int chance = ChanceForFailureCount(failureCount);
        bool success = chance >= 100 || Api.World.Rand.Next(100) < chance;
        ItemStack? output = null;
        if (success)
        {
            output = outputPool[Api.World.Rand.Next(outputPool.Length)].Clone();
            output.StackSize = 1;
            if (!IsEligibleOutput(output))
            {
                Cancel(player.PlayerUID, token);
                return false;
            }
        }

        if (!TryConsumeAttemptDye())
        {
            Cancel(player.PlayerUID, token);
            Notify(player, FaText.GetKey("facore:decoding-insufficient-dye"));
            return false;
        }

        activeSession = null;
        if (success)
        {
            slots[(int)SlotRole.Decoding] = output;
            slots[(int)SlotRole.Lectern] = null;
            failureCount = 0;
            retryAllowedAtTotalHours = 0;
            relevantInputRevision++;
        }
        else
        {
            failureCount = Math.Min(MaxFailureCount, failureCount + 1);
            retryAllowedAtTotalHours = Api.World.Calendar.TotalHours + CooldownHours;
        }

        MarkTableDirty();
        Notify(player, FaText.GetKey(success ? "facore:decoding-success" : "facore:decoding-failure"));
        return true;
    }

    public void Cancel(string playerUid, long token)
    {
        if (activeSession?.PlayerUid == playerUid && activeSession.Token == token)
        {
            activeSession = null;
        }
    }

    public void HandleElementInteraction(IPlayer player, string actionName)
    {
        using var languageScope = FaText.ForPlayer(player);
        int index = Array.IndexOf(SlotActions, actionName);
        if (index < 0 || Api.Side != EnumAppSide.Server) return;
        if (!Api.World.Claims.TryAccess(player, Pos, EnumBlockAccessFlags.Use))
        {
            Notify(player, FaText.GetKey("protected-blockinteract"));
            return;
        }

        ItemSlot? activeSlot = player.InventoryManager?.ActiveHotbarSlot;
        ItemStack? held = activeSlot?.Itemstack;
        if (held == null)
        {
            RetrieveSlot(player, (SlotRole)index);
            return;
        }

        if (slots[index] != null)
        {
            Notify(player, FaText.GetKey("facore:decoding-slot-occupied"));
            return;
        }
        if (!CanStore((SlotRole)index, held))
        {
            Notify(player, FaText.GetKey("facore:decoding-wrong-item"));
            return;
        }

        ItemStack? inserted = activeSlot!.TakeOut(1);
        activeSlot.MarkDirty();
        if (inserted == null) return;
        inserted.StackSize = 1;
        slots[index] = inserted;
        if ((SlotRole)index != SlotRole.Quill) InvalidateRelevantInputs();
        MarkTableDirty();
    }

    public string DescribeState()
    {
        var builder = new StringBuilder();
        builder.Append(DescribeInsight());
        AppendCooldownTooltip(builder);
        return builder.ToString();
    }

    public string DescribeElementState(string actionName)
    {
        int index = Array.IndexOf(SlotActions, actionName);
        if (index < 0) return "";
        ItemStack? stack = slots[index];
        if (stack == null)
        {
            if (actionName == "Decoding") return WithCooldownTooltip(FaText.GetKey("facore:decoding-tooltip-empty-surface"));
            if (actionName == "Lectern") return WithCooldownTooltip(FaText.GetKey("facore:decoding-tooltip-empty-lectern"));
        }
        string value = stack == null ? FaText.Get("empty") : !IsSafeForPresentation(stack) ? FaText.Get("unavailable item") : stack.GetName();
        string description = actionName == "Decoding" ? FaText.Get("Decoding surface: {0}\n{1}", value, DescribeInsight()) : FaText.Get("{0}: {1}", FaText.Get(actionName), value);
        return WithCooldownTooltip(description);
    }

    private string WithCooldownTooltip(string description)
    {
        var builder = new StringBuilder(description);
        AppendCooldownTooltip(builder);
        return builder.ToString();
    }

    private void AppendCooldownTooltip(StringBuilder builder)
    {
        if (!IsCoolingDown) return;
        double remainingHours = Math.Max(0, retryAllowedAtTotalHours - Api.World.Calendar.TotalHours);
        int wholeHours = (int)Math.Floor(remainingHours);
        int minutes = (int)Math.Ceiling((remainingHours - wholeHours) * 60);
        if (minutes >= 60)
        {
            wholeHours++;
            minutes = 0;
        }

        string time = wholeHours <= 0
            ? $"{Math.Max(1, minutes)}m"
            : minutes <= 0 ? $"{wholeHours}h" : $"{wholeHours}h {minutes}m";
        if (builder.Length > 0) builder.AppendLine();
        builder.Append(FaText.GetKey("facore:decoding-tired-for", time));
    }

    private string DescribeInsight()
    {
        string stageLangCode = ChancePercent switch
        {
            20 => "facore:decoding-insight-20",
            40 => "facore:decoding-insight-40",
            60 => "facore:decoding-insight-60",
            80 => "facore:decoding-insight-80",
            _ => "facore:decoding-insight-100"
        };
        return FaText.GetKey("facore:decoding-insight", FaText.GetKey(stageLangCode));
    }

    public WorldInteraction[] GetInteractionHelp(string actionName)
    {
        int index = Array.IndexOf(SlotActions, actionName);
        if (index < 0) return [];
        if (slots[index] != null)
        {
            if (actionName == "Decoding" && IsBlankParchment(slots[index]))
            {
                string attemptHelp = !IsActualBowl(slots[(int)SlotRole.Bowl]) ? "facore:decoding-missing-bowl"
                    : !IsExactItem(slots[(int)SlotRole.Lectern], CoreSchematicCode) ? "facore:decoding-missing-core"
                    : !serverHasOutputs ? "facore:decoding-no-results"
                    : "facore:decoding-help-hold";
                return
                [
                    new WorldInteraction { ActionLangCode = attemptHelp, MouseButton = EnumMouseButton.Right },
                    new WorldInteraction { ActionLangCode = "facore:decoding-help-take", MouseButton = EnumMouseButton.Right, RequireFreeHand = true }
                ];
            }
            return [new WorldInteraction { ActionLangCode = "facore:decoding-help-take", MouseButton = EnumMouseButton.Right, RequireFreeHand = true }];
        }
        return [new WorldInteraction { ActionLangCode = "facore:decoding-help-insert-" + actionName.ToLowerInvariant(), MouseButton = EnumMouseButton.Right }];
    }

    public void WriteDropState(ItemStack tableDrop)
    {
        var state = new TreeAttribute();
        state.SetInt("version", 1);
        state.SetInt("failureCount", Math.Clamp(failureCount, 0, MaxFailureCount));
        state.SetDouble("retryAllowedAtTotalHours", double.IsFinite(retryAllowedAtTotalHours) ? retryAllowedAtTotalHours : 0);
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack? recovery = pendingBreakRecovery?[i];
            if (recovery == null && pendingBreakRecovery == null)
            {
                ItemStack? stack = slots[i];
                if (stack != null && !IsSafelyMaterialized(stack)) recovery = stack;
            }
            if (recovery != null) state.SetItemstack("recovery" + i, recovery.Clone());
        }
        tableDrop.Attributes[DropStateKey] = state;
    }

    public override void OnBlockPlaced(ItemStack byItemStack = null!)
    {
        base.OnBlockPlaced(byItemStack);
        breakHandled = false;
        pendingBreakRecovery = null;
        ITreeAttribute? state = byItemStack?.Attributes?.GetTreeAttribute(DropStateKey);
        if (state == null) return;
        failureCount = Math.Clamp(state.GetInt("failureCount"), 0, MaxFailureCount);
        double deadline = state.GetDouble("retryAllowedAtTotalHours");
        retryAllowedAtTotalHours = double.IsFinite(deadline) ? deadline : 0;
        bool restoredRelevantInput = false;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) continue;
            ItemStack? recovered = state.GetItemstack("recovery" + i);
            if (recovered != null)
            {
                recovered.ResolveBlockOrItem(Api.World);
                slots[i] = recovered;
                restoredRelevantInput |= i != (int)SlotRole.Quill;
            }
        }
        if (restoredRelevantInput) relevantInputRevision++;
        MarkTableDirty();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        for (int i = 0; i < slots.Length; i++) SetOrRemoveStack(tree, SlotTreeKeys[i], slots[i]);
        tree.SetInt("failureCount", Math.Clamp(failureCount, 0, MaxFailureCount));
        tree.SetDouble("retryAllowedAtTotalHours", double.IsFinite(retryAllowedAtTotalHours) ? retryAllowedAtTotalHours : 0);
        tree.SetLong("relevantInputRevision", relevantInputRevision);
        tree.SetBool("hasDecodingOutputs", serverHasOutputs);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack? stack = tree.GetItemstack(SlotTreeKeys[i]);
            stack?.ResolveBlockOrItem(worldForResolving);
            slots[i] = stack;
        }
        failureCount = Math.Clamp(tree.GetInt("failureCount"), 0, MaxFailureCount);
        double deadline = tree.GetDouble("retryAllowedAtTotalHours");
        retryAllowedAtTotalHours = double.IsFinite(deadline) ? deadline : 0;
        relevantInputRevision = tree.GetLong("relevantInputRevision");
        serverHasOutputs = tree.GetBool("hasDecodingOutputs");
        activeSession = null;
        if (worldForResolving.Side == EnumAppSide.Client)
        {
            PublishRenderSnapshot();
            worldForResolving.BlockAccessor.MarkBlockDirty(Pos);
        }
    }

    public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
    {
        base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
        foreach (ItemStack? stack in slots)
        {
            if (stack?.Collectible == null) continue;
            if (stack.Class == EnumItemClass.Block) blockIdMapping[stack.Id] = stack.Collectible.Code;
            else itemIdMapping[stack.Id] = stack.Collectible.Code;
            stack.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(stack), blockIdMapping, itemIdMapping);
        }
    }

    public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
    {
        base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
        for (int i = 0; i < slots.Length; i++) slots[i]?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForNewMappings);
        failureCount = 0;
        retryAllowedAtTotalHours = 0;
        activeSession = null;
    }

    public override void OnBlockBroken(IPlayer byPlayer)
    {
        if (Api.Side == EnumAppSide.Server && !breakHandled)
        {
            breakHandled = true;
            activeSession = null;
            pendingBreakRecovery = new ItemStack?[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                ItemStack? stack = slots[i];
                if (stack == null) continue;
                if (CanSafelyMaterialize(stack)) Api.World.SpawnItemEntity(stack.Clone(), Pos.ToVec3d().Add(0.5, 0.8, 0.5));
                else pendingBreakRecovery[i] = stack.Clone();
            }
            // Block.GetDrops runs after this callback. Keep the references alive until block removal
            // so its versioned table-item payload can retain only stacks that could not be materialized.
        }
        base.OnBlockBroken(byPlayer);
    }

    public override void OnBlockRemoved()
    {
        activeSession = null;
        pendingBreakRecovery = null;
        Volatile.Write(ref renderSnapshot, DecodingRenderSnapshot.Empty);
        base.OnBlockRemoved();
    }

    public override void OnBlockUnloaded()
    {
        activeSession = null;
        pendingBreakRecovery = null;
        Volatile.Write(ref renderSnapshot, DecodingRenderSnapshot.Empty);
        base.OnBlockUnloaded();
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        if (dsc.Length > 0) dsc.AppendLine();
        dsc.Append(DescribeState());
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        bool skip = base.OnTesselation(mesher, tessThreadTesselator);
        DecodingRenderSnapshot snapshot = Volatile.Read(ref renderSnapshot);
        IReadOnlyList<StationElementZone> zones = Block is BlockFAStation station ? station.GetElementZones() : [];
        for (int i = 0; i < snapshot.Stacks.Length; i++)
        {
            ItemStack? stack = snapshot.Stacks[i];
            StationElementZone? zone = zones.FirstOrDefault(candidate => candidate.ActionName == SlotActions[i]);
            if (stack?.Collectible == null || zone == null) continue;
            MeshData? mesh = CreateStackMesh(tessThreadTesselator, stack, i == (int)SlotRole.Bowl ? snapshot : null);
            if (mesh == null) continue;
            SlotPose pose = i == (int)SlotRole.Decoding && IsEligibleOutput(stack)
                ? DecodedSchematicPose
                : SlotPoses[i];
            AlignMesh(mesh, zone.StationBox, pose, Block.Shape.rotateY);
            mesher.AddMeshData(mesh, 1);
        }
        return skip;
    }

    private bool ValidateIngredients(out string failure)
    {
        if (!IsBlankParchment(slots[(int)SlotRole.Decoding]))
        {
            failure = FaText.GetKey("facore:decoding-missing-parchment");
            return false;
        }
        if (!IsActualBowl(slots[(int)SlotRole.Bowl]))
        {
            failure = FaText.GetKey("facore:decoding-missing-bowl");
            return false;
        }
        if (!HasEnoughValidDye(slots[(int)SlotRole.Bowl]))
        {
            failure = FaText.GetKey("facore:decoding-insufficient-dye");
            return false;
        }
        if (!IsExactItem(slots[(int)SlotRole.Lectern], CoreSchematicCode))
        {
            failure = FaText.GetKey("facore:decoding-missing-core");
            return false;
        }
        failure = "";
        return true;
    }

    private bool InputsMatch(ActiveSession session)
    {
        return SameStack(slots[(int)SlotRole.Decoding], session.Parchment)
            && SameStack(slots[(int)SlotRole.Lectern], session.CoreSchematic)
            && SameStack(slots[(int)SlotRole.Bowl], session.Bowl);
    }

    private bool SameStack(ItemStack? current, ItemStack captured) => current != null
        && current.StackSize == 1
        && current.Equals(Api.World, captured);

    private static int ChanceForFailureCount(int failures) => 20 * (Math.Clamp(failures, 0, MaxFailureCount) + 1);

    private static bool CanStore(SlotRole role, ItemStack stack)
    {
        if (stack.StackSize <= 0) return false;
        return role switch
        {
            SlotRole.Decoding => IsBlankParchmentKind(stack),
            SlotRole.Lectern => stack.Collectible?.Code?.Equals(CoreSchematicCode) == true,
            SlotRole.Bowl => IsActualBowlKind(stack),
            SlotRole.Quill => IsInkAndQuill(stack),
            _ => false
        };
    }

    private static bool IsExactItem(ItemStack? stack, AssetLocation code) => stack?.StackSize == 1 && stack.Collectible?.Code?.Equals(code) == true;

    private static bool IsEligibleOutput(ItemStack? stack) =>
        stack?.StackSize == 1
        && stack.Collectible?.Code is AssetLocation code
        && code.Domain != "facore"
        && code.Domain.StartsWith("fa", StringComparison.Ordinal)
        && stack.Collectible.Attributes?["faDecodingResult"].AsBool(false) == true;

    private static bool IsBlankParchment(ItemStack? stack)
    {
        return stack?.StackSize == 1
            && IsBlankParchmentKind(stack);
    }

    private static bool IsBlankParchmentKind(ItemStack stack) =>
        stack.Collectible?.Code?.Equals(ParchmentCode) == true
        && (stack.Attributes == null || stack.Attributes.Count == 0);

    private static bool IsActualBowl(ItemStack? stack)
    {
        return stack?.StackSize == 1
            && IsActualBowlKind(stack);
    }

    private static bool IsActualBowlKind(ItemStack stack) =>
        stack.Block is BlockLiquidContainerBase
        && stack.Block.Attributes?["mealContainer"].AsBool(false) == true;

    private static bool HasEnoughValidDye(ItemStack? stack)
    {
        if (!IsActualBowl(stack) || stack!.Collectible is not ILiquidInterface liquidInterface) return false;
        ItemStack? content = liquidInterface.GetContent(stack);
        if (content == null || content.Collectible == null) return false;
        AssetLocation code = content.Collectible.Code;
        if (code.Domain != "game" || code.Path is not ("dye-black" or "dye-gray")) return false;
        WaterTightContainableProps? props = BlockLiquidContainerBase.GetContainableProps(content);
        return props != null && content.StackSize >= Math.Ceiling(DyeLitresPerAttempt * props.ItemsPerLitre);
    }

    private bool TryConsumeAttemptDye()
    {
        ItemStack? bowl = slots[(int)SlotRole.Bowl];
        if (!HasEnoughValidDye(bowl)
            || bowl!.Collectible is not ILiquidInterface liquidInterface
            || bowl.Collectible is not ILiquidSource liquidSource
            || liquidInterface.GetContent(bowl) is not ItemStack content
            || BlockLiquidContainerBase.GetContainableProps(content) is not WaterTightContainableProps props)
        {
            return false;
        }

        int amount = (int)Math.Ceiling(DyeLitresPerAttempt * props.ItemsPerLitre);
        ItemStack? consumed = liquidSource.TryTakeContent(bowl, amount);
        return consumed?.StackSize == amount;
    }

    private bool CanSafelyMaterialize(ItemStack stack)
    {
        if (stack.StackSize <= 0) return false;
        if (stack.Collectible == null && !stack.ResolveBlockOrItem(Api.World)) return false;
        if (IsActualBowlKind(stack) && stack.Collectible is ILiquidInterface liquidInterface)
        {
            ItemStack? content = liquidInterface.GetContent(stack);
            if (content != null && content.Collectible == null && !content.ResolveBlockOrItem(Api.World)) return false;
        }
        return stack.Collectible != null;
    }

    private static bool IsSafeForPresentation(ItemStack stack)
    {
        if (stack.Collectible == null) return false;
        if (!IsActualBowlKind(stack) || stack.Collectible is not ILiquidInterface liquidInterface) return true;
        ItemStack? content = liquidInterface.GetContent(stack);
        return content == null || content.Collectible != null;
    }

    private static bool IsSafelyMaterialized(ItemStack stack) => stack.StackSize > 0 && IsSafeForPresentation(stack);

    private void RetrieveSlot(IPlayer player, SlotRole role)
    {
        int index = (int)role;
        ItemStack? stack = slots[index];
        if (stack == null) return;
        if (!CanSafelyMaterialize(stack))
        {
            Notify(player, FaText.GetKey("facore:decoding-invalid-stored"));
            return;
        }

        slots[index] = null;
        if (role != SlotRole.Quill) InvalidateRelevantInputs();
        if (!player.InventoryManager.TryGiveItemstack(stack, true)) Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.8, 0.5));
        MarkTableDirty();
    }

    private void InvalidateRelevantInputs()
    {
        relevantInputRevision++;
        activeSession = null;
    }

    private void MarkTableDirty()
    {
        if (Api.Side == EnumAppSide.Client) PublishRenderSnapshot();
        MarkDirty(true);
        Api.World.BlockAccessor.MarkBlockDirty(Pos);
        if (Block is BlockFAStation station)
        {
            foreach (BlockPos proxy in station.GetStationProxyPositions(Pos)) Api.World.BlockAccessor.MarkBlockDirty(proxy);
        }
    }

    private void PublishRenderSnapshot()
    {
        if (Api?.Side != EnumAppSide.Client) return;
        ItemStack?[] cloned = slots.Select(stack => stack?.Clone()).ToArray();
        TextureAtlasPosition? texturePosition = null;
        int textureSubId = 0;
        ItemStack? bowl = cloned[(int)SlotRole.Bowl];
        if (Api is ICoreClientAPI capi
            && bowl?.Collectible is ILiquidInterface liquidInterface
            && liquidInterface.GetContent(bowl) is ItemStack content
            && BlockLiquidContainerBase.GetContainableProps(content)?.Texture is CompositeTexture sourceTexture)
        {
            CompositeTexture texture = sourceTexture.Clone();
            texture.Bake(capi.Assets);
            capi.BlockTextureAtlas.GetOrInsertTexture(texture, out textureSubId, out texturePosition, 0.005f);
        }
        Volatile.Write(ref renderSnapshot, new DecodingRenderSnapshot
        {
            Stacks = cloned,
            BowlContentTexture = texturePosition,
            BowlContentTextureSubId = textureSubId
        });
    }

    private MeshData? CreateStackMesh(ITesselatorAPI tessellator, ItemStack stack, DecodingRenderSnapshot? bowlSnapshot)
    {
        MeshData mesh;
        if (stack.Item != null)
        {
            if (Api is not ICoreClientAPI capi || stack.Item.Shape?.Base == null) return null;
            AssetLocation shapeLocation = stack.Item.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape? shape = Shape.TryGet(Api, shapeLocation);
            if (shape == null) return null;
            ITexPositionSource fallback = tessellator.GetTextureSource(Block, 0, false);
            tessellator.TesselateShape(
                "facore-deciphering-item",
                shape,
                out mesh,
                new ItemBlockAtlasTextureSource(capi, fallback, stack.Item),
                new Vec3f());
        }
        else if (stack.Block != null)
        {
            tessellator.TesselateBlock(stack.Block, out mesh);
            if (bowlSnapshot?.BowlContentTexture != null)
            {
                Shape? contentShape = Shape.TryGet(Api, new AssetLocation("game", "shapes/block/clay/bowl-liquidcontents.json"));
                if (contentShape != null)
                {
                    var source = new MappedTextureSource(
                        tessellator.GetTextureSource(stack.Block, 0, false),
                        "content",
                        bowlSnapshot.BowlContentTexture);
                    tessellator.TesselateShape("facore-decoding-bowl-content", contentShape, out MeshData contentMesh, source, new Vec3f());
                    if (contentMesh != null) mesh.AddMeshData(contentMesh);
                }
            }
        }
        else return null;
        ForceOpaqueRenderPass(mesh);
        return mesh?.VerticesCount > 0 ? mesh : null;
    }

    private static void ForceOpaqueRenderPass(MeshData mesh)
    {
        if (mesh.RenderPassesAndExtraBits == null || mesh.RenderPassesAndExtraBits.Length == 0) mesh.WithRenderpasses();
        if (mesh.RenderPassesAndExtraBits != null) Array.Fill(mesh.RenderPassesAndExtraBits, (short)EnumChunkRenderPass.Opaque);
    }

    private static void AlignMesh(MeshData mesh, Cuboidf anchor, SlotPose pose, float stationYawDegrees)
    {
        if (!TryGetBounds(mesh, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ)) return;
        var origin = new Vec3f((minX + maxX) / 2f, (minY + maxY) / 2f, (minZ + maxZ) / 2f);
        float largest = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
        float scale = largest > 0 ? pose.MaxSize / largest : 1f;
        mesh.Scale(origin, scale, scale, scale);
        mesh.Rotate(
            origin,
            pose.PitchDegrees * GameMath.DEG2RAD,
            pose.YawDegrees * GameMath.DEG2RAD,
            pose.RollDegrees * GameMath.DEG2RAD);
        mesh.Rotate(origin, 0f, (stationYawDegrees + pose.FacingYawDegrees) * GameMath.DEG2RAD, 0f);
        if (!TryGetBounds(mesh, out minX, out minY, out minZ, out maxX, out maxY, out maxZ)) return;
        float stationYaw = stationYawDegrees * GameMath.DEG2RAD;
        float offsetX = pose.OffsetX * GameMath.Cos(stationYaw) + pose.OffsetZ * GameMath.Sin(stationYaw);
        float offsetZ = -pose.OffsetX * GameMath.Sin(stationYaw) + pose.OffsetZ * GameMath.Cos(stationYaw);
        mesh.Translate(
            (anchor.X1 + anchor.X2 - minX - maxX) / 2f + offsetX,
            anchor.Y2 + pose.OffsetY - minY,
            (anchor.Z1 + anchor.Z2 - minZ - maxZ) / 2f + offsetZ);
    }

    private static bool TryGetBounds(MeshData mesh, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ)
    {
        minX = minY = minZ = float.MaxValue;
        maxX = maxY = maxZ = float.MinValue;
        if (mesh.xyz == null || mesh.VerticesCount == 0) return false;
        for (int i = 0; i < mesh.VerticesCount * 3; i += 3)
        {
            minX = Math.Min(minX, mesh.xyz[i]); maxX = Math.Max(maxX, mesh.xyz[i]);
            minY = Math.Min(minY, mesh.xyz[i + 1]); maxY = Math.Max(maxY, mesh.xyz[i + 1]);
            minZ = Math.Min(minZ, mesh.xyz[i + 2]); maxZ = Math.Max(maxZ, mesh.xyz[i + 2]);
        }
        return true;
    }

    private static void SetOrRemoveStack(ITreeAttribute tree, string key, ItemStack? stack)
    {
        if (stack == null) tree.RemoveAttribute(key);
        else tree.SetItemstack(key, stack);
    }

    private static void Notify(IPlayer player, string message)
    {
        if (player is IServerPlayer serverPlayer) serverPlayer.SendIngameError("facore-decodingtable", message);
    }

    private sealed class MappedTextureSource(ITexPositionSource fallback, string textureCode, TextureAtlasPosition? texture) : ITexPositionSource
    {
        public TextureAtlasPosition? this[string requestedTextureCode] => requestedTextureCode == textureCode && texture != null
            ? texture
            : fallback[requestedTextureCode];

        public Size2i? AtlasSize => fallback.AtlasSize;
    }

    private sealed class ItemBlockAtlasTextureSource : ITexPositionSource
    {
        private readonly ITexPositionSource fallback;
        private readonly Dictionary<string, TextureAtlasPosition> textures = new(StringComparer.Ordinal);

        public ItemBlockAtlasTextureSource(ICoreClientAPI capi, ITexPositionSource fallback, Item item)
        {
            this.fallback = fallback;
            if (item.Textures == null) return;
            foreach ((string code, CompositeTexture itemTexture) in item.Textures)
            {
                CompositeTexture texture = itemTexture.Clone();
                texture.Alpha = 255;
                texture.Bake(capi.Assets);
                capi.BlockTextureAtlas.GetOrInsertTexture(texture, out _, out TextureAtlasPosition position, 0.005f);
                textures[code] = position;
            }
        }

        public TextureAtlasPosition? this[string code] => textures.TryGetValue(code, out TextureAtlasPosition? texture)
            ? texture
            : textures.TryGetValue("all", out TextureAtlasPosition? allTexture) ? allTexture : fallback[code];

        public Size2i? AtlasSize => fallback.AtlasSize;
    }
}
