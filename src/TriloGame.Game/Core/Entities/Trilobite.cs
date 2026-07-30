using TriloGame.Game.Audio;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.Traits;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Entities;

public sealed partial class Trilobite : Creature, IInventoryCarrier
{
    private readonly TrilobiteBuildingAssignment _buildingAssignment = new();
    private readonly List<string> _manualMineTileKeys = [];
    private bool _fleeingToQueen;
    private MineTileResult? _pendingMiningStrikeResult;
    private bool _depositInventoryBeforeRole;
    private int _builderRetryAfterTick;

    public Trilobite(string name, GridPoint location, GameSession session)
        : base(name, location, session, CreatureMovementProfile.Trilobite)
    {
        Inventory = new Core.Economy.Inventory();
        InventoryCapacity = GameConstants.TrilobiteCarryCapacity;
        BuilderWorkRate = 5;
        TraitState = new TrilobiteTraitState(TrilobiteTraits.CreateRandomStarterTraits(GameConstants.TrilobiteStarterTraitCount));
    }

    public Core.Economy.Inventory Inventory { get; }

    public int InventoryCapacity { get; }

    public TrilobiteTraitState TraitState { get; }

    public Building? AssignedBuilding => _buildingAssignment.Building;

    public string? PendingMineTileKey { get; private set; }
    public string? PendingManualMineSelectionKey { get; private set; }

    public MiningClaim? ActiveMiningClaim { get; private set; }

    public MinerState MinerState { get; private set; } = MinerState.Idle;

    public FarmerState FarmerState { get; private set; } = FarmerState.Idle;

    public BuilderState BuilderState { get; private set; } = BuilderState.Idle;

    public FighterState FighterState { get; private set; } = FighterState.Idle;

    public WorkerRoleFailureReason LastRoleFailure { get; private set; }

    internal void AcceptMiningClaim(MiningClaim claim, string? manualSelectionKey = null)
    {
        ActiveMiningClaim = claim;
        PendingMineTileKey = claim.TileKey;
        PendingMinePath = null;
        PendingManualMineSelectionKey = manualSelectionKey;
    }

    private List<GridPoint>? PendingMinePath { get; set; }

    public Enemy? FighterTarget { get; private set; }

    public string? FighterPathMode { get; private set; }

    public MiningPost? BuilderSourcePost { get; private set; }

    public Building? BuilderSourceBuilding { get; private set; }

    public int BuilderWorkRate { get; }

    internal MiningPostSelectionMetrics? LastMiningPostSelectionMetrics { get; private set; }

    public bool HasInventory() => Inventory.HasItems;

    public int GetInventorySpace() => System.Math.Max(0, InventoryCapacity - Inventory.Amount);

    public int AddToInventory(ResourceName resourceType, int amount) => Inventory.Add(resourceType, amount, InventoryCapacity);

    public int RemoveFromInventory(int amount) => Inventory.Remove(amount);

    public int RemoveFromInventory(ResourceName resourceType, int amount) => Inventory.Remove(resourceType, amount);

    public void ClearInventory() => Inventory.Clear();

