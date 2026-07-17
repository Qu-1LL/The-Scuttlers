using TriloGame.Game.Audio;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Interaction;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Combat;

// Mining owns its original claim, reach, timing, and result rules independently of combat.
public sealed class MiningStrikeSystem
{
    private const int StrikeWindupTicks = 1;
    private static readonly int MiningPointDisplayRadius = WorldUnits.FromPixels(18);
    private static readonly int MiningCenterZoneInset = WorldUnits.UnitsPerTile / 4;
    private readonly List<MiningStrike> _active = [];
    private readonly List<MiningStrikeIntent> _pending = [];
    private readonly HashSet<int> _activeSources = [];
    private readonly HashSet<int> _pendingSources = [];
    private int _nextId = 1;

    public IReadOnlyList<MiningStrike> Active => _active;

    public MiningStrike? GetActiveFor(Creature source)
    {
        for (var index = 0; index < _active.Count; index++) if (_active[index].SourceId == source.Id) return _active[index];
        return null;
    }

    public bool HasActiveOrPending(Creature source) => _activeSources.Contains(source.Id) || _pendingSources.Contains(source.Id);

    public static bool CanMineReach(Creature source, string tileKey) =>
        source.Cave?.GetTile(tileKey) is { } tile && Building.IsMineableType(tile.Base) && TryBuildMiningPoint(source, tile, out _);

    public bool TryQueueMining(Creature source, string tileKey)
    {
        if (source.Health <= 0 || source.Cave?.GetTile(tileKey) is not { } tile || !Building.IsMineableType(tile.Base) ||
            !CanMineReach(source, tileKey) || HasActiveOrPending(source) ||
            (source is Trilobite trilobite && trilobite.ActiveMiningClaim?.TileKey != tileKey)) return false;
        _pending.Add(new MiningStrikeIntent(source, tileKey));
        _pendingSources.Add(source.Id);
        source.SetActivity(CreatureActivity.Working);
        return true;
    }

    public void RemoveFor(Creature source)
    {
        for (var index = _active.Count - 1; index >= 0; index--) if (_active[index].SourceId == source.Id) { _active.RemoveAt(index); _activeSources.Remove(source.Id); }
        for (var index = _pending.Count - 1; index >= 0; index--) if (_pending[index].SourceId == source.Id) _pending.RemoveAt(index);
        _pendingSources.Remove(source.Id);
    }

    public void Advance(GameSession session)
    {
        for (var index = _active.Count - 1; index >= 0; index--)
        {
            var strike = _active[index];
            if (strike.Source.Health <= 0 || strike.Source.Cave is null) { _active.RemoveAt(index); _activeSources.Remove(strike.SourceId); continue; }
            if (!strike.Resolved && session.TickCount >= strike.ResolveTick)
            {
                Resolve(strike);
                strike.Resolved = true;
            }
            if (session.TickCount >= strike.ExpireTick) { _active.RemoveAt(index); _activeSources.Remove(strike.SourceId); }
        }

        for (var index = 0; index < _pending.Count; index++)
        {
            var intent = _pending[index];
            if (intent.Source.Health <= 0 || intent.Source.Cave is null) continue;
            if (intent.Source.Cave.GetTile(intent.TileKey) is not { } tile || !Building.IsMineableType(tile.Base) ||
                !TryBuildMiningPoint(intent.Source, tile, out var center)) { NotifyResult(intent.Source, MineTileResult.NotApplied); continue; }
            var direction = RectangleCenter(MiningCenterZone(tile.Coordinates)) - intent.Source.Position;
            if (direction.IsZero) direction = intent.Source.FacingDirection;
            intent.Source.CancelMovement(); intent.Source.Face(direction); intent.Source.SnapPresentationPose();
            _active.Add(new MiningStrike
            {
                Id = _nextId++, Source = intent.Source, SourceId = intent.Source.Id, TileKey = intent.TileKey,
                Center = center, Radius = MiningPointDisplayRadius, SpawnTick = session.TickCount,
                ResolveTick = session.TickCount + StrikeWindupTicks, ExpireTick = session.TickCount + StrikeWindupTicks
            });
            _activeSources.Add(intent.Source.Id);
            session.RequestAudioCueOncePerTick(GameAudioCue.MiningStrike, intent.Source.Position, AudioCueRequest.CreatureEffectFootprintTiles);
        }
        _pending.Clear(); _pendingSources.Clear();
    }

