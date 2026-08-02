using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Rendering.Lighting;

// How much of each tile of a building's footprint its sprite actually covers.
//
// The world tile grid is the only thing the ray march reads for full-height occlusion, and it is
// addressed per TILE - so a building occludes as its bounding footprint unless something tells the
// grid otherwise. For a 1x1 wall that is invisible, since the sprite fills its tile. For the radar,
// whose footprint is 4x4 and whose dish is round, it is a hard-edged 4x4 RECTANGLE of shadow with
// obvious corners where the sprite has none.
//
// Measuring the sprite's own alpha per footprint cell fixes that at the resolution the grid can
// actually express. It stays coarse - sixteen cells for the radar - but coarse and dish-shaped reads
// as soft occlusion, whereas coarse and rectangular reads as a bug. The finer silhouette continues to
// come from the sprite-shaped cast shadow in the scene layer, which is what carries the crisp edge at
// the building's base.
//
// Registered once from the sprite atlas because it needs GetData off the GPU, which is far too
// expensive to repeat per frame; the result depends only on the texture and the footprint, neither of
// which changes at runtime.
public sealed class BuildingOccluderCoverage
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    private readonly record struct Entry(float[] Cells, Point Size);

    public void Register(string textureKey, Texture2D texture, GridPoint footprintTiles)
    {
        if (string.IsNullOrEmpty(textureKey) || _entries.ContainsKey(textureKey))
        {
            return;
        }

        var width = Math.Max(1, footprintTiles.X);
        var height = Math.Max(1, footprintTiles.Y);
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        _entries[textureKey] = new Entry(
            MeasureCoverage(pixels, texture.Width, texture.Height, width, height),
            new Point(width, height));
    }

    // Mean alpha of every pixel falling in each footprint cell. Mean rather than "any opaque pixel"
    // so a cell the sprite barely clips reports a little occlusion rather than all of it - the value
    // becomes a transmission weight, so partial coverage has somewhere to go.
    internal static float[] MeasureCoverage(
        IReadOnlyList<Color> pixels,
        int textureWidth,
        int textureHeight,
        int cellsX,
        int cellsY)
    {
        var coverage = new float[cellsX * cellsY];
        var totals = new int[cellsX * cellsY];
        var safeWidth = Math.Max(1, textureWidth);
        var safeHeight = Math.Max(1, textureHeight);

        for (var y = 0; y < safeHeight; y++)
        {
            var cellY = Math.Min(cellsY - 1, y * cellsY / safeHeight);
            for (var x = 0; x < safeWidth; x++)
            {
                var cellX = Math.Min(cellsX - 1, x * cellsX / safeWidth);
                var index = (cellY * cellsX) + cellX;
                coverage[index] += pixels[(y * safeWidth) + x].A / 255f;
                totals[index]++;
            }
        }

        for (var index = 0; index < coverage.Length; index++)
        {
            coverage[index] = totals[index] > 0 ? coverage[index] / totals[index] : 0f;
        }

        return coverage;
    }

    // Coverage at a footprint cell, accounting for the building's display rotation.
    //
    // The measurement is stored in the texture's own orientation, but the footprint a rotated
    // building occupies is the rotated one, so the lookup has to be un-rotated first. Getting this
    // wrong does not fail loudly - it mirrors or transposes the occlusion against the sprite, which
    // looks like a slightly wrong shape rather than an error.
    public float GetCoverage(string textureKey, int localX, int localY, int rotationTurns)
    {
        if (!_entries.TryGetValue(textureKey, out var entry))
        {
            // Unmeasured buildings keep the old behaviour: the whole footprint occludes.
            return 1f;
        }

        var natural = ToNaturalCell(localX, localY, entry.Size, rotationTurns);
        if (natural.X < 0 || natural.Y < 0 || natural.X >= entry.Size.X || natural.Y >= entry.Size.Y)
        {
            return 1f;
        }

        return entry.Cells[(natural.Y * entry.Size.X) + natural.X];
    }

    // Inverse of an N-quarter-turn clockwise rotation, mapping a cell of the ROTATED footprint back
    // to the cell of the natural (unrotated) grid that landed there. naturalSize is the unrotated
    // grid's dimensions; for odd turns the rotated footprint has them swapped.
    internal static Point ToNaturalCell(int x, int y, Point naturalSize, int rotationTurns)
    {
        return (((rotationTurns % 4) + 4) % 4) switch
        {
            1 => new Point(y, naturalSize.Y - 1 - x),
            2 => new Point(naturalSize.X - 1 - x, naturalSize.Y - 1 - y),
            3 => new Point(naturalSize.X - 1 - y, x),
            _ => new Point(x, y)
        };
    }
}
