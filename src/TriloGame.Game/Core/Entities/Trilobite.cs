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
    private readonly CombatAgentController _combatAgentController = new();
    private readonly List<string> _manualMineTileKeys = [];
    private bool _fleeingToQueen;
    private MineTileResult? _pendingMiningStrikeResult;
    private GridPoint? _farmerHarvestTarget;
    private bool _fighterPreferAssignedStation = true;
    private bool _depositInventoryBeforeRole;

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
        PendingMinePath = claim.Route;
        PendingManualMineSelectionKey = manualSelectionKey;
    }

    private IReadOnlyList<GridPoint>? PendingMinePath { get; set; }

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

    private bool AdvanceFarmerRole()
    {
        return FarmerState switch
        {
            FarmerState.Idle => AdvanceFarmerSelectFarm(),
            FarmerState.SelectFarm => AdvanceFarmerSelectFarm(),
            FarmerState.MoveToFarmSlot => AdvanceFarmerMoveToFarmSlot(),
            FarmerState.Harvest => _farmerHarvestTarget.HasValue
                ? AdvanceFarmerHarvest(_farmerHarvestTarget.Value)
                : QueueFarmerState(FarmerState.MoveToFarmSlot, WorkerRoleFailureReason.TargetInvalid, result: false),
            FarmerState.MoveToQueen => AdvanceFarmerMoveToQueen(),
            FarmerState.FeedQueen => AdvanceFarmerFeedQueen(),
            FarmerState.WaitForFarm => QueueFarmerState(FarmerState.Idle, WorkerRoleFailureReason.NoWork, result: false),
            _ => QueueFarmerState(FarmerState.Idle, WorkerRoleFailureReason.TargetInvalid, result: false)
        };
    }

    private bool AdvanceBuilderRole()
    {
        return BuilderState switch
        {
            BuilderState.Idle => AdvanceBuilderSelectScaffold(),
            BuilderState.SelectScaffold => AdvanceBuilderSelectScaffold(),
            BuilderState.ReserveMaterial => AdvanceBuilderReserveMaterial(),
            BuilderState.MoveToSource or BuilderState.WithdrawMaterial => AdvanceBuilderWithdrawMaterial(),
            BuilderState.MoveToScaffold or BuilderState.DepositMaterial => AdvanceBuilderDepositMaterial(),
            BuilderState.BuildScaffold => AdvanceBuilderBuildScaffold(),
            BuilderState.DepositExtraInventory => AdvanceBuilderDepositExtraInventory(),
            BuilderState.WaitForMaterials => QueueBuilderState(BuilderState.Idle, WorkerRoleFailureReason.NoWork, result: false),
            _ => QueueBuilderState(BuilderState.Idle, WorkerRoleFailureReason.TargetInvalid, result: false)
        };
    }

    private bool AdvanceFighterRole()
    {
        if (FighterState == FighterState.Idle &&
            Cave is { } cave &&
            (cave.GetTurretList().Count > 0 || cave.GetBarracksList().Count > 0))
        {
            WakeForFighterStationAvailability();
        }

        return _combatAgentController.Advance(this);
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
        if (TryHoldAssignedFighterStation())
        {
            return true;
        }

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
        if (TryHoldAssignedFighterStation())
        {
            return true;
        }

        if (!IsFighter() ||
            (!Session.Danger && MovementCohort.GoalKind != MovementGoalKind.Combat))
        {
            return false;
        }

        return _combatAgentController.RefreshActivePursuit(this);
    }

    // Dock at an assigned station on arrival; turret crews also board while danger is active.
    private bool TryHoldAssignedFighterStation()
    {
        if (!IsFighter() || !IsLocomotionEnabled)
        {
            return false;
        }

        var station = GetAssignedFighterStation();
        return station is not null &&
               (!Session.Danger || station is Turret) &&
               !ShouldBalanceFighterStationAssignments(station) &&
               station.IsCreatureAtNavigationTarget(this) &&
               TryStationAtFighterStation(station);
    }

    // Idle trilobites should step off scaffolding so finished builds can complete without prolonged blocking.
    private void TryLeaveScaffoldingWhileIdle()
    {
        if (QueuedTaskCount > 0 || Cave is null || !IsLocomotionEnabled)
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

    public int FeedQueenAlgae(Queen queen)
    {
        if (!HasInventory() || Inventory.Type != ResourceName.Algae)
        {
            return 0;
        }

        var result = queen.FeedAlgae(Inventory.Amount, this, Cave);
        if (result.Accepted <= 0)
        {
            return 0;
        }

        RemoveFromInventory(result.Accepted);
        return result.Accepted;
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
            PendingMinePath = null;
            return;
        }

        _buildingAssignment.Release(this, restoreHostedCreatureLocomotion, PendingMineTileKey is not null);

        PendingMineTileKey = null;
        PendingManualMineSelectionKey = null;
        ActiveMiningClaim = null;
        PendingMinePath = null;
    }

    protected override bool EnsureReadyForNavigation()
    {
        return IsLocomotionEnabled ||
               (HostedBuilding as StationBuilding)?.TryRestoreCreatureLocomotion(this) == true;
    }

    internal bool TryStationAtFighterStation(StationBuilding station)
    {
        return station.TryStationCreature(this);
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

    // Resume the generic scaffold-selection pass without disturbing an active route or reservation.
    internal void WakeForScaffoldAvailability()
    {
        if (Role != CreatureRole.Builder)
        {
            return;
        }

        BuilderState = BuilderState.SelectScaffold;
        LastRoleFailure = WorkerRoleFailureReason.None;
    }

    // A new scaffold only wakes builders that are not already committed to valid scaffold work.
    internal void WakeForNewScaffolding()
    {
        if (Role != CreatureRole.Builder)
        {
            return;
        }

        var assignedScaffold = GetAssignedScaffolding();
        if (assignedScaffold is not null && assignedScaffold.IsInProgress())
        {
            return;
        }

        WakeForScaffoldAvailability();
    }

    // A newly available station restarts only fighters that are currently idling.
    internal void WakeForFighterStationAvailability()
    {
        if (Role != CreatureRole.Fighter || FighterState != FighterState.Idle)
        {
            return;
        }

        FighterState = FighterState.SelectStation;
        LastRoleFailure = WorkerRoleFailureReason.None;
    }

    public void ClearFighterTarget()
    {
        if (FighterTarget is null)
        {
            return;
        }

        FighterTarget = null;
    }

    internal void SetFighterTarget(Enemy? target)
    {
        if (ReferenceEquals(FighterTarget, target))
        {
            return;
        }

        FighterTarget = target;
    }

    internal bool HasValidFighterTarget()
    {
        return FighterTarget is { Health: > 0 } target && ReferenceEquals(target.Cave, Cave);
    }

    internal bool CanReachFighterTarget()
    {
        return HasValidFighterTarget() &&
               CombatWorld.CanMeleeReach(this, Combat.CombatTargetRef.For(FighterTarget!));
    }

    public IReadOnlyList<Barracks> GetBarracksBuildings()
    {
        return Cave?.GetBarracksList() ?? [];
    }

    public IReadOnlyList<Turret> GetTurretBuildings()
    {
        return Cave?.GetTurretList() ?? [];
    }

    public IReadOnlyList<StationBuilding> GetFighterStationBuildings()
    {
        return Cave?.GetFighterStations() ?? [];
    }

    public StationBuilding? GetFighterStationAtLocation(GridPoint? location = null)
    {
        if (location is null && HostedBuilding is StationBuilding hostedStation)
        {
            return hostedStation;
        }

        var checkLocation = location ?? Location;
        return GetFighterStationBuildings()
            .Where(station => IsAtFighterStationNavigationTarget(station, checkLocation))
            .OrderByDescending(station => station.FighterAssignmentPriority)
            .ThenBy(GetOwnedBuildingSelectionKey, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public Barracks? GetBarracksAtLocation(GridPoint? location = null)
    {
        return GetFighterStationAtLocation(location) as Barracks;
    }

    private static string GetOwnedBuildingSelectionKey(Building? building)
    {
        return building?.Location?.ToString() ?? building?.Name ?? string.Empty;
    }

    private int CompareBuildingNavigationPriority(Building left, Building right)
    {
        var leftDistance = Cave?.GetBuildingNavigationDistance(left, Location) ?? int.MaxValue;
        var rightDistance = Cave?.GetBuildingNavigationDistance(right, Location) ?? int.MaxValue;
        var distanceOrder = leftDistance.CompareTo(rightDistance);
        return distanceOrder != 0
            ? distanceOrder
            : string.CompareOrdinal(GetOwnedBuildingSelectionKey(left), GetOwnedBuildingSelectionKey(right));
    }

    private bool IsAtFighterStationNavigationTarget(StationBuilding station, GridPoint location)
    {
        if (Cave is null)
        {
            return false;
        }

        return station switch
        {
            Turret turret => Cave.GetTile(location.ToString()) is { } tile &&
                             tile.Neighbors.Any(neighbor => ReferenceEquals(neighbor.Built, turret)),
            _ => IsOnPassableBuildingTile(station, location)
        };
    }

    private bool IsSelectableStation(StationBuilding? station, ISet<StationBuilding>? excludedStations = null)
    {
        return station is not null &&
               station.Location is not null &&
               station.TileArray.Count > 0 &&
               station.CanAssign(this) &&
               excludedStations?.Contains(station) != true;
    }

    private bool ShouldBalanceFighterStationAssignments(StationBuilding? preferredStation)
    {
        return preferredStation is null || (Cave?.ShouldRebalanceFighterStationAssignments(preferredStation) ?? false);
    }

    private IEnumerable<TStation> EnumerateStationTypeCandidates<TStation>(
        int priority,
        TStation? nearestStation,
        IEnumerable<TStation> allStations,
        ISet<StationBuilding> excludedStations,
        ISet<StationBuilding> visited)
        where TStation : StationBuilding
    {
        if (IsSelectableStation(nearestStation, excludedStations) &&
            nearestStation!.FighterAssignmentPriority == priority &&
            visited.Add(nearestStation))
        {
            yield return nearestStation;
        }

        var candidates = new List<TStation>();
        foreach (var station in allStations)
        {
            if (IsSelectableStation(station, excludedStations) &&
                station.FighterAssignmentPriority == priority)
            {
                candidates.Add(station);
            }
        }

        candidates.Sort((left, right) => CompareBuildingNavigationPriority(left, right));
        foreach (var station in candidates)
        {
            if (visited.Add(station))
            {
                yield return station;
            }
        }
    }

    private IEnumerable<StationBuilding> EnumerateFighterStationCandidates(int priority, StationBuilding? preferredStation = null, ISet<StationBuilding>? excludedStations = null)
    {
        if (Cave is null)
        {
            yield break;
        }

        excludedStations ??= new HashSet<StationBuilding>();
        var visited = new HashSet<StationBuilding>();

        if (IsSelectableStation(preferredStation, excludedStations) &&
            preferredStation!.FighterAssignmentPriority == priority &&
            visited.Add(preferredStation))
        {
            yield return preferredStation;
        }

        foreach (var turret in EnumerateStationTypeCandidates(
                     priority,
                     Cave.GetNearestTurret(Location),
                     GetTurretBuildings(),
                     excludedStations,
                     visited))
        {
            yield return turret;
        }

        foreach (var barracks in EnumerateStationTypeCandidates(
                     priority,
                     Cave.GetNearestBarracks(Location),
                     GetBarracksBuildings(),
                     excludedStations,
                     visited))
        {
            yield return barracks;
        }
    }

    public List<StationBuilding> GetFighterStationPriorityList()
    {
        var prioritizedStations = new List<StationBuilding>();
        var visited = new HashSet<StationBuilding>();
        foreach (var priority in GetFighterStationBuildings()
                     .Select(station => station.FighterAssignmentPriority)
                     .Distinct()
                     .OrderByDescending(priority => priority))
        {
            foreach (var station in EnumerateFighterStationCandidates(priority, GetAssignedFighterStation(), visited))
            {
                if (!visited.Add(station))
                {
                    continue;
                }

                prioritizedStations.Add(station);
            }
        }

        return prioritizedStations;
    }

    internal StationBuilding? SelectFighterStation(StationBuilding? preferredStation = null, ISet<StationBuilding>? excludedStations = null)
    {
        excludedStations ??= new HashSet<StationBuilding>();
        foreach (var priority in GetFighterStationBuildings()
                     .Where(station => IsSelectableStation(station, excludedStations))
                     .Select(station => station.FighterAssignmentPriority)
                     .Distinct()
                     .OrderByDescending(priority => priority))
        {
            var shouldBalanceAssignments = ShouldBalanceFighterStationAssignments(
                preferredStation is not null && preferredStation.FighterAssignmentPriority == priority
                    ? preferredStation
                    : null);
            StationBuilding? bestStation = null;
            var bestCount = int.MaxValue;

            foreach (var station in EnumerateFighterStationCandidates(
                         priority,
                         shouldBalanceAssignments ? null : preferredStation,
                         excludedStations))
            {
                if (!shouldBalanceAssignments)
                {
                    return station;
                }

                var assignmentCount = Cave?.GetStationAssignmentCount(station) ?? int.MaxValue;
                if (bestStation is null || assignmentCount < bestCount)
                {
                    bestStation = station;
                    bestCount = assignmentCount;
                }
            }

            if (bestStation is not null)
            {
                return bestStation;
            }
        }

        return null;
    }

    public List<Barracks> GetBarracksPriorityList()
    {
        return GetFighterStationPriorityList()
            .OfType<Barracks>()
            .ToList();
    }

    internal Barracks? SelectBarracks(Barracks? preferredBarracks = null, ISet<Barracks>? excludedBarracks = null)
    {
        HashSet<StationBuilding>? excludedStations = null;
        if (excludedBarracks is not null)
        {
            excludedStations = [];
            foreach (var barracks in excludedBarracks)
            {
                excludedStations.Add(barracks);
            }
        }

        return SelectFighterStation(preferredBarracks, excludedStations) as Barracks;
    }

    internal Turret? SelectTurret(Turret? preferredTurret = null, ISet<Turret>? excludedTurrets = null)
    {
        HashSet<StationBuilding>? excludedStations = null;
        if (excludedTurrets is not null)
        {
            excludedStations = [];
            foreach (var turret in excludedTurrets)
            {
                excludedStations.Add(turret);
            }
        }

        return SelectFighterStation(preferredTurret, excludedStations) as Turret;
    }

    public IReadOnlyList<Enemy> GetEnemyCreatures()
    {
        return Cave?.GetEnemyList() ?? [];
    }

    public Enemy? GetReachableEnemy()
    {
        return Session.Combat.FindDirectedEnemy(this) ?? Session.Combat.FindReachableEnemy(this);
    }

    public bool QueueFighterPath(IReadOnlyList<GridPoint> path, string? mode = null, bool clearExisting = true)
    {
        if (path.Count < 2)
        {
            FighterPathMode = null;
            return path.Count > 0;
        }

        FighterPathMode = mode;
        return clearExisting ? QueueMovePath(path) : AppendMovePath(path);
    }

    public bool TryNavigateToFighterStation(ISet<StationBuilding>? excludedStations = null, bool preferAssignedStation = true)
    {
        if (!EnsureFighterState())
        {
            return false;
        }

        excludedStations ??= new HashSet<StationBuilding>();
        var preferredStation = preferAssignedStation ? GetAssignedFighterStation() : null;
        var station = SelectFighterStation(preferredStation, excludedStations);
        if (station is null)
        {
            return false;
        }

        if (!station.CanAssign(this))
        {
            excludedStations.Add(station);
            return TryNavigateToFighterStation(excludedStations, preferAssignedStation: false);
        }

        SetAssignedBuilding(station);
        if (!station.Assign(this))
        {
            ReleaseAssignedBuilding();
            excludedStations.Add(station);
            return TryNavigateToFighterStation(excludedStations, preferAssignedStation: false);
        }

        if (TryStationAtFighterStation(station))
        {
            return false;
        }

        FighterPathMode = "station";
        var startedNavigation = station is Turret
            ? NavigateToInteractionZone(station, InteractionZonePurpose.Approach)
            : NavigateToBuilding(station);
        if (startedNavigation)
        {
            return true;
        }

        // The worker publishes the first field after construction. Keep the deterministic
        // assignment while that field is pending instead of falling back to a lower-priority home.
        if (Cave?.UsesAsyncBuildingNavigationField(station) == true &&
            station.PublishedNavigationField is null)
        {
            return false;
        }

        ReleaseAssignedBuilding();
        excludedStations.Add(station);
        return TryNavigateToFighterStation(excludedStations, preferAssignedStation: false);
    }

    public bool TryNavigateBarracks(ISet<Barracks>? excludedBarracks = null, bool preferAssignedBarracks = true)
    {
        HashSet<StationBuilding>? excludedStations = null;
        if (excludedBarracks is not null)
        {
            excludedStations = [];
            foreach (var barracks in excludedBarracks)
            {
                excludedStations.Add(barracks);
            }
        }

        return TryNavigateToFighterStation(excludedStations, preferAssignedBarracks);
    }

    internal bool AdvanceFighterReturnToStation(bool preferAssignedStation = true)
    {
        if (!EnsureFighterState())
        {
            return false;
        }

        var assignedStation = GetAssignedFighterStation();
        var shouldRebalanceAssignedStation = ShouldBalanceFighterStationAssignments(assignedStation);
        if (preferAssignedStation && assignedStation is not null)
        {
            var retainedAssignedStation = assignedStation.Assign(this);
            if (retainedAssignedStation && !shouldRebalanceAssignedStation && TryStationAtFighterStation(assignedStation))
            {
                return false;
            }
        }

        if (preferAssignedStation)
        {
            var currentStation = GetFighterStationAtLocation();
            if (currentStation is not null && currentStation.CanAssign(this))
            {
                SetAssignedBuilding(currentStation);
                currentStation.Assign(this);
                if (!ShouldBalanceFighterStationAssignments(currentStation) && TryStationAtFighterStation(currentStation))
                {
                    return false;
                }
            }
        }

        if (SelectFighterStation(preferAssignedStation ? assignedStation : null) is null)
        {
            if (!preferAssignedStation)
            {
                ReleaseAssignedBuilding();
            }

            return false;
        }

        return TryNavigateToFighterStation(preferAssignedStation: preferAssignedStation);
    }

    public bool FighterReturnToBarracks(bool preferAssignedBarracks = true)
    {
        return AdvanceFighterReturnToStation(preferAssignedBarracks);
    }

    public List<AlgaeFarm> GetAlgaeFarmPriorityList()
    {
        return EnumerateAlgaeFarmCandidates(GetAssignedAlgaeFarm())
            .Where(CanReachAlgaeFarm)
            .ToList();
    }

    private bool CanSearchForAlgaeFarm(AlgaeFarm? preferredFarm = null)
    {
        return Cave is not null &&
               ((preferredFarm is not null && preferredFarm.HasAssignmentSlot(this)) || Cave.HasOpenAlgaeFarms);
    }

    private bool IsSelectableAlgaeFarm(AlgaeFarm? farm, ISet<AlgaeFarm>? excludedFarms = null)
    {
        return farm is not null &&
               farm.Location is not null &&
               farm.TileArray.Count > 0 &&
               farm.HasAssignmentSlot(this) &&
               excludedFarms?.Contains(farm) != true;
    }

    private bool CanReachAlgaeFarm(AlgaeFarm farm)
    {
        return Cave is not null &&
               (farm.IsLocationOnFarm(Location) ||
                ReferenceEquals(Cave.GetNearestAlgaeFarm(Location), farm) ||
                Cave.GetBuildingBfsFieldValue(farm, Location) != int.MaxValue);
    }

    private IEnumerable<AlgaeFarm> EnumerateAlgaeFarmCandidates(AlgaeFarm? preferredFarm = null, ISet<AlgaeFarm>? excludedFarms = null)
    {
        if (Cave is null)
        {
            yield break;
        }

        excludedFarms ??= new HashSet<AlgaeFarm>();
        var visited = new HashSet<AlgaeFarm>();

        if (IsSelectableAlgaeFarm(preferredFarm, excludedFarms) && visited.Add(preferredFarm!))
        {
            yield return preferredFarm!;
        }

        if (!CanSearchForAlgaeFarm(preferredFarm))
        {
            yield break;
        }

        var candidates = new List<AlgaeFarm>();
        foreach (var farm in GetAlgaeFarms())
        {
            if (IsSelectableAlgaeFarm(farm, excludedFarms))
            {
                candidates.Add(farm);
            }
        }

        candidates.Sort((left, right) => CompareBuildingNavigationPriority(left, right));
        foreach (var farm in candidates)
        {
            if (visited.Add(farm))
            {
                yield return farm;
            }
        }
    }

    internal AlgaeFarm? SelectAlgaeFarm(AlgaeFarm? preferredFarm = null, ISet<AlgaeFarm>? excludedFarms = null)
    {
        foreach (var farm in EnumerateAlgaeFarmCandidates(preferredFarm, excludedFarms))
        {
            if (CanReachAlgaeFarm(farm))
            {
                return farm;
            }
        }

        return null;
    }

    public bool TryNavigateAlgaeFarms(ISet<AlgaeFarm>? excludedFarms = null)
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        excludedFarms ??= new HashSet<AlgaeFarm>();
        var farm = SelectAlgaeFarm(GetAssignedAlgaeFarm(), excludedFarms);
        if (farm is null)
        {
            ReleaseAssignedBuilding();
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        SetAssignedBuilding(farm);
        if (!farm.Assign(this))
        {
            ReleaseAssignedBuilding();
            excludedFarms.Add(farm);
            return TryNavigateAlgaeFarms(excludedFarms);
        }

        if (ReservedZone is { Purpose: InteractionZonePurpose.Work } reservedWorkZone &&
            ReferenceEquals(reservedWorkZone.Owner, farm) &&
            IsAtReservedInteractionSlot())
        {
            return AdvanceFarmerMoveToFarmSlot();
        }

        if (!NavigateToInteractionZone(farm, InteractionZonePurpose.Work))
        {
            ReleaseAssignedBuilding();
            excludedFarms.Add(farm);
            return TryNavigateAlgaeFarms(excludedFarms);
        }

        QueueFarmerState(FarmerState.MoveToFarmSlot);
        return true;
    }

    private bool AdvanceFarmerSelectFarm()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        if (HasInventory())
        {
            if (Inventory.Type == ResourceName.Algae)
            {
                return AdvanceFarmerMoveToQueen();
            }

            ClearInventory();
        }

        if (SelectAlgaeFarm(GetAssignedAlgaeFarm()) is null)
        {
            ReleaseAssignedBuilding();
            return false;
        }

        return TryNavigateAlgaeFarms();
    }

    private bool AdvanceFarmerMoveToFarmSlot()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        var farm = GetAssignedAlgaeFarm();
        if (farm is null)
        {
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        if (ReservedZone is not { Purpose: InteractionZonePurpose.Work } workZone ||
            !ReferenceEquals(workZone.Owner, farm) ||
            !IsAtReservedInteractionSlot())
        {
            if (!NavigateToInteractionZone(farm, InteractionZonePurpose.Work))
            {
                ReleaseAssignedBuilding();
                QueueFarmerState(FarmerState.SelectFarm);
                return false;
            }

            QueueFarmerState(FarmerState.MoveToFarmSlot);
            return true;
        }

        var nextLocation = farm.GetNextTraversalLocation(Location);
        if (nextLocation is null)
        {
            ReleaseAssignedBuilding();
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        if (!TryMoveInteractionReservation(nextLocation.Value))
        {
            QueueFarmerState(FarmerState.MoveToFarmSlot);
            return false;
        }

        QueueFarmerHarvest(nextLocation.Value);
        return true;
    }

    private bool AdvanceFarmerHarvest(GridPoint nextLocation)
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        var farm = GetAssignedAlgaeFarm();
        if (farm is null)
        {
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        if (!IsAtReservedInteractionSlot() || CurrentCell != nextLocation)
        {
            QueueFarmerState(FarmerState.MoveToFarmSlot);
            return false;
        }

        SetActivity(CreatureActivity.Working);
        if (!farm.TryHarvest(this))
        {
            QueueFarmerState(FarmerState.MoveToFarmSlot);
            return true;
        }

        ClearTaskQueue();
        return AdvanceFarmerMoveToQueen();
    }

    private bool AdvanceFarmerMoveToQueen()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        if (!HasInventory() || Inventory.Type != ResourceName.Algae)
        {
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        var queen = GetQueen();
        if (queen is null)
        {
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        if (queen.CanBeFedBy(this))
        {
            return AdvanceFarmerFeedQueen();
        }

        ClearTaskQueue();
        if (!NavigateToInteractionZone(queen, InteractionZonePurpose.Feeding, clearExisting: false))
        {
            QueueFarmerState(FarmerState.MoveToQueen);
            return false;
        }

        QueueFarmerState(FarmerState.FeedQueen);
        return true;
    }

    private bool AdvanceFarmerMoveToQueenStep(GridPoint nextLocation)
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        var moved = PerformMove(nextLocation);
        if (!moved)
        {
            return AdvanceFarmerMoveToQueen();
        }

        return true;
    }

    private bool AdvanceFarmerFeedQueen()
    {
        if (!EnsureFarmerState())
        {
            return false;
        }

        var queen = GetQueen();
        if (queen is null)
        {
            QueueFarmerState(FarmerState.SelectFarm);
            return false;
        }

        if (!queen.CanBeFedBy(this))
        {
            return AdvanceFarmerMoveToQueen();
        }

        SetActivity(CreatureActivity.Feeding);
        var fed = FeedQueenAlgae(queen);
        if (fed <= 0)
        {
            QueueFarmerState(FarmerState.MoveToQueen);
            return false;
        }

        return AdvanceFarmerSelectFarm();
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
        return Cave is not null &&
               ((preferredPost is not null && preferredPost.GetInventorySpace() > 0 && preferredPost.HasAnyClaimableMineable(Cave)) ||
                Cave.HasAvailableMiningPostAssignments);
    }

    private bool WaitForMiningAssignmentAvailability(MiningPost? currentPost = null)
    {
        if (Cave is null ||
            Cave.HasAvailableMiningPostAssignments ||
            (currentPost is not null &&
             currentPost.GetInventorySpace() > 0 &&
             currentPost.HasClaimableMineableFor(Cave, this, carriedResource: null)))
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

        var candidates = new List<MiningPost>();
        foreach (var post in GetMiningPosts())
        {
            if (IsSelectableMiningPost(post, excludedPosts))
            {
                candidates.Add(post);
            }
        }

        candidates.Sort((left, right) => CompareBuildingNavigationPriority(left, right));
        foreach (var post in candidates)
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

        if (GetAssignedMiningPost() is null && Cave is not null && !Cave.HasAvailableMiningPostAssignments)
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

        if (Cave is null || !Cave.HasAvailableMiningPostAssignments)
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

        var path = PendingMinePath;
        PendingMinePath = null;
        if (MiningStrikeSystem.CanMineReach(this, targetTile.Key))
        {
            return AdvanceMinerMineClaim();
        }

        if (path is null || path.Count == 0 || path[0] != Location || path[^1] != navTarget.Value)
        {
            path = Cave?.BuildPathToMineableApproach(this, targetTile);
            if (path is null)
            {
                ResetPendingMineTarget(true);
                QueueMinerState(MinerState.SelectPost);
                return false;
            }

            navTarget = path[^1];
            if (ActiveMiningClaim is { } claim)
            {
                ActiveMiningClaim = claim with { ApproachPoint = WorldPoint.FromGridPoint(navTarget.Value), Route = path };
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

    private (Building SourceBuilding, IResourceStorage Storage, int RequirementIndex, ResourceRequirement Requirement, ResourceName ResourceType, int Amount)? GetBuilderSupplyOptionFromMiningPosts(
        Scaffolding scaffold,
        IReadOnlyList<ScaffoldRequirementNeed> neededRequirements,
        IEnumerable<MiningPost> candidatePosts,
        bool orderedCandidates = false)
    {
        if (orderedCandidates)
        {
            LastMiningPostSelectionMetrics = new MiningPostSelectionMetrics
            {
                Purpose = "builder-supply-ordered"
            };
        }

        foreach (var post in candidatePosts)
        {
            if (orderedCandidates && LastMiningPostSelectionMetrics is not null)
            {
                LastMiningPostSelectionMetrics.CandidateCount++;
            }

            if (!CanReachMiningPostInventory(post))
            {
                continue;
            }

            foreach (var neededRequirement in neededRequirements)
            {
                var reserveAmount = System.Math.Min(InventoryCapacity, neededRequirement.Amount);
                var resourceMatch = post.FindAvailableResource(neededRequirement.Requirement, reserveAmount, this);
                if (resourceMatch is not null)
                {
                    return (
                        post,
                        post,
                        neededRequirement.RequirementIndex,
                        neededRequirement.Requirement,
                        resourceMatch.Value.ResourceType,
                        resourceMatch.Value.Amount);
                }
            }
        }

        return null;
    }

    private (Building SourceBuilding, IResourceStorage Storage, int RequirementIndex, ResourceRequirement Requirement, ResourceName ResourceType, int Amount)? GetBuilderSupplyOptionFromStorageBuildings(
        Scaffolding scaffold,
        IReadOnlyList<ScaffoldRequirementNeed> neededRequirements)
    {
        if (Cave is null)
        {
            return null;
        }

        foreach (var building in Cave.GetBuildingList()
                     .Where(building => building is not MiningPost && building is IResourceStorage)
                     .OrderBy(building => Cave.GetBuildingBfsFieldValue(building, Location))
                     .ThenBy(building => building.Location is null ? int.MaxValue : GridPoint.SquaredDistance(Location, building.Location.Value))
                     .ThenBy(GetOwnedBuildingSelectionKey, StringComparer.Ordinal))
        {
            if (building is not IResourceStorage storage || !CanReachResourceStorage(building))
            {
                continue;
            }

            foreach (var neededRequirement in neededRequirements)
            {
                var reserveAmount = System.Math.Min(InventoryCapacity, neededRequirement.Amount);
                var resourceMatch = storage.FindStoredResource(neededRequirement.Requirement, reserveAmount);
                if (resourceMatch is not null)
                {
                    return (
                        building,
                        storage,
                        neededRequirement.RequirementIndex,
                        neededRequirement.Requirement,
                        resourceMatch.Value.ResourceType,
                        resourceMatch.Value.Amount);
                }
            }
        }

        return null;
    }

    public (Building SourceBuilding, IResourceStorage Storage, int RequirementIndex, ResourceRequirement Requirement, ResourceName ResourceType, int Amount)? GetBuilderSupplyOptionForScaffold(Scaffolding scaffold, IReadOnlyList<MiningPost>? orderedPosts = null)
    {
        var neededRequirements = scaffold.GetNeededRequirements(true, this);
        if (neededRequirements.Count == 0)
        {
            return null;
        }

        if (orderedPosts is not null)
        {
            return GetBuilderSupplyOptionFromMiningPosts(scaffold, neededRequirements, orderedPosts, orderedCandidates: true);
        }

        return GetBuilderSupplyOptionFromMiningPosts(scaffold, neededRequirements, EnumerateMiningPostCandidates("builder-supply")) ??
               GetBuilderSupplyOptionFromStorageBuildings(scaffold, neededRequirements);
    }

    private bool CanReachResourceStorage(Building building)
    {
        return Cave is not null &&
               building.Location is not null &&
               building.TileArray.Count > 0 &&
               (IsAtResourceStorageSource(building) || Cave.GetBuildingBfsFieldValue(building, Location) != int.MaxValue);
    }

    private bool IsAtResourceStorageSource(Building building)
    {
        return ReservedZone is { Purpose: InteractionZonePurpose.ResourceTransfer } zone &&
               ReferenceEquals(zone.Owner, building) &&
               IsAtReservedInteractionSlot();
    }

    public bool CanActOnScaffold(Scaffolding scaffold)
    {
        if (!scaffold.IsInProgress())
        {
            return false;
        }

        if (!CanReachScaffolding(scaffold))
        {
            return false;
        }

        if (HasInventory())
        {
            return scaffold.NeedsResource(Inventory.Type!.Value);
        }

        if (scaffold.GetMaterialReservation(this) is not null && BuilderSourceBuilding is not null)
        {
            return true;
        }

        if (!scaffold.IsRecipeComplete())
        {
            return GetBuilderSupplyOptionForScaffold(scaffold) is not null;
        }

        if (scaffold.NeedsConstructionWork())
        {
            return true;
        }

        return scaffold.IsConstructionComplete() && !scaffold.CompletionPending;
    }

    public List<Scaffolding> GetScaffoldingPriorityList(bool actionableOnly = false, IEnumerable<Scaffolding>? excludeScaffolds = null)
    {
        var excluded = excludeScaffolds?.ToHashSet() ?? [];
        return GetScaffoldingBuildings()
            .Where(scaffold =>
                !excluded.Contains(scaffold) &&
                (Cave?.GetBuildingBfsFieldValue(scaffold, Location) ?? int.MaxValue) != int.MaxValue &&
                (!actionableOnly || CanActOnScaffold(scaffold)))
            .OrderBy(scaffold => scaffold.GetVolume())
            .ThenBy(scaffold => Cave?.GetBuildingBfsFieldValue(scaffold, Location) ?? int.MaxValue)
            .ThenBy(scaffold => scaffold.Location is null ? int.MaxValue : GridPoint.SquaredDistance(Location, scaffold.Location.Value))
            .ToList();
    }

    private Scaffolding? GetBestScaffolding(bool actionableOnly = false, ISet<Scaffolding>? excludedScaffolds = null)
    {
        Scaffolding? bestScaffold = null;
        var bestVolume = int.MaxValue;
        var bestBfs = int.MaxValue;
        var bestDistance = int.MaxValue;
        string? bestKey = null;

        foreach (var scaffold in GetScaffoldingBuildings())
        {
            if (excludedScaffolds?.Contains(scaffold) == true)
            {
                continue;
            }

            var bfsValue = Cave?.GetBuildingBfsFieldValue(scaffold, Location) ?? int.MaxValue;
            if (bfsValue == int.MaxValue || (actionableOnly && !CanActOnScaffold(scaffold)))
            {
                continue;
            }

            var volume = scaffold.GetVolume();
            var distance = scaffold.Location is null ? int.MaxValue : GridPoint.SquaredDistance(Location, scaffold.Location.Value);
            var tieKey = scaffold.Location?.ToString() ?? scaffold.Name;
            if (bestScaffold is null ||
                volume < bestVolume ||
                (volume == bestVolume && bfsValue < bestBfs) ||
                (volume == bestVolume && bfsValue == bestBfs && distance < bestDistance) ||
                (volume == bestVolume && bfsValue == bestBfs && distance == bestDistance && string.CompareOrdinal(tieKey, bestKey) < 0))
            {
                bestScaffold = scaffold;
                bestVolume = volume;
                bestBfs = bfsValue;
                bestDistance = distance;
                bestKey = tieKey;
            }
        }

        return bestScaffold;
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

    public Scaffolding? EnsureBuilderAssignment(bool actionableOnly = false, IEnumerable<Scaffolding>? excludeScaffolds = null)
    {
        var excluded = excludeScaffolds?.ToHashSet() ?? [];
        var assignedScaffold = GetAssignedScaffolding();
        if (assignedScaffold is not null &&
            assignedScaffold.IsInProgress() &&
            CanReachScaffolding(assignedScaffold) &&
            !excluded.Contains(assignedScaffold) &&
            (!actionableOnly || CanActOnScaffold(assignedScaffold)))
        {
            assignedScaffold.Assign(this);
            return assignedScaffold;
        }

        if (assignedScaffold is not null)
        {
            ReleaseAssignedBuilding();
        }

        var scaffold = GetBestScaffolding(actionableOnly, excluded);
        if (scaffold is null)
        {
            ReleaseAssignedBuilding();
            return null;
        }

        SetAssignedBuilding(scaffold);
        scaffold.Assign(this);
        return scaffold;
    }

    private bool AdvanceBuilderDepositExtraInventory()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        if (!HasInventory())
        {
            return AdvanceBuilderSelectScaffold();
        }

        var post = SelectMiningPostForInventoryDeposit();
        if (post is null)
        {
            return false;
        }

        if (ReservedZone is not { Purpose: InteractionZonePurpose.ResourceTransfer } transferZone ||
            !ReferenceEquals(transferZone.Owner, post) ||
            !IsAtReservedInteractionSlot())
        {
            if (!NavigateToInteractionZone(post, InteractionZonePurpose.ResourceTransfer))
            {
                return false;
            }

            QueueBuilderState(BuilderState.DepositExtraInventory);
            return true;
        }

        if (!TryDepositCarrierInventory(post, out _))
        {
            QueueBuilderState(BuilderState.WaitForMaterials, WorkerRoleFailureReason.NoStorage, result: false);
            return false;
        }

        return HasInventory() ? AdvanceBuilderDepositExtraInventory() : AdvanceBuilderSelectScaffold();
    }

    private bool AdvanceBuilderSelectScaffold()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var scaffold = EnsureBuilderAssignment(true);
        if (scaffold is null)
        {
            return HasInventory() ? AdvanceBuilderDepositExtraInventory() : false;
        }

        var scaffoldReservation = scaffold.GetMaterialReservation(this);
        var sourceBuilding = BuilderSourceBuilding;
        var postReservation = BuilderSourcePost?.GetMaterialReservation(this);

        if (HasInventory())
        {
            if (scaffold.NeedsResource(Inventory.Type!.Value))
            {
                return AdvanceBuilderDepositMaterial();
            }

            scaffold.ReleaseMaterialReservation(this);
            return AdvanceBuilderDepositExtraInventory();
        }

        if (scaffoldReservation is not null &&
            sourceBuilding is not null &&
            (BuilderSourcePost is null || postReservation is not null))
        {
            return AdvanceBuilderWithdrawMaterial();
        }

        if (scaffoldReservation is not null && sourceBuilding is null)
        {
            scaffold.ReleaseMaterialReservation(this);
        }
        else if (scaffoldReservation is null && sourceBuilding is not null)
        {
            ClearBuilderSourcePost();
        }

        if (scaffold.NeedsAnyResource(true, this) && AdvanceBuilderReserveMaterial())
        {
            return true;
        }

        if (scaffold.IsRecipeComplete() && scaffold.NeedsConstructionWork())
        {
            return AdvanceBuilderBuildScaffold();
        }

        if (scaffold.IsRecipeComplete() && scaffold.IsConstructionComplete() && scaffold.TryCompleteConstruction(this))
        {
            return true;
        }

        ReleaseAssignedBuilding();
        return false;
    }

    private bool AdvanceBuilderReserveMaterial()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var scaffold = GetAssignedScaffolding();
        if (scaffold is null)
        {
            QueueBuilderState(BuilderState.SelectScaffold);
            return false;
        }

        var supplyOption = GetBuilderSupplyOptionForScaffold(scaffold);
        if (supplyOption is null)
        {
            return false;
        }

        var scaffoldReserved = scaffold.ReserveMaterial(
            this,
            supplyOption.Value.RequirementIndex,
            supplyOption.Value.ResourceType,
            supplyOption.Value.Amount);
        if (scaffoldReserved <= 0)
        {
            return false;
        }

        if (supplyOption.Value.SourceBuilding is MiningPost sourcePost)
        {
            var postReserved = sourcePost.ReserveMaterial(this, supplyOption.Value.ResourceType, scaffoldReserved);
            if (postReserved != scaffoldReserved)
            {
                scaffold.ReleaseMaterialReservation(this);
                sourcePost.ReleaseMaterialReservation(this);
                return false;
            }
        }

        SetBuilderSource(supplyOption.Value.SourceBuilding);

        if (IsAtResourceStorageSource(supplyOption.Value.SourceBuilding))
        {
            return AdvanceBuilderWithdrawMaterial();
        }

        if (!NavigateToInteractionZone(
                supplyOption.Value.SourceBuilding,
                InteractionZonePurpose.ResourceTransfer))
        {
            scaffold.ReleaseMaterialReservation(this);
            ClearBuilderSourcePost();
            QueueBuilderState(BuilderState.SelectScaffold);
            return false;
        }

        QueueBuilderState(BuilderState.WithdrawMaterial);
        return true;
    }

    private bool AdvanceBuilderWithdrawMaterial()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var scaffold = GetAssignedScaffolding();
        var sourceBuilding = BuilderSourceBuilding;
        var storage = sourceBuilding as IResourceStorage;
        var scaffoldReservation = scaffold?.GetMaterialReservation(this);
        var postReservation = BuilderSourcePost?.GetMaterialReservation(this);

        if (scaffold is null ||
            sourceBuilding is null ||
            storage is null ||
            scaffoldReservation is null ||
            (BuilderSourcePost is not null &&
             (postReservation is null || scaffoldReservation.Value.ResourceType != postReservation.ResourceType)))
        {
            scaffold?.ReleaseMaterialReservation(this);
            ClearBuilderSourcePost();
            QueueBuilderState(BuilderState.SelectScaffold);
            return false;
        }

        if (HasInventory())
        {
            return AdvanceBuilderDepositMaterial();
        }

        if (!IsAtResourceStorageSource(sourceBuilding))
        {
            if (!NavigateToInteractionZone(sourceBuilding, InteractionZonePurpose.ResourceTransfer))
            {
                scaffold.ReleaseMaterialReservation(this);
                ClearBuilderSourcePost();
                QueueBuilderState(BuilderState.SelectScaffold);
                return false;
            }

            QueueBuilderState(BuilderState.WithdrawMaterial);
            return true;
        }

        var activeScaffoldReservation = scaffoldReservation.Value;
        var requestedAmount = System.Math.Min(GetInventorySpace(), activeScaffoldReservation.Amount);
        var withdrawnResourceType = activeScaffoldReservation.ResourceType;
        SetActivity(CreatureActivity.Hauling);
        var withdrawnAmount = BuilderSourcePost is not null
            ? BuilderSourcePost.WithdrawReservedMaterial(this, requestedAmount)?.Amount ?? 0
            : storage.Withdraw(withdrawnResourceType, requestedAmount);

        if (withdrawnAmount <= 0)
        {
            scaffold.ReleaseMaterialReservation(this);
            ClearBuilderSourcePost();
            QueueBuilderState(BuilderState.SelectScaffold);
            return false;
        }

        if (AddToInventory(withdrawnResourceType, withdrawnAmount) != withdrawnAmount)
        {
            storage.Deposit(withdrawnResourceType, withdrawnAmount);
            scaffold.ReleaseMaterialReservation(this);
            ClearBuilderSourcePost();
            QueueBuilderState(BuilderState.SelectScaffold);
            return false;
        }

        BuilderSourcePost = null;
        BuilderSourceBuilding = null;
        ReleaseInteractionReservation();
        return AdvanceBuilderDepositMaterial();
    }

    private bool AdvanceBuilderDepositMaterial()
    {
        if (!EnsureBuilderState())
        {
            return false;
        }

        var scaffold = GetAssignedScaffolding();
        if (!HasInventory())
        {
            QueueBuilderState(BuilderState.SelectScaffold);
            return false;
        }

        if (scaffold is null || !scaffold.IsInProgress())
        {
            return AdvanceBuilderDepositExtraInventory();
        }

        if (!scaffold.NeedsResource(Inventory.Type!.Value))
        {
            scaffold.ReleaseMaterialReservation(this);
            return AdvanceBuilderDepositExtraInventory();
        }

        if (ReservedZone is not { Purpose: InteractionZonePurpose.Construction } deliveryZone ||
            !ReferenceEquals(deliveryZone.Owner, scaffold) ||
            !IsAtReservedInteractionSlot())
        {
            if (!NavigateToInteractionZone(scaffold, InteractionZonePurpose.Construction))
            {
                scaffold.ReleaseMaterialReservation(this);
                QueueBuilderState(BuilderState.DepositExtraInventory);
                return false;
            }

            QueueBuilderState(BuilderState.DepositMaterial);
            return true;
        }

        if (!TryDepositCarrierInventory(scaffold, out _))
        {
            scaffold.ReleaseMaterialReservation(this);
            QueueBuilderState(BuilderState.DepositExtraInventory, WorkerRoleFailureReason.NoStorage, result: false);
            return false;
        }

        return HasInventory() ? AdvanceBuilderDepositExtraInventory() : AdvanceBuilderSelectScaffold();
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
            ReleaseAssignedBuilding();
            return false;
        }

        if (HasInventory())
        {
            return AdvanceBuilderDepositMaterial();
        }

        if (!scaffold.IsRecipeComplete())
        {
            QueueBuilderState(BuilderState.SelectScaffold);
            return false;
        }

        if (!scaffold.NeedsConstructionWork())
        {
            return AdvanceBuilderSelectScaffold();
        }

        if (ReservedZone is not { Purpose: InteractionZonePurpose.Construction } workZone ||
            !ReferenceEquals(workZone.Owner, scaffold) ||
            !IsAtReservedInteractionSlot())
        {
            if (!NavigateToInteractionZone(scaffold, InteractionZonePurpose.Construction))
            {
                QueueBuilderState(BuilderState.SelectScaffold);
                return false;
            }

            QueueBuilderState(BuilderState.BuildScaffold);
            return true;
        }

        SetActivity(CreatureActivity.Working);
        var worked = scaffold.ApplyConstructionWork(BuilderWorkRate, this);
        if (worked <= 0)
        {
            QueueBuilderState(BuilderState.SelectScaffold);
            return false;
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
            QueueBuilderState(BuilderState.SelectScaffold);
            return true;
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

        if (!NavigateTo(bestLocation.Value))
        {
            return false;
        }

        QueueBuilderState(BuilderState.SelectScaffold);
        return true;
    }
}
