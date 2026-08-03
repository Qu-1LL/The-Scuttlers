using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Combat;

// A deterministic uniform grid keeps narrow phase work local to the hitbox bounds.
public sealed class CombatSpatialGrid
{
    private readonly Dictionary<GridPoint, List<CombatHurtbox>> _buckets = [];
    private readonly List<CombatHurtbox> _results = [];
    private readonly HashSet<int> _seen = [];
    private readonly int _cellSize;

    public CombatSpatialGrid(int cellSize = WorldUnits.UnitsPerTile * 2)
    {
        _cellSize = Math.Max(1, cellSize);
    }

    public void Clear()
    {
        foreach (var bucket in _buckets.Values)
        {
            bucket.Clear();
        }
    }

    public void Add(CombatHurtbox hurtbox)
    {
        var bounds = hurtbox.Shape.GetBounds();
        var minX = FloorDiv(bounds.X);
        var maxX = FloorDiv(bounds.Right);
        var minY = FloorDiv(bounds.Y);
        var maxY = FloorDiv(bounds.Bottom);
        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                var key = new GridPoint(x, y);
                if (!_buckets.TryGetValue(key, out var bucket))
                {
                    bucket = [];
                    _buckets.Add(key, bucket);
                }

                bucket.Add(hurtbox);
            }
        }
    }

    public void Query(CombatShape shape)
    {
        _results.Clear();
        _seen.Clear();
        var bounds = shape.GetBounds();
        for (var x = FloorDiv(bounds.X); x <= FloorDiv(bounds.Right); x++)
        {
            for (var y = FloorDiv(bounds.Y); y <= FloorDiv(bounds.Bottom); y++)
            {
                if (!_buckets.TryGetValue(new GridPoint(x, y), out var bucket))
                {
                    continue;
                }

                for (var index = 0; index < bucket.Count; index++)
                {
                    var hurtbox = bucket[index];
                    if (_seen.Add(hurtbox.Id))
                    {
                        _results.Add(hurtbox);
                    }
                }
            }
        }

        for (var index = 1; index < _results.Count; index++)
        {
            var value = _results[index];
            var insert = index - 1;
            while (insert >= 0 && _results[insert].Id > value.Id)
            {
                _results[insert + 1] = _results[insert];
                insert--;
            }

            _results[insert + 1] = value;
        }
    }

    public IReadOnlyList<CombatHurtbox> Results => _results;

    private int FloorDiv(int value) => value >= 0 ? value / _cellSize : ((value + 1) / _cellSize) - 1;
}