    public bool ChangeAssignment(string assignment)
    {
        var normalizedAssignment = assignment.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAssignment) ||
            string.Equals(Assignment, normalizedAssignment, StringComparison.Ordinal))
        {
            return false;
        }

        var oldRole = Role;
        var newRole = CreatureRoleNames.Parse(normalizedAssignment);
        if (!string.Equals(normalizedAssignment, TrilobiteRoles.Miner, StringComparison.Ordinal))
        {
            ClearManualMineOrders();
        }

        _depositInventoryBeforeRole = oldRole == CreatureRole.Miner &&
                                      newRole != CreatureRole.Miner &&
                                      HasInventory();

        if (GetAssignedBuilding() is not null)
        {
            ReleaseAssignedBuilding();
        }

        Assignment = normalizedAssignment;
        RestartBehavior();
        return true;
    }

    public void SetTraits(IEnumerable<TrilobiteTrait> traits)
    {
        TraitState.SetTraits(traits);
    }

    public override void CleanupBeforeRemoval(object? source = null)
    {
        if (Health <= 0 && TraitState.HasTrait(TrilobiteTrait.Explosive))
        {
            Cave?.TriggerDeathExplosion(this, source);
        }

        ClearTaskQueue();
        ClearManualMineOrders();
        ClearFighterTarget();
        FighterPathMode = null;
        _fleeingToQueen = false;
        _depositInventoryBeforeRole = false;
        ResetRoleStates();
        ReleaseAssignedBuilding(restoreHostedCreatureLocomotion: false);
        PendingMineTileKey = null;
        PendingMinePath = null;
        _pendingMiningStrikeResult = null;
        ActiveMiningClaim = null;
        ClearInventory();
    }

    protected override bool QueueBehavior()
    {
        return Role switch
        {
            CreatureRole.Miner or CreatureRole.Farmer or CreatureRole.Builder or CreatureRole.Fighter =>
                EnqueueTask(new CreatureTask(CreatureTaskKind.RunRole)),
            _ => StartUnassignedBehavior()
        };
    }

    protected override bool ExecuteTask(CreatureTask task)
    {
        return task.Kind switch
        {
            CreatureTaskKind.RunRole => RunRole(),
            _ => base.ExecuteTask(task)
        };
    }

    public bool RunRole()
    {
        if (_depositInventoryBeforeRole && Role != CreatureRole.Miner && HasInventory())
        {
            return AdvanceRoleChangeInventoryDeposit();
        }

        _depositInventoryBeforeRole = false;
        return Role switch
        {
            CreatureRole.Miner => AdvanceMinerRole(),
            CreatureRole.Farmer => AdvanceFarmerRole(),
            CreatureRole.Builder => AdvanceBuilderRole(),
            CreatureRole.Fighter => AdvanceFighterRole(),
            _ => StartUnassignedBehavior()
        };
    }

    internal bool RunRoleState(MinerState state)
    {
        MinerState = state;
        LastRoleFailure = WorkerRoleFailureReason.None;
        return RunRole();
    }

    internal bool RunRoleState(FarmerState state, GridPoint? harvestTarget = null)
    {
        FarmerState = state;
        _farmerHarvestTarget = harvestTarget;
        LastRoleFailure = WorkerRoleFailureReason.None;
        return RunRole();
    }

    internal bool RunRoleState(BuilderState state)
    {
        BuilderState = state;
        LastRoleFailure = WorkerRoleFailureReason.None;
        return RunRole();
    }

    internal bool RunRoleState(FighterState state, bool preferAssignedStation = true)
    {
        FighterState = state;
        _fighterPreferAssignedStation = preferAssignedStation;
        LastRoleFailure = WorkerRoleFailureReason.None;
        return RunRole();
    }

    private void ResetRoleStates()
    {
        MinerState = MinerState.Idle;
        FarmerState = FarmerState.Idle;
        BuilderState = BuilderState.Idle;
        FighterState = FighterState.Idle;
        _farmerHarvestTarget = null;
        _fighterPreferAssignedStation = true;
        LastRoleFailure = WorkerRoleFailureReason.None;
    }

    private bool QueueRole(WorkerRoleFailureReason failure = WorkerRoleFailureReason.None, bool result = true)
    {
        LastRoleFailure = failure;
        EnqueueTask(new CreatureTask(CreatureTaskKind.RunRole));
        return result;
    }

    private bool QueueMinerState(MinerState state, WorkerRoleFailureReason failure = WorkerRoleFailureReason.None, bool result = true)
    {
        MinerState = state;
        return QueueRole(failure, result);
    }

    private bool QueueFarmerState(FarmerState state, WorkerRoleFailureReason failure = WorkerRoleFailureReason.None, bool result = true)
    {
        FarmerState = state;
        return QueueRole(failure, result);
    }

    private bool QueueFarmerHarvest(GridPoint target, bool result = true)
    {
        _farmerHarvestTarget = target;
        FarmerState = FarmerState.Harvest;
        return QueueRole(result: result);
    }

    private bool QueueBuilderState(BuilderState state, WorkerRoleFailureReason failure = WorkerRoleFailureReason.None, bool result = true)
    {
        BuilderState = state;
        return QueueRole(failure, result);
    }

    private bool QueueFighterState(FighterState state, WorkerRoleFailureReason failure = WorkerRoleFailureReason.None, bool result = true)
    {
        FighterState = state;
        if (state is FighterState.SelectStation or FighterState.ReturnToStation)
        {
            _fighterPreferAssignedStation = true;
        }

        return QueueRole(failure, result);
    }

    internal void SetCombatState(FighterState state)
    {
        FighterState = state;
    }

    internal bool PreferAssignedFighterStation => _fighterPreferAssignedStation;

    private bool AdvanceMinerRole()
    {
        return MinerState switch
        {
            MinerState.Idle => AdvanceMinerIdle(),
            MinerState.SelectPost => AdvanceMinerSelectPost(),
            MinerState.AcquireClaim => AdvanceMinerAcquireClaim(),
            MinerState.MoveToClaim => AdvanceMinerMoveToClaim(),
            MinerState.MineClaim => AdvanceMinerMineClaim(),
            MinerState.DepositInventory => AdvanceMinerDepositInventory(),
            MinerState.WaitForWork => QueueMinerState(MinerState.Idle, WorkerRoleFailureReason.NoWork, result: false),
            MinerState.WaitForStorage => QueueMinerState(MinerState.Idle, WorkerRoleFailureReason.NoStorage, result: false),
            _ => QueueMinerState(MinerState.Idle, WorkerRoleFailureReason.TargetInvalid, result: false)
        };
    }

    private bool AdvanceBuilderRole()
    {
        return BuilderState switch
        {
            BuilderState.Idle => AdvanceBuilderIdle(),
            BuilderState.SelectScaffold => AdvanceBuilderSelectScaffold(),
            BuilderState.SelectSource => AdvanceBuilderSelectSource(),
            BuilderState.MoveToSource => AdvanceBuilderMoveToSource(),
            BuilderState.WithdrawMaterial => AdvanceBuilderWithdrawMaterial(),
            BuilderState.MoveToScaffold => AdvanceBuilderMoveToScaffold(),
            BuilderState.DepositMaterial => AdvanceBuilderDepositMaterial(),
            BuilderState.BuildScaffold => AdvanceBuilderBuildScaffold(),
            _ => QueueBuilderState(BuilderState.Idle, WorkerRoleFailureReason.TargetInvalid, result: false)
        };
    }

    private bool StartUnassignedBehavior()
    {
        StartUnassignedRoleBehavior();
        return true;
    }

    private bool AdvanceRoleChangeInventoryDeposit()
    {
        var post = SelectMiningPostForInventoryDeposit();
        if (post is null)
        {
            LastRoleFailure = WorkerRoleFailureReason.NoStorage;
            return false;
        }

        if (ReservedZone is not { Purpose: InteractionZonePurpose.ResourceTransfer } transferZone ||
            !ReferenceEquals(transferZone.Owner, post) ||
            !IsAtReservedInteractionSlot())
        {
            if (!NavigateToInteractionZone(post, InteractionZonePurpose.ResourceTransfer))
            {
                LastRoleFailure = WorkerRoleFailureReason.NoReachablePath;
                return false;
            }

            QueueRole();
            return true;
        }

        if (!TryDepositCarrierInventory(post, out _))
        {
            LastRoleFailure = WorkerRoleFailureReason.NoStorage;
            return false;
        }

        if (HasInventory())
        {
            QueueRole(WorkerRoleFailureReason.NoStorage, result: false);
            return false;
        }

        _depositInventoryBeforeRole = false;
        ReleaseInteractionReservation();
        return RunRole();
    }

    private bool TryDepositCarrierInventory(IResourceStorage storage, out int accepted)
    {
        accepted = 0;
        if (!HasInventory())
        {
            ReleaseInteractionReservation();
            return true;
        }

        SetActivity(CreatureActivity.Depositing);
        var index = 0;
        while (index < Inventory.ResourceTypeCount && storage.GetInventorySpace() > 0)
        {
            var resourceType = Inventory.GetResourceTypeAt(index);
            var carried = Inventory.GetAmount(resourceType);
            var deposited = storage.Deposit(resourceType, carried);
            if (deposited > 0)
            {
                RemoveFromInventory(resourceType, deposited);
                accepted += deposited;
            }

            if (Inventory.GetAmount(resourceType) > 0)
            {
                index++;
            }
        }

        return FinishCarrierInventoryDeposit(accepted);
    }

    private bool TryDepositCarrierInventory(Scaffolding scaffold, out int accepted)
    {
        accepted = 0;
        if (!HasInventory())
        {
            ReleaseInteractionReservation();
            return true;
        }

        SetActivity(CreatureActivity.Depositing);
        var resourceType = Inventory.Type!.Value;
        accepted = scaffold.Deposit(resourceType, Inventory.GetAmount(resourceType), this);
        if (accepted > 0)
        {
            RemoveFromInventory(resourceType, accepted);
        }

        return FinishCarrierInventoryDeposit(accepted);
    }

    private bool FinishCarrierInventoryDeposit(int accepted)
    {
        if (accepted > 0)
        {
            Session.RequestAudioCue(
                GameAudioCue.CreatureDeposit,
                Position,
                AudioCueRequest.CreatureEffectFootprintTiles);
            if (HasInventory())
            {
                SetActivity(CreatureActivity.WaitingForSlot);
            }
        }
        else
        {
            SetActivity(CreatureActivity.WaitingForSlot);
        }

        ReleaseInteractionReservation();
        return accepted > 0;
    }

    protected override bool CanUseIdleMovement => Role != CreatureRole.Enemy;

    // Keep every trilobite profession on the same idle algorithm while biasing it toward assigned work.
    protected override bool TryGetIdleAnchor(out WorldPoint anchor)
    {
        if (AssignedBuilding is { Location: not null } building && ReferenceEquals(building.Cave, Cave))
        {
            anchor = WorldPoint.FromGridPoint(building.GetCenter());
            return true;
        }

        return base.TryGetIdleAnchor(out anchor);
    }

    internal void StartUnassignedRoleBehavior()
    {
        ClearFighterTarget();
        FighterPathMode = null;
        ReleaseAssignedBuilding();
    }

    public bool IsMiner() => Role == CreatureRole.Miner;

    public bool IsFarmer() => Role == CreatureRole.Farmer;

    public bool IsBuilder() => Role == CreatureRole.Builder;

    public bool IsFighter() => Role == CreatureRole.Fighter;

    protected override bool TryInterruptQueuedTask()
    {
        if (!ShouldFleeFromNearbyEnemy())
        {
            if (_fleeingToQueen)
            {
                _fleeingToQueen = false;
                ClearTaskQueue();
            }

            TryLeaveScaffoldingWhileIdle();
            return false;
        }

        var queen = GetQueen();
        if (queen is null)
        {
            _fleeingToQueen = false;
            return false;
        }

        if (!_fleeingToQueen)
        {
            _fleeingToQueen = true;
            SetActivity(CreatureActivity.Fleeing);
            ReleaseAssignedBuilding();
            ClearTaskQueue();
            if (queen.CanBeFedAt(Location) || IsOnPassableBuildingTile(queen))
            {
                return true;
            }

            NavigateToBuilding(queen, true);
            return false;
        }

        if (queen.CanBeFedAt(Location) || IsOnPassableBuildingTile(queen))
        {
            SetActivity(CreatureActivity.Fleeing);
            ClearTaskQueue();
            return true;
        }

        if (QueuedTaskCount == 0)
        {
            ClearTaskQueue();
            NavigateToBuilding(queen, true);
        }

        return false;
    }

    protected override bool TryInterruptActiveMovement()
    {
        if (!IsFighter() ||
            (!Session.Danger && MovementCohort.GoalKind != MovementGoalKind.Combat))
        {
            return false;
        }

        return _combatAgentController.RefreshActivePursuit(this);
    }

    // Idle trilobites should step off scaffolding so finished builds can complete without prolonged blocking.
    private void TryLeaveScaffoldingWhileIdle()
    {
        if ((QueuedTaskCount > 0 && !(IsBuilder() && BuilderState == BuilderState.Idle)) ||
            Cave is null ||
            !IsLocomotionEnabled)
        {
            return;
        }

        var currentTile = Cave.GetTile(Location);
        if (currentTile?.Built is not Scaffolding)
        {
            return;
        }

        var path = Cave.BuildPathToNearestEmptyTile(Location);
        if (path is null || path.Count < 2)
        {
            return;
        }

        QueueMovePath(path);
    }

    private bool ShouldFleeFromNearbyEnemy()
    {
        if (Cave is null ||
            !Session.Danger ||
            Role is not (CreatureRole.Miner or CreatureRole.Builder or CreatureRole.Farmer))
        {
            return false;
        }

        var enemyDistance = Cave.GetBfsFieldValue("enemy", Location);
        return enemyDistance < GameConstants.WorkerEnemyFleeRadius;
    }

    public bool EnsureMinerState()
    {
        ClearFighterTarget();
        FighterPathMode = null;

        if (IsMiner())
        {
            if (GetAssignedBuilding() is not null && GetAssignedMiningPost() is null)
            {
                ReleaseAssignedBuilding();
            }

            return true;
        }

        if (GetAssignedMiningPost() is not null)
        {
            ReleaseAssignedBuilding();
        }

        StartCurrentRoleBehaviorIfNot(TrilobiteRoles.Miner);

        return false;
    }

    public bool EnsureFarmerState()
    {
        ClearFighterTarget();
        FighterPathMode = null;

        if (IsFarmer())
        {
            if (GetAssignedBuilding() is not null &&
                GetAssignedAlgaeFarm() is null &&
                GetAssignedRanch() is null)
            {
                ReleaseAssignedBuilding();
            }

            return true;
        }

        if (GetAssignedAlgaeFarm() is not null || GetAssignedRanch() is not null)
        {
            ReleaseAssignedBuilding();
        }

        StartCurrentRoleBehaviorIfNot(TrilobiteRoles.Farmer);

        return false;
    }

    public bool EnsureBuilderState()
    {
        ClearFighterTarget();
        FighterPathMode = null;

        if (IsBuilder())
        {
            if (GetAssignedBuilding() is not null && GetAssignedScaffolding() is null)
            {
                ReleaseAssignedBuilding();
            }

            return true;
        }

        if (GetAssignedScaffolding() is not null)
        {
            ReleaseAssignedBuilding();
        }
        else
        {
            ClearBuilderSourcePost();
        }

        StartCurrentRoleBehaviorIfNot(TrilobiteRoles.Builder);

        return false;
    }

    public bool EnsureFighterState()
    {
        if (IsFighter())
        {
            if (Cave is { } cave)
            {
                Session.Combat.BeginTick(cave);
            }

            if (GetAssignedBuilding() is not null && GetAssignedFighterStation() is null)
            {
                ReleaseAssignedBuilding();
            }

            return true;
        }

        ClearFighterTarget();
        FighterPathMode = null;
        if (GetAssignedFighterStation() is not null)
        {
            ReleaseAssignedBuilding();
        }

        StartCurrentRoleBehaviorIfNot(TrilobiteRoles.Fighter);

        return false;
    }

    private void StartCurrentRoleBehaviorIfNot(string role)
    {
        if (Role == CreatureRoleNames.Parse(role))
        {
            return;
        }

        QueueBehavior();
    }

    public IReadOnlyList<AlgaeFarm> GetAlgaeFarms()
    {
        return Cave?.GetAlgaeFarms() ?? [];
    }

    public Queen? GetQueen()
    {
        return Cave?.GetQueenBuilding();
    }

    public GridPoint? GetClosestPassableBuildingTile(Building building, GridPoint? startLocation = null)
    {
        var origin = startLocation ?? Location;
        GridPoint? bestTile = null;
        var bestDistance = int.MaxValue;

        foreach (var tile in building.TileArray)
        {
            if (!tile.CreatureFits())
            {
                continue;
            }

            var distance = GridPoint.SquaredDistance(origin, tile.Coordinates);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTile = tile.Coordinates;
            }
        }

        return bestTile;
    }

    public bool IsOnPassableBuildingTile(Building building, GridPoint? location = null)
    {
        var target = location ?? Location;
        foreach (var tile in building.TileArray)
        {
            if (tile.CreatureFits() && tile.Coordinates == target)
            {
                return true;
            }
        }

        return false;
    }

    public Building? GetAssignedBuilding() => AssignedBuilding;

    public AlgaeFarm? GetAssignedAlgaeFarm() => AssignedBuilding as AlgaeFarm;

    public Ranch? GetAssignedRanch() => AssignedBuilding as Ranch;

    public MiningPost? GetAssignedMiningPost() => AssignedBuilding as MiningPost;

    public StationBuilding? GetAssignedFighterStation() => AssignedBuilding as StationBuilding;

    public Barracks? GetAssignedBarracks() => GetAssignedFighterStation() as Barracks;

    public Scaffolding? GetAssignedScaffolding() => AssignedBuilding as Scaffolding;

    public void SetAssignedBuilding(Building? building)
    {
        if (!_buildingAssignment.IsAssignedTo(building))
        {
            ReleaseAssignedBuilding();
            _buildingAssignment.Set(building);
        }
    }

    public void ReleaseAssignedBuilding(bool restoreHostedCreatureLocomotion = true)
    {
        ClearBuilderSourcePost();
        if (AssignedBuilding is null)
        {
            PendingMineTileKey = null;
            PendingManualMineSelectionKey = null;
            ActiveMiningClaim = null;
            return;
        }

        _buildingAssignment.Release(this, restoreHostedCreatureLocomotion, PendingMineTileKey is not null);

        PendingMineTileKey = null;
        PendingManualMineSelectionKey = null;
        ActiveMiningClaim = null;
    }

    protected override bool EnsureReadyForNavigation()
    {
        return IsLocomotionEnabled ||
               (HostedBuilding as StationBuilding)?.TryRestoreCreatureLocomotion(this) == true;
    }

    public void ClearBuilderSourcePost(bool releaseReservation = true)
    {
        if (releaseReservation && BuilderSourcePost is not null)
        {
            BuilderSourcePost.ReleaseMaterialReservation(this);
        }

        BuilderSourcePost = null;
        BuilderSourceBuilding = null;
    }

    private void SetBuilderSource(Building sourceBuilding)
    {
        BuilderSourceBuilding = sourceBuilding;
        BuilderSourcePost = sourceBuilding as MiningPost;
    }

    public IReadOnlyList<MiningPost> GetMiningPosts()
    {
        return Cave?.GetMiningPosts() ?? [];
    }

    public IReadOnlyList<string> GetManualMineOrders() => _manualMineTileKeys;

    public void SetManualMineOrders(IEnumerable<string> tileKeys)
    {
        _manualMineTileKeys.Clear();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tileKey in tileKeys)
        {
            if (!string.IsNullOrWhiteSpace(tileKey) && seen.Add(tileKey))
            {
                _manualMineTileKeys.Add(tileKey);
            }
        }

        if (_manualMineTileKeys.Count == 0)
        {
            return;
        }

        ResetPendingMineTarget();
        RestartBehavior();
    }

    public void ClearManualMineOrders(bool restartBehavior = false)
    {
        _manualMineTileKeys.Clear();
        if (restartBehavior)
        {
            RestartBehavior();
        }
    }

    public void ResetPendingMineTarget(bool requeue = false)
    {
        var miningPost = GetAssignedMiningPost();
        if (requeue && miningPost is not null && PendingMineTileKey is not null)
        {
            miningPost.InvalidateMineableQueuesForKeys([PendingMineTileKey]);
        }

        miningPost?.Assign(this, null);
        ActiveMiningClaim = null;
        PendingMineTileKey = null;
        PendingMinePath = null;
        PendingManualMineSelectionKey = null;
    }

    private bool RetargetStalePendingMineTarget(MiningPost miningPost, string tileKey)
    {
        miningPost.InvalidateMineableQueuesForKeys([tileKey]);
        miningPost.Assign(this, null);
        ActiveMiningClaim = null;
        PendingMineTileKey = null;
        PendingMinePath = null;
        var wasManualTarget = HasPendingManualMineSelection();
        PendingManualMineSelectionKey = null;
        if (wasManualTarget)
        {
            PruneResolvedManualMineOrders();
        }

        return HasInventory() ? AdvanceMinerDepositInventory() : AdvanceMinerAcquireClaim();
    }

    private static string? GetMiningPostSelectionKey(MiningPost? post)
    {
        return post?.Location?.ToString() ?? post?.Name;
    }

    private bool IsSelectableMiningPost(MiningPost? post, ISet<MiningPost>? excludedPosts = null)
    {
        return post is not null &&
               post.Location is not null &&
               post.TileArray.Count > 0 &&
               excludedPosts?.Contains(post) != true;
    }

    private bool CanSearchForMiningPost(MiningPost? preferredPost = null)
    {
        return Cave is not null && HasReachableMiningPostWork(preferredPost);
    }

    private bool WaitForMiningAssignmentAvailability(MiningPost? currentPost = null)
    {
        if (Cave is null || HasReachableMiningPostWork(currentPost))
        {
            return false;
        }

        if (HasMiningWorkBlockedOnlyByStorage())
        {
            QueueMinerState(MinerState.WaitForStorage, WorkerRoleFailureReason.NoStorage);
        }
        else
        {
            QueueMinerState(MinerState.WaitForWork, WorkerRoleFailureReason.NoWork);
        }

        return true;
    }

    private bool HasReachableMiningPostWork(MiningPost? preferredPost = null)
    {
        if (Cave is null)
        {
            return false;
        }

        var posts = Cave.GetMiningPosts();
        for (var index = 0; index < posts.Count; index++)
        {
            var post = posts[index];
            if (post.GetInventorySpace() <= 0 || !CanReachMiningPostArea(post))
            {
                continue;
            }

            if (HasManualMineOrders() || post.HasClaimableMineableFor(Cave, this, carriedResource: null))
            {
                return true;
            }
        }

        return preferredPost is not null &&
               preferredPost.GetInventorySpace() > 0 &&
               CanReachMiningPostArea(preferredPost) &&
               preferredPost.HasClaimableMineableFor(Cave, this, carriedResource: null);
    }

    private bool HasMiningWorkBlockedOnlyByStorage()
    {
        if (Cave is null)
        {
            return false;
        }

        var posts = GetMiningPosts();
        for (var index = 0; index < posts.Count; index++)
        {
            var post = posts[index];
            if (post.Location is not null &&
                post.TileArray.Count > 0 &&
                post.GetInventorySpace() <= 0 &&
                post.HasAnyClaimableMineable(Cave))
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldBalanceMiningPostAssignments(MiningPost? preferredPost)
    {
        return preferredPost is null || (Cave?.ShouldRebalanceMiningPostAssignments(preferredPost) ?? false);
    }

    private bool CanReachMiningPostArea(MiningPost post)
    {
        return Cave is not null &&
               (post.IsLocationInArea(Location) || Cave.GetBuildingBfsFieldValue(post, Location) != int.MaxValue);
    }

    private bool CanReachMiningPostInventory(MiningPost post)
    {
        return Cave is not null &&
               (post.IsLocationOnPost(Location) || Cave.GetBuildingBfsFieldValue(post, Location) != int.MaxValue);
    }

    private IEnumerable<MiningPost> EnumerateMiningPostCandidates(string purpose, MiningPost? preferredPost = null, ISet<MiningPost>? excludedPosts = null)
    {
        var metrics = new MiningPostSelectionMetrics
        {
            Purpose = purpose,
            PreferredPostKey = GetMiningPostSelectionKey(preferredPost)
        };
        LastMiningPostSelectionMetrics = metrics;

        if (Cave is null)
        {
            yield break;
        }

        excludedPosts ??= new HashSet<MiningPost>();
        var visited = new HashSet<MiningPost>();

        if (IsSelectableMiningPost(preferredPost, excludedPosts) && visited.Add(preferredPost!))
        {
            metrics.ReusedPreferredPost = true;
            metrics.CandidateCount++;
            yield return preferredPost!;
        }

        if (purpose.StartsWith("miner", StringComparison.Ordinal) && !CanSearchForMiningPost(preferredPost))
        {
            yield break;
        }

        var nearestOwner = Cave.GetNearestMiningPost(Location);
        metrics.NearestOwnerPostKey = GetMiningPostSelectionKey(nearestOwner);

        var queue = new Queue<MiningPost>();
        if (IsSelectableMiningPost(nearestOwner, excludedPosts) && visited.Add(nearestOwner!))
        {
            queue.Enqueue(nearestOwner!);
        }

        if (queue.Count > 0)
        {
            Session.MiningPostMovementTelemetry.RecordSelectionGraphBfs();
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            metrics.CandidateCount++;
            if (!ReferenceEquals(current, preferredPost) && !ReferenceEquals(current, nearestOwner))
            {
                metrics.UsedAdjacencyFallback = true;
            }

            yield return current;

            foreach (var neighbor in Cave.GetAdjacentMiningPosts(current))
            {
                if (IsSelectableMiningPost(neighbor, excludedPosts) && visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (metrics.CandidateCount > 0)
        {
            yield break;
        }

        metrics.FullScanFallbackCount++;
        foreach (var post in GetMiningPosts()
                     .Where(post => IsSelectableMiningPost(post, excludedPosts))
                     .OrderBy(GetMiningPostSelectionKey, StringComparer.Ordinal))
        {
            if (!visited.Add(post))
            {
                continue;
            }

            metrics.CandidateCount++;
            yield return post;
        }
    }

    internal MiningPost? SelectMiningPostForMining(ISet<MiningPost>? excludedPosts = null)
    {
        if (Cave is null)
        {
            return null;
        }

        var preferredPost = GetAssignedMiningPost();
        var shouldBalanceAssignments = ShouldBalanceMiningPostAssignments(preferredPost);
        MiningPost? bestPost = null;
        var bestCount = int.MaxValue;

        foreach (var post in EnumerateMiningPostCandidates("miner", shouldBalanceAssignments ? null : preferredPost, excludedPosts))
        {
            if (!CanReachMiningPostArea(post) ||
                post.GetInventorySpace() <= 0 ||
                (!HasManualMineOrders() && !post.HasClaimableMineableFor(Cave, this, carriedResource: null)))
            {
                continue;
            }

            if (!shouldBalanceAssignments)
            {
                return post;
            }

            var assignmentCount = Cave.GetMiningPostAssignmentCount(post);
            if (bestPost is null || assignmentCount < bestCount)
            {
                bestPost = post;
                bestCount = assignmentCount;
            }
        }

        return bestPost;
    }

    internal MiningPost? SelectMiningPostForInventoryDeposit(ISet<MiningPost>? excludedPosts = null)
    {
        foreach (var post in EnumerateMiningPostCandidates("builder-deposit", null, excludedPosts))
        {
            if (!CanReachMiningPostInventory(post) || post.GetInventorySpace() <= 0)
            {
                continue;
            }

            return post;
        }

        return null;
    }

    public List<MiningPost> GetMiningPostPriorityList()
    {
        return EnumerateMiningPostCandidates("miner-priority", GetAssignedMiningPost())
            .Where(post => Cave is not null && CanReachMiningPostArea(post) && post.GetInventorySpace() > 0 && post.AssignmentsAvailable)
            .ToList();
    }

    public bool TryNavigateMiningPosts(ISet<MiningPost>? excludedPosts = null)
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        var assignedPost = GetAssignedMiningPost();
        if (WaitForMiningAssignmentAvailability(assignedPost))
        {
            return false;
        }

        if (assignedPost is not null &&
            Cave is not null &&
            (assignedPost.GetInventorySpace() <= 0 ||
             !assignedPost.HasClaimableMineableFor(Cave, this, carriedResource: null)))
        {
            ReleaseAssignedBuilding();
        }

        excludedPosts ??= new HashSet<MiningPost>();
        var post = SelectMiningPostForMining(excludedPosts);
        if (post is null)
        {
            ReleaseAssignedBuilding();
            if (HasMiningWorkBlockedOnlyByStorage())
            {
                QueueMinerState(MinerState.WaitForStorage, WorkerRoleFailureReason.NoStorage);
            }
            else
            {
                QueueMinerState(MinerState.WaitForWork, WorkerRoleFailureReason.NoWork);
            }

            return false;
        }

        SetAssignedBuilding(post);
        post.Assign(this, null);

        if (post.IsLocationInArea(Location))
        {
            return AdvanceMinerDepositInventory();
        }

        if (!NavigateToBuilding(post))
        {
            ReleaseAssignedBuilding();
            excludedPosts.Add(post);
            return TryNavigateMiningPosts(excludedPosts);
        }

        QueueMinerState(MinerState.DepositInventory);
        return true;
    }

    private bool AdvanceMinerIdle()
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        if (HasInventory())
        {
            var depositPost = SelectMiningPostForInventoryDeposit();
            if (depositPost is not null)
            {
                SetAssignedBuilding(depositPost);
                depositPost.Assign(this, null);
                return AdvanceMinerDepositInventory();
            }
        }

        if (Cave is null || !HasReachableMiningPostWork())
        {
            ReleaseAssignedBuilding();
            LastRoleFailure = HasMiningWorkBlockedOnlyByStorage()
                ? WorkerRoleFailureReason.NoStorage
                : WorkerRoleFailureReason.NoWork;
            return false;
        }

        return AdvanceMinerSelectPost();
    }

    private bool AdvanceMinerSelectPost()
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        return TryNavigateMiningPosts();
    }

    private bool AdvanceMinerDepositInventory()
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        var miningPost = GetAssignedMiningPost();
        if (miningPost is null)
        {
            QueueMinerState(MinerState.SelectPost);
            return false;
        }

        if (ShouldDepositMinerInventory(miningPost))
        {
            if (ReservedZone is not { Purpose: InteractionZonePurpose.ResourceTransfer } transferZone ||
                !ReferenceEquals(transferZone.Owner, miningPost) ||
                !IsAtReservedInteractionSlot())
            {
                if (!NavigateToInteractionZone(miningPost, InteractionZonePurpose.ResourceTransfer))
                {
                    ReleaseAssignedBuilding();
                    QueueMinerState(MinerState.SelectPost);
                    return false;
                }

                QueueMinerState(MinerState.DepositInventory);
                return true;
            }

            if (!TryDepositCarrierInventory(miningPost, out _))
            {
                ReleaseAssignedBuilding();
                QueueMinerState(MinerState.WaitForStorage, WorkerRoleFailureReason.NoStorage, result: false);
                return false;
            }

            if (HasInventory())
            {
                ReleaseAssignedBuilding();
                QueueMinerState(MinerState.WaitForStorage, WorkerRoleFailureReason.NoStorage, result: false);
                return false;
            }
        }

        if (WaitForMiningAssignmentAvailability(miningPost))
        {
            return false;
        }

        if (!miningPost.HasClaimableMineableFor(Cave!, this, carriedResource: null))
        {
            ReleaseAssignedBuilding();
            QueueMinerState(MinerState.SelectPost);
            return false;
        }

        return AdvanceMinerAcquireClaim();
    }

    private bool AdvanceMinerAcquireClaim()
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        var miningPost = GetAssignedMiningPost();
        if (miningPost is null)
        {
            QueueMinerState(MinerState.SelectPost);
            return false;
        }

        var manualTarget = GetNextManualMineOrder();
        if (manualTarget is not null)
        {
            var manualClaim = MiningClaimAllocator.TryClaim(this, miningPost, manualTarget.Value.TargetTile);
            if (!manualClaim.HasValue)
            {
                RotateManualMineOrderToBack(manualTarget.Value.RequestedKey);
                QueueMinerState(MinerState.SelectPost);
                return false;
            }

            AcceptMiningClaim(manualClaim.Value, manualTarget.Value.RequestedKey);
            return AdvanceMinerMoveToClaim();
        }

        if (WaitForMiningAssignmentAvailability(miningPost))
        {
            return false;
        }

        if (!miningPost.HasClaimableMineableFor(Cave!, this, carriedResource: null))
        {
            ReleaseAssignedBuilding();
            QueueMinerState(MinerState.SelectPost);
            return false;
        }

        var claimResult = MiningClaimAllocator.TryClaimNextDetailed(this, miningPost, carriedResource: null);
        if (!claimResult.Claimed)
        {
            miningPost.Assign(this, null);
            QueueMinerState(
                MapMiningClaimFailureToMinerState(claimResult.FailureReason),
                MapMiningClaimFailureToWorkerFailure(claimResult.FailureReason),
                result: false);
            return false;
        }

        AcceptMiningClaim(claimResult.Claim!.Value);
        return AdvanceMinerMoveToClaim();
    }

    private bool AdvanceMinerMoveToClaim()
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        var miningPost = GetAssignedMiningPost();
        if (miningPost is null || PendingMineTileKey is null)
        {
            QueueMinerState(MinerState.SelectPost);
            return false;
        }

        var targetTile = Cave?.GetTile(PendingMineTileKey);
        if (targetTile is null)
        {
            return RetargetStalePendingMineTarget(miningPost, PendingMineTileKey);
        }

        if (!Building.IsMineableType(targetTile.Base))
        {
            return RetargetStalePendingMineTarget(miningPost, PendingMineTileKey);
        }

        var navTarget = ActiveMiningClaim?.ApproachPoint.ToGridPoint() ??
                        miningPost.GetNavigationTarget(Cave!, targetTile);
        if (navTarget is null)
        {
            var wasManualTarget = HasPendingManualMineSelection();
            ResetPendingMineTarget(!wasManualTarget);
            if (wasManualTarget)
            {
                PruneResolvedManualMineOrders();
            }
            QueueMinerState(MinerState.SelectPost);
            return false;
        }

        if (miningPost.GetAssignment(this) != PendingMineTileKey)
        {
            ResetPendingMineTarget(true);
            QueueMinerState(MinerState.SelectPost);
            return false;
        }

        PendingMinePath = null;
        if (MiningStrikeSystem.CanMineReach(this, targetTile.Key))
        {
            return AdvanceMinerMineClaim();
        }

        var approachPoint = ActiveMiningClaim?.ApproachPoint ?? WorldPoint.FromGridPoint(navTarget.Value);
        if (NavigateTo(approachPoint))
        {
            QueueMinerState(MinerState.MineClaim);
            return true;
        }

        var path = PendingMinePath;
        PendingMinePath = null;
        if (path is null || path.Count == 0 || path[0] != Location || path[^1] != navTarget.Value)
        {
            path = Cave?.BuildDirectPathToPoint(Location, navTarget.Value);
            if (path is null)
            {
                ResetPendingMineTarget(true);
                QueueMinerState(MinerState.SelectPost);
                return false;
            }
        }

        if (path.Count < 2)
        {
            return AdvanceMinerMineClaim();
        }

        if (!EnqueueResolvedPath(path, clearExisting: true))
        {
            var wasManualTarget = HasPendingManualMineSelection();
            ResetPendingMineTarget(!wasManualTarget);
            if (wasManualTarget)
            {
                PruneResolvedManualMineOrders();
            }
            QueueMinerState(MinerState.SelectPost);
            return false;
        }

        QueueMinerState(MinerState.MineClaim);
        return true;
    }

    public MineTileResult MineTile(string tileKey)
    {
        var tile = Cave?.GetTile(tileKey);
        if (tile is null)
        {
            return MineTileResult.NotApplied;
        }

        if (!Building.IsMineableType(tile.Base))
        {
            return MineTileResult.NotApplied;
        }

        if (string.Equals(tile.Base, "wall", StringComparison.Ordinal) &&
            GetInventorySpace() < GameConstants.WallMineResourceAmount)
        {
            return MineTileResult.NotApplied;
        }

        if (!tile.IsCaveCrystal() &&
            !string.Equals(tile.Base, "wall", StringComparison.Ordinal) &&
            GetInventorySpace() < 1)
        {
            return MineTileResult.NotApplied;
        }

        if (!MiningStrikeSystem.CanMineReach(this, tileKey))
        {
            return MineTileResult.NotApplied;
        }

        var result = Session.MineTile(Cave!, tileKey, source: "creature");
        if (!result.HitApplied)
        {
            return result;
        }

        if (result.ResourceAmount > 0)
        {
            if (!result.ResourceType.HasValue)
            {
                return MineTileResult.NotApplied;
            }

            var accepted = AddToInventory(result.ResourceType.Value, result.ResourceAmount);
            if (accepted != result.ResourceAmount)
            {
                RemoveFromInventory(result.ResourceType.Value, accepted);
                return MineTileResult.NotApplied;
            }
        }

        return result;
    }

    internal void RecordMiningStrikeResult(MineTileResult result)
    {
        _pendingMiningStrikeResult = result;
    }

    private bool AdvanceMinerMineClaim()
    {
        if (!EnsureMinerState())
        {
            return false;
        }

        var miningPost = GetAssignedMiningPost();
        if (miningPost is null || PendingMineTileKey is null)
        {
            QueueMinerState(MinerState.SelectPost);
            return false;
        }

        var wasManualTarget = HasPendingManualMineSelection();
        if (!_pendingMiningStrikeResult.HasValue)
        {
            var targetTile = Cave?.GetTile(PendingMineTileKey);
            if (targetTile is null || !Building.IsMineableType(targetTile.Base))
            {
                return HasInventory()
                    ? AdvanceMinerDepositInventory()
                    : RetargetStalePendingMineTarget(miningPost, PendingMineTileKey);
            }

            if (!Session.Mining.HasActiveOrPending(this) &&
                !Session.Mining.TryQueueMining(this, PendingMineTileKey))
            {
                ResetPendingMineTarget(!wasManualTarget);
                if (HasInventory())
                {
                    return AdvanceMinerDepositInventory();
                }

                QueueMinerState(MinerState.SelectPost);
                return false;
            }

            QueueMinerState(MinerState.MineClaim);
            return true;
        }

        var result = _pendingMiningStrikeResult.Value;
        _pendingMiningStrikeResult = null;
        if (!result.HitApplied)
        {
            ResetPendingMineTarget(!wasManualTarget);
            if (wasManualTarget)
            {
                PruneResolvedManualMineOrders();
            }

            if (HasInventory())
            {
                return AdvanceMinerDepositInventory();
            }

            QueueMinerState(MinerState.SelectPost);
            return false;
        }

        if (result.TileDepleted)
        {
            var shouldReturnToPostForRevealedSelection = wasManualTarget && ShouldReturnToPostAfterManualReveal();
            ResetPendingMineTarget(false);
            PruneResolvedManualMineOrders();

            if (shouldReturnToPostForRevealedSelection)
            {
                return AdvanceMinerDepositInventory();
            }

            if (GetInventorySpace() <= 0)
            {
                return AdvanceMinerDepositInventory();
            }

            if (CanContinueMiningBeforeDeposit(miningPost))
            {
                return AdvanceMinerAcquireClaim();
            }

            return HasInventory() ? AdvanceMinerDepositInventory() : AdvanceMinerSelectPost();
        }

        if (GetInventorySpace() <= 0)
        {
            ResetPendingMineTarget(!wasManualTarget);
            return AdvanceMinerDepositInventory();
        }

        Session.Mining.TryQueueMining(this, PendingMineTileKey);
        QueueMinerState(MinerState.MineClaim);
        return true;
    }

    private bool ShouldDepositMinerInventory(MiningPost miningPost)
    {
        return HasInventory() &&
               (GetInventorySpace() <= 0 || !CanContinueMiningBeforeDeposit(miningPost));
    }

    private bool CanContinueMiningBeforeDeposit(MiningPost miningPost)
    {
        if (Cave is null)
        {
            return false;
        }

        return HasCompatibleManualMineOrder(carriedResource: null) ||
               miningPost.HasClaimableMineableFor(Cave, this, carriedResource: null);
    }

    private static MinerState MapMiningClaimFailureToMinerState(MiningClaimFailureReason reason)
    {
        return reason switch
        {
            MiningClaimFailureReason.PostFull => MinerState.WaitForStorage,
            MiningClaimFailureReason.NoCompatibleResource => MinerState.DepositInventory,
            MiningClaimFailureReason.NoReachableApproach => MinerState.WaitForWork,
            MiningClaimFailureReason.StaleQueue => MinerState.AcquireClaim,
            _ => MinerState.SelectPost
        };
    }

    private static WorkerRoleFailureReason MapMiningClaimFailureToWorkerFailure(MiningClaimFailureReason reason)
    {
        return reason switch
        {
            MiningClaimFailureReason.PostFull => WorkerRoleFailureReason.NoStorage,
            MiningClaimFailureReason.NoCompatibleResource => WorkerRoleFailureReason.InventoryBlocked,
            MiningClaimFailureReason.NoReachableApproach => WorkerRoleFailureReason.NoReachablePath,
            MiningClaimFailureReason.StaleQueue => WorkerRoleFailureReason.TargetInvalid,
            _ => WorkerRoleFailureReason.NoWork
        };
    }

    private bool HasCompatibleManualMineOrder(ResourceName? carriedResource)
    {
        if (Cave is null)
        {
            return false;
        }

        for (var index = 0; index < _manualMineTileKeys.Count; index++)
        {
            var tile = Cave.GetTile(_manualMineTileKeys[index]);
            var resolvedTile = tile is null ? null : MineOrderPlanner.ResolveTarget(Cave, tile);
            if (resolvedTile is not null &&
                MiningPost.IsMineableTypeCompatibleWithResource(resolvedTile.Base, carriedResource))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasManualMineOrders()
    {
        return _manualMineTileKeys.Count > 0;
    }

    private (string RequestedKey, World.Tile TargetTile)? GetNextManualMineOrder()
    {
        if (Cave is null)
        {
            return null;
        }

        for (var index = 0; index < _manualMineTileKeys.Count; index++)
        {
            var tileKey = _manualMineTileKeys[index];
            var tile = Cave.GetTile(tileKey);
            if (tile is null)
            {
                _manualMineTileKeys.RemoveAt(index);
                index--;
                continue;
            }

            if (Cave.IsTileRevealed(tile) && !Building.IsMineableType(tile.Base))
            {
                _manualMineTileKeys.RemoveAt(index);
                index--;
                continue;
            }

            var resolvedTile = MineOrderPlanner.ResolveTarget(Cave, tile);
            if (resolvedTile is not null)
            {
                if (!MiningPost.IsMineableTypeCompatibleWithResource(resolvedTile.Base, requiredResource: null))
                {
                    continue;
                }

                return (tileKey, resolvedTile);
            }
        }

        return null;
    }

    private bool HasPendingManualMineSelection()
    {
        return !string.IsNullOrWhiteSpace(PendingManualMineSelectionKey);
    }

    private void PruneResolvedManualMineOrders()
    {
        if (Cave is null)
        {
            return;
        }

        for (var index = _manualMineTileKeys.Count - 1; index >= 0; index--)
        {
            var tile = Cave.GetTile(_manualMineTileKeys[index]);
            if (tile is null ||
                (Cave.IsTileRevealed(tile) && !Building.IsMineableType(tile.Base)))
            {
                _manualMineTileKeys.RemoveAt(index);
            }
        }
    }

    private void RotateManualMineOrderToBack(string tileKey)
    {
        var index = _manualMineTileKeys.FindIndex(key => string.Equals(key, tileKey, StringComparison.Ordinal));
        if (index < 0 || _manualMineTileKeys.Count <= 1)
        {
            return;
        }

        var current = _manualMineTileKeys[index];
        _manualMineTileKeys.RemoveAt(index);
        _manualMineTileKeys.Add(current);
    }

    private bool ShouldReturnToPostAfterManualReveal()
    {
        if (Cave is null || string.IsNullOrWhiteSpace(PendingManualMineSelectionKey))
        {
            return false;
        }

        var tile = Cave.GetTile(PendingManualMineSelectionKey);
        return tile is not null &&
               Cave.IsTileRevealed(tile) &&
               !string.Equals(tile.Base, "wall", StringComparison.Ordinal);
    }

    public IReadOnlyList<Scaffolding> GetScaffoldingBuildings()
    {
        if (Cave is null)
        {
            return [];
        }

        return Cave.GetScaffoldingList().Where(scaffold => scaffold.IsInProgress()).ToList();
    }

    private (Building SourceBuilding, IResourceStorage Storage, int RequirementIndex, ResourceRequirement Requirement, ResourceName ResourceType, int Amount)? GetBuilderSupplyOptionForScaffold(Scaffolding scaffold)
    {
        if (Cave is null)
        {
            return null;
        }

        var needs = scaffold.GetNeededRequirements(includeReservations: true, excludeCreature: this);
        if (needs.Count == 0)
        {
            return null;
        }

        Building? bestBuilding = null;
        IResourceStorage? bestStorage = null;
        ResourceStorageMatch bestMatch = default;
        ResourceRequirement? bestRequirement = null;
        var bestDistance = int.MaxValue;
        var bestBuildingId = int.MaxValue;
        var bestRequirementIndex = int.MaxValue;
        var metrics = new MiningPostSelectionMetrics
        {
            Purpose = "builder-supply"
        };
        LastMiningPostSelectionMetrics = metrics;

        // Treat every resource storage as one source pool and choose the nearest
        // reachable compatible source with deterministic building-ID tie breaks.
        foreach (var building in Cave.GetBuildingList())
        {
            if (building is not IResourceStorage storage ||
                building.Cave != Cave ||
                building.Location is null ||
                building.Health <= 0 ||
                !CanReachResourceStorage(building))
            {
                continue;
            }

            var distance = Cave.GetBuildingBfsFieldValue(building, Location);
            if (distance == int.MaxValue ||
                distance > bestDistance ||
                (distance == bestDistance && building.Id >= bestBuildingId))
            {
                continue;
            }

            for (var index = 0; index < needs.Count; index++)
            {
                var need = needs[index];
                var amount = Math.Min(InventoryCapacity, need.Amount);
                var match = storage is MiningPost post
                    ? post.FindAvailableResource(need.Requirement, amount, this)
                    : storage.FindStoredResource(need.Requirement, amount);
                if (!match.HasValue)
                {
                    continue;
                }

                bestBuilding = building;
                bestStorage = storage;
                bestMatch = match.Value;
                bestRequirement = need.Requirement;
                bestDistance = distance;
                bestBuildingId = building.Id;
                bestRequirementIndex = need.RequirementIndex;
                break;
            }
        }

        return bestBuilding is null
            ? null
            : CompleteBuilderSupplySelection(
                bestBuilding,
                bestStorage!,
                bestRequirementIndex,
                bestRequirement!,
                bestMatch,
                metrics);
    }

    private (Building SourceBuilding, IResourceStorage Storage, int RequirementIndex, ResourceRequirement Requirement, ResourceName ResourceType, int Amount) CompleteBuilderSupplySelection(
        Building building,
        IResourceStorage storage,
        int requirementIndex,
        ResourceRequirement requirement,
        ResourceStorageMatch match,
        MiningPostSelectionMetrics metrics)
    {
        if (building is MiningPost selectedPost && Cave is not null)
        {
            var posts = Cave.GetMiningPosts();
            metrics.CandidateCount = 0;
            for (var index = 0; index < posts.Count; index++)
            {
                metrics.CandidateCount++;
                if (ReferenceEquals(posts[index], selectedPost))
                {
                    break;
                }
            }
        }

        metrics.UsedAdjacencyFallback = metrics.CandidateCount > 1;
        return (building, storage, requirementIndex, requirement, match.ResourceType, match.Amount);
    }

    public (Building SourceBuilding, IResourceStorage Storage, int RequirementIndex, ResourceRequirement Requirement, ResourceName ResourceType, int Amount)? GetBuilderSupplyOptionForScaffold(
        Scaffolding scaffold,
        IReadOnlyList<MiningPost>? orderedPosts = null)
    {
        return GetBuilderSupplyOptionForScaffold(scaffold);
    }

    private bool CanReachResourceStorage(Building building)
    {
        return Cave is not null &&
               building.Location is not null &&
               building.TileArray.Count > 0 &&
               Cave.GetBuildingBfsFieldValue(building, Location) != int.MaxValue;
    }

    private bool IsAtResourceStorageSource(Building building)
    {
        return ReservedZone is { Purpose: InteractionZonePurpose.ResourceTransfer } zone &&
               ReferenceEquals(zone.Owner, building) &&
               IsAtReservedInteractionSlot();
    }

    public bool CanActOnScaffold(Scaffolding scaffold)
    {
        if (!scaffold.IsInProgress() || !CanReachScaffolding(scaffold))
        {
            return false;
        }

        if (scaffold.GetAssignments().Contains(this))
        {
            return true;
        }

        if (HasInventory())
        {
            return scaffold.NeedsResource(Inventory.Type!.Value);
        }

        if (scaffold.IsRecipeComplete())
        {
            return scaffold.NeedsConstructionWork() && scaffold.GetAssignments().Count == 0;
        }

        return scaffold.CanAssignBuilder(this, InventoryCapacity) &&
               GetBuilderSupplyOptionForScaffold(scaffold).HasValue;
    }

    public List<Scaffolding> GetScaffoldingPriorityList(
        bool actionableOnly = false,
        IEnumerable<Scaffolding>? excludeScaffolds = null)
    {
        var excluded = excludeScaffolds?.ToHashSet() ?? [];
        var result = new List<Scaffolding>();
        if (Cave is null)
        {
            return result;
        }

        var scaffolds = Cave.GetScaffoldingList();
        for (var index = 0; index < scaffolds.Count; index++)
        {
            var scaffold = scaffolds[index];
            if (excluded.Contains(scaffold) ||
                !scaffold.IsInProgress() ||
                !CanReachScaffolding(scaffold) ||
                (actionableOnly && !CanActOnScaffold(scaffold)))
            {
                continue;
            }

            result.Add(scaffold);
        }

        result.Sort(static (left, right) =>
        {
            if (left.BuildFirst != right.BuildFirst)
            {
                return left.BuildFirst ? -1 : 1;
            }

            return left.Id.CompareTo(right.Id);
        });
        return result;
    }

    private Scaffolding? GetBestScaffolding(
        bool actionableOnly = false,
        ISet<Scaffolding>? excludedScaffolds = null)
    {
        Scaffolding? best = null;
        if (Cave is null)
        {
            return null;
        }

        var scaffolds = Cave.GetScaffoldingList();
        for (var index = 0; index < scaffolds.Count; index++)
        {
            var scaffold = scaffolds[index];
            if (excludedScaffolds?.Contains(scaffold) == true ||
                !scaffold.IsInProgress() ||
                !CanReachScaffolding(scaffold) ||
                (actionableOnly && !CanActOnScaffold(scaffold)))
            {
                continue;
            }

            if (best is null ||
                (scaffold.BuildFirst && !best.BuildFirst) ||
                (scaffold.BuildFirst == best.BuildFirst && scaffold.Id < best.Id))
            {
                best = scaffold;
            }
        }

        return best;
    }

    private bool HasHigherPriorityActionableScaffold(Scaffolding current)
    {
        if (Cave is null || current.BuildFirst)
        {
            return false;
        }

        var scaffolds = Cave.GetScaffoldingList();
        for (var index = 0; index < scaffolds.Count; index++)
        {
            var candidate = scaffolds[index];
            if (!candidate.BuildFirst ||
                !candidate.IsInProgress() ||
                !CanReachScaffolding(candidate) ||
                !CanActOnScaffold(candidate))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    public Scaffolding? EnsureBuilderAssignment(
        bool actionableOnly = false,
        IEnumerable<Scaffolding>? excludeScaffolds = null)
    {
        var excluded = excludeScaffolds?.ToHashSet() ?? [];
        var current = GetAssignedScaffolding();
        if (current is not null &&
            current.IsInProgress() &&
            !excluded.Contains(current) &&
            CanReachScaffolding(current) &&
            (!actionableOnly || CanActOnScaffold(current)) &&
            !HasHigherPriorityActionableScaffold(current))
        {
            current.Assign(this);
            return current;
        }

        if (current is not null)
        {
            ReleaseAssignedBuilding();
        }

        var scaffold = GetBestScaffolding(actionableOnly, excluded);
        if (scaffold is null || !scaffold.CanAssignBuilder(this, InventoryCapacity))
        {
            return null;
        }

        SetAssignedBuilding(scaffold);
        scaffold.Assign(this);
        return scaffold;
    }

    public List<MiningPost> GetBuilderMiningPostPriorityList()
    {
        return EnumerateMiningPostCandidates("builder-priority")
            .Where(CanReachMiningPostInventory)
            .ToList();
    }

    public bool IsInBuildingWorkRange(Building building, GridPoint? location = null)
    {
        return (Cave?.GetBuildingBfsFieldValue(building, location ?? Location) ?? int.MaxValue) == 0;
    }

    private bool CanReachScaffolding(Scaffolding scaffold)
    {
        return (Cave?.GetBuildingBfsFieldValue(scaffold, Location) ?? int.MaxValue) != int.MaxValue;
    }

    private bool QueueBuilderIdle(WorkerRoleFailureReason failure)
    {
        ReleaseInteractionReservation();
        ReleaseAssignedBuilding();
        _builderRetryAfterTick = Session.TickCount + InteractionZone.ReservationLeaseTicks;
        var result = QueueBuilderState(BuilderState.Idle, failure, result: false);
        TryLeaveScaffoldingWhileIdle();
        return result;
    }

    private bool AdvanceBuilderIdle()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        if (Session.TickCount < _builderRetryAfterTick)
        {
            SetActivity(CreatureActivity.Idle);
            return false;
        }

        QueueBuilderState(BuilderState.SelectScaffold);
        return AdvanceBuilderSelectScaffold();
    }

    private bool AdvanceBuilderSelectScaffold()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var current = GetAssignedScaffolding();
        if (current?.CompletionPending == true)
        {
            return LeaveCompletedScaffold(current);
        }

        var scaffold = EnsureBuilderAssignment(actionableOnly: true);
        if (scaffold is null)
        {
            return QueueBuilderIdle(WorkerRoleFailureReason.NoWork);
        }

        if (HasInventory())
        {
            QueueBuilderState(BuilderState.MoveToScaffold);
            return AdvanceBuilderMoveToScaffold();
        }

        if (scaffold.NeedsAnyResource())
        {
            QueueBuilderState(BuilderState.SelectSource);
            return AdvanceBuilderSelectSource();
        }

        if (scaffold.NeedsConstructionWork())
        {
            QueueBuilderState(BuilderState.MoveToScaffold);
            return AdvanceBuilderMoveToScaffold();
        }

        if (scaffold.TryCompleteConstruction(this))
        {
            return true;
        }

        return LeaveCompletedScaffold(scaffold);
    }

    private bool AdvanceBuilderSelectSource()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var scaffold = GetAssignedScaffolding();
        if (scaffold is null || !scaffold.IsInProgress())
        {
            return QueueBuilderIdle(WorkerRoleFailureReason.TargetInvalid);
        }

        if (HasInventory())
        {
            return AdvanceBuilderMoveToScaffold();
        }

        var option = GetBuilderSupplyOptionForScaffold(scaffold);
        if (!option.HasValue)
        {
            scaffold.ReleaseMaterialReservation(this);
            return QueueBuilderIdle(WorkerRoleFailureReason.NoStorage);
        }

        var supply = option.Value;
        var reserved = scaffold.ReserveMaterial(
            this,
            supply.RequirementIndex,
            supply.ResourceType,
            Math.Min(InventoryCapacity, supply.Amount));
        if (reserved <= 0)
        {
            return QueueBuilderIdle(WorkerRoleFailureReason.NoStorage);
        }

        if (supply.SourceBuilding is MiningPost sourcePost)
        {
            var sourceReserved = sourcePost.ReserveMaterial(this, supply.ResourceType, reserved);
            if (sourceReserved != reserved)
            {
                scaffold.ReleaseMaterialReservation(this);
                sourcePost.ReleaseMaterialReservation(this);
                return QueueBuilderIdle(WorkerRoleFailureReason.NoStorage);
            }
        }

        SetBuilderSource(supply.SourceBuilding);
        if (IsAtResourceStorageSource(supply.SourceBuilding))
        {
            QueueBuilderState(BuilderState.WithdrawMaterial);
            return AdvanceBuilderWithdrawMaterial();
        }

        if (!NavigateToInteractionZone(
                supply.SourceBuilding,
                InteractionZonePurpose.ResourceTransfer,
                streamFromBuildingField: true))
        {
            scaffold.ReleaseMaterialReservation(this);
            ClearBuilderSourcePost();
            return QueueBuilderIdle(WorkerRoleFailureReason.NoReachablePath);
        }

        QueueBuilderState(BuilderState.MoveToSource);
        return true;
    }

    private bool AdvanceBuilderMoveToSource()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        if (HasActiveMovement)
        {
            return true;
        }

        if (HasInventory())
        {
            return AdvanceBuilderMoveToScaffold();
        }

        var source = BuilderSourceBuilding as IResourceStorage;
        var scaffold = GetAssignedScaffolding();
        if (source is null || scaffold is null || scaffold.GetMaterialReservation(this) is null)
        {
            return QueueBuilderIdle(WorkerRoleFailureReason.TargetInvalid);
        }

        if (!IsAtResourceStorageSource(BuilderSourceBuilding!))
        {
            if (!NavigateToInteractionZone(
                    BuilderSourceBuilding!,
                    InteractionZonePurpose.ResourceTransfer,
                    streamFromBuildingField: true))
            {
                scaffold.ReleaseMaterialReservation(this);
                ClearBuilderSourcePost();
                return QueueBuilderIdle(WorkerRoleFailureReason.NoReachablePath);
            }

            QueueBuilderState(BuilderState.MoveToSource);
            return true;
        }

        QueueBuilderState(BuilderState.WithdrawMaterial);
        return AdvanceBuilderWithdrawMaterial();
    }

    private bool AdvanceBuilderWithdrawMaterial()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var scaffold = GetAssignedScaffolding();
        var source = BuilderSourceBuilding as IResourceStorage;
        var reservation = scaffold?.GetMaterialReservation(this);
        if (scaffold is null || source is null || reservation is null)
        {
            return QueueBuilderIdle(WorkerRoleFailureReason.TargetInvalid);
        }

        if (HasInventory())
        {
            return AdvanceBuilderMoveToScaffold();
        }

        if (!IsAtResourceStorageSource(BuilderSourceBuilding!))
        {
            QueueBuilderState(BuilderState.MoveToSource);
            return AdvanceBuilderMoveToSource();
        }

        SetActivity(CreatureActivity.Hauling);
        var requested = Math.Min(GetInventorySpace(), reservation.Value.Amount);
        var withdrawn = BuilderSourcePost is not null
            ? BuilderSourcePost.WithdrawReservedMaterial(this, requested)?.Amount ?? 0
            : source.Withdraw(reservation.Value.ResourceType, requested);
        if (withdrawn <= 0)
        {
            scaffold.ReleaseMaterialReservation(this);
            ClearBuilderSourcePost();
            return QueueBuilderIdle(WorkerRoleFailureReason.NoStorage);
        }

        if (withdrawn < requested)
        {
            scaffold.ReleaseMaterialReservation(this);
            scaffold.ReserveMaterial(
                this,
                reservation.Value.RequirementIndex,
                reservation.Value.ResourceType,
                withdrawn);
        }

        if (AddToInventory(reservation.Value.ResourceType, withdrawn) != withdrawn)
        {
            source.Deposit(reservation.Value.ResourceType, withdrawn);
            scaffold.ReleaseMaterialReservation(this);
            ClearBuilderSourcePost();
            return QueueBuilderIdle(WorkerRoleFailureReason.InventoryBlocked);
        }

        ClearBuilderSourcePost();
        ReleaseInteractionReservation();
        QueueBuilderState(BuilderState.MoveToScaffold);
        return AdvanceBuilderMoveToScaffold();
    }

    private bool AdvanceBuilderMoveToScaffold()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        if (HasActiveMovement)
        {
            return true;
        }

        var scaffold = GetAssignedScaffolding();
        if (scaffold is null || !scaffold.IsInProgress())
        {
            return QueueBuilderIdle(WorkerRoleFailureReason.TargetInvalid);
        }

        if (!HasInventory())
        {
            if (scaffold.NeedsAnyResource())
            {
                QueueBuilderState(BuilderState.SelectSource);
                return AdvanceBuilderSelectSource();
            }

            if (scaffold.NeedsConstructionWork())
            {
                if (ReservedZone is { Purpose: InteractionZonePurpose.Construction } workZone &&
                    ReferenceEquals(workZone.Owner, scaffold) &&
                    IsAtReservedInteractionSlot())
                {
                    QueueBuilderState(BuilderState.BuildScaffold);
                    return AdvanceBuilderBuildScaffold();
                }

                if (!NavigateToInteractionZone(
                        scaffold,
                        InteractionZonePurpose.Construction,
                        streamFromBuildingField: true))
                {
                    return QueueBuilderIdle(WorkerRoleFailureReason.NoReachablePath);
                }

                QueueBuilderState(BuilderState.MoveToScaffold);
                return true;
            }

            return LeaveCompletedScaffold(scaffold);
        }

        if (!scaffold.NeedsResource(Inventory.Type!.Value))
        {
            QueueBuilderState(BuilderState.SelectScaffold);
            return AdvanceBuilderSelectScaffold();
        }

        if (ReservedZone is { Purpose: InteractionZonePurpose.Construction } constructionZone &&
            ReferenceEquals(constructionZone.Owner, scaffold) &&
            IsAtReservedInteractionSlot())
        {
            QueueBuilderState(BuilderState.DepositMaterial);
            return AdvanceBuilderDepositMaterial();
        }

        if (!NavigateToInteractionZone(
                scaffold,
                InteractionZonePurpose.Construction,
                streamFromBuildingField: true))
        {
            return QueueBuilderIdle(WorkerRoleFailureReason.NoReachablePath);
        }

        QueueBuilderState(BuilderState.MoveToScaffold);
        return true;
    }

    private bool AdvanceBuilderDepositMaterial()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var scaffold = GetAssignedScaffolding();
        if (scaffold is null || !HasInventory())
        {
            return QueueBuilderIdle(WorkerRoleFailureReason.TargetInvalid);
        }

        if (ReservedZone is not { Purpose: InteractionZonePurpose.Construction } deliveryZone ||
            !ReferenceEquals(deliveryZone.Owner, scaffold) ||
            !IsAtReservedInteractionSlot())
        {
            QueueBuilderState(BuilderState.MoveToScaffold);
            return AdvanceBuilderMoveToScaffold();
        }

        if (!TryDepositCarrierInventory(scaffold, out _))
        {
            scaffold.ReleaseMaterialReservation(this);
            QueueBuilderState(BuilderState.SelectScaffold);
            return AdvanceBuilderSelectScaffold();
        }

        QueueBuilderState(BuilderState.SelectScaffold);
        return AdvanceBuilderSelectScaffold();
    }

    private bool AdvanceBuilderBuildScaffold()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var scaffold = GetAssignedScaffolding();
        if (scaffold is null || !scaffold.IsInProgress())
        {
            return QueueBuilderIdle(WorkerRoleFailureReason.TargetInvalid);
        }

        if (HasInventory())
        {
            return AdvanceBuilderDepositMaterial();
        }

        if (!scaffold.IsRecipeComplete())
        {
            QueueBuilderState(BuilderState.SelectSource);
            return AdvanceBuilderSelectSource();
        }

        if (scaffold.CompletionPending || !scaffold.NeedsConstructionWork())
        {
            return LeaveCompletedScaffold(scaffold);
        }

        if (ReservedZone is not { Purpose: InteractionZonePurpose.Construction } workZone ||
            !ReferenceEquals(workZone.Owner, scaffold) ||
            !IsAtReservedInteractionSlot())
        {
            QueueBuilderState(BuilderState.MoveToScaffold);
            return AdvanceBuilderMoveToScaffold();
        }

        SetActivity(CreatureActivity.Working);
        var worked = scaffold.ApplyConstructionWork(BuilderWorkRate, this);
        if (worked <= 0)
        {
            QueueBuilderState(BuilderState.SelectScaffold);
            return AdvanceBuilderSelectScaffold();
        }

        if (!scaffold.NeedsConstructionWork() && scaffold.CompletionPending)
        {
            return LeaveCompletedScaffold(scaffold);
        }

        return true;
    }

    private bool LeaveCompletedScaffold(Scaffolding scaffold)
    {
        if (Cave is null)
        {
            return QueueBuilderIdle(WorkerRoleFailureReason.TargetInvalid);
        }

        GridPoint? bestLocation = null;
        var bestDistance = int.MaxValue;
        string? bestKey = null;
        foreach (var tile in scaffold.TileArray)
        {
            foreach (var neighbor in tile.Neighbors)
            {
                if (ReferenceEquals(neighbor.Built, scaffold) || !Cave.CanCreatureTraverseTile(this, neighbor))
                {
                    continue;
                }

                var distance = GridPoint.SquaredDistance(Location, neighbor.Coordinates);
                if (bestLocation is null ||
                    distance < bestDistance ||
                    (distance == bestDistance && string.CompareOrdinal(neighbor.Key, bestKey) < 0))
                {
                    bestLocation = neighbor.Coordinates;
                    bestDistance = distance;
                    bestKey = neighbor.Key;
                }
            }
        }

        if (bestLocation is null)
        {
            QueueBuilderState(BuilderState.SelectScaffold);
            return true;
        }

        if (Location == bestLocation.Value)
        {
            CancelMovement();
            ReleaseInteractionReservation();
            QueueBuilderState(BuilderState.SelectScaffold);
            return true;
        }

        if (!NavigateTo(bestLocation.Value))
        {
            return QueueBuilderIdle(WorkerRoleFailureReason.NoReachablePath);
        }

        QueueBuilderState(BuilderState.SelectScaffold);
        return true;
    }

}