    private static void Resolve(MiningStrike strike)
    {
        if (strike.TileKey is null || strike.Source.Cave is not { } cave || cave.GetTile(strike.TileKey) is not { } tile ||
            !Building.IsMineableType(tile.Base) || strike.Source.Velocity.Length > Math.Max(1, strike.Source.BaseSpeed / 8) ||
            (strike.Source is Trilobite claimant && claimant.ActiveMiningClaim?.TileKey != strike.TileKey) ||
            !TryBuildMiningPoint(strike.Source, tile, out _)) { NotifyResult(strike.Source, MineTileResult.NotApplied); return; }
        var result = strike.Source is Trilobite trilobite ? trilobite.MineTile(strike.TileKey) : strike.Source.Session.MineTile(cave, strike.TileKey, source: strike.Source);
        NotifyResult(strike.Source, result);
    }

    private static void NotifyResult(Creature source, MineTileResult result) { if (source is Trilobite trilobite) trilobite.RecordMiningStrikeResult(result); }

    private static bool TryBuildMiningPoint(Creature source, Tile tile, out WorldPoint center) =>
        TryBuildMiningPoint(source, tile, (RectangleCenter(MiningCenterZone(tile.Coordinates)) - source.Position), out center);

    private static bool TryBuildMiningPoint(Creature source, Tile tile, WorldVector direction, out WorldPoint center)
    {
        var zone = MiningCenterZone(tile.Coordinates);
        if (direction.IsZero) center = ClampPointToRectangle(source.Position, zone);
        else if (!TryIntersectRayWithRectangle(source.Position, direction, zone, out center)) center = ClampPointToRectangle(source.Position, zone);
        var reach = WorldUnits.UnitsPerTile + source.CollisionRadius;
        return (center - source.Position).LengthSquared <= (long)reach * reach;
    }

    private static WorldRectangle TileBounds(GridPoint point) => new(
        (point.X * WorldUnits.UnitsPerTile) - WorldUnits.UnitsPerHalfTile,
        (point.Y * WorldUnits.UnitsPerTile) - WorldUnits.UnitsPerHalfTile,
        WorldUnits.UnitsPerTile, WorldUnits.UnitsPerTile);

    private static WorldRectangle MiningCenterZone(GridPoint point)
    {
        var bounds = TileBounds(point);
        return new WorldRectangle(bounds.X + MiningCenterZoneInset, bounds.Y + MiningCenterZoneInset,
            bounds.Width - MiningCenterZoneInset * 2, bounds.Height - MiningCenterZoneInset * 2);
    }

    private static WorldPoint RectangleCenter(WorldRectangle bounds) => new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    private static WorldPoint ClampPointToRectangle(WorldPoint point, WorldRectangle bounds) => new(Math.Clamp(point.X, bounds.X, bounds.Right), Math.Clamp(point.Y, bounds.Y, bounds.Bottom));

    private static bool TryIntersectRayWithRectangle(WorldPoint origin, WorldVector direction, WorldRectangle bounds, out WorldPoint point)
    {
        point = default; var minT = double.NegativeInfinity; var maxT = double.PositiveInfinity;
        if (!ClipRayAxis(origin.X, direction.X, bounds.X, bounds.Right, ref minT, ref maxT) || !ClipRayAxis(origin.Y, direction.Y, bounds.Y, bounds.Bottom, ref minT, ref maxT) || maxT < 0d) return false;
        var t = Math.Max(0d, minT); point = new WorldPoint((int)Math.Round(origin.X + direction.X * t), (int)Math.Round(origin.Y + direction.Y * t)); return true;
    }

    private static bool ClipRayAxis(int origin, int direction, int min, int max, ref double minT, ref double maxT)
    {
        if (direction == 0) return origin >= min && origin <= max;
        var t1 = (min - origin) / (double)direction; var t2 = (max - origin) / (double)direction;
        if (t1 > t2) (t1, t2) = (t2, t1); minT = Math.Max(minT, t1); maxT = Math.Min(maxT, t2); return minT <= maxT;
    }
}

public sealed class MiningStrike
{
    public required int Id { get; init; }
    public required int SourceId { get; init; }
    public required Creature Source { get; init; }
    public required string TileKey { get; init; }
    public WorldPoint Center { get; init; }
    public int Radius { get; init; }
    public int SpawnTick { get; init; }
    public int ResolveTick { get; init; }
    public int ExpireTick { get; init; }
    public bool Resolved { get; internal set; }
}

internal readonly record struct MiningStrikeIntent(Creature Source, string TileKey) { public int SourceId => Source.Id; }
