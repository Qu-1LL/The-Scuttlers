using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.World;
using TriloGame.Game.Rendering;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Rendering.Lighting;

public readonly record struct LightingTileGridLayout(Point Origin, Point Size)
{
    public int Width => Size.X;

    public int Height => Size.Y;

    public bool Contains(Point coordinates)
    {
        return coordinates.X >= Origin.X &&
               coordinates.Y >= Origin.Y &&
               coordinates.X < Origin.X + Size.X &&
               coordinates.Y < Origin.Y + Size.Y;
    }

    public int GetIndex(Point coordinates)
    {
        return ((coordinates.Y - Origin.Y) * Size.X) + (coordinates.X - Origin.X);
    }

    // Compute a stable tile rectangle around the camera footprint, including one-cell sampling padding.
    public static LightingTileGridLayout Create(CameraController camera, Point viewportSize)
    {
        camera.GetVisibleWorldBounds(viewportSize, out var topLeft, out var bottomRight);
        var minWorldX = MathF.Min(topLeft.X, bottomRight.X);
        var minWorldY = MathF.Min(topLeft.Y, bottomRight.Y);
        var maxWorldX = MathF.Max(topLeft.X, bottomRight.X);
        var maxWorldY = MathF.Max(topLeft.Y, bottomRight.Y);
        var padding = 1;
        var minX = (int)MathF.Floor((minWorldX + TileConstants.TileHalfSize) / TileConstants.TileSize) - padding;
        var minY = (int)MathF.Floor((minWorldY + TileConstants.TileHalfSize) / TileConstants.TileSize) - padding;
        var maxX = (int)MathF.Floor((maxWorldX + TileConstants.TileHalfSize) / TileConstants.TileSize) + padding;
        var maxY = (int)MathF.Floor((maxWorldY + TileConstants.TileHalfSize) / TileConstants.TileSize) + padding;
        return new LightingTileGridLayout(
            new Point(minX, minY),
            new Point(Math.Max(1, maxX - minX + 1), Math.Max(1, maxY - minY + 1)));
    }
}

public sealed class LightingTileGrid : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private Color[] _cells = [];
    private Color[] _emissionColors = [];
    private LightingTileGridLayout _layout;

    public LightingTileGrid(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    public Texture2D? Texture { get; private set; }

    // RGB = the emitting ore's light colour for that cell. Kept in a companion texture because
    // the state texture has no spare channels (R = blocker, G = known, B = emission strength).
    public Texture2D? EmissionColorTexture { get; private set; }

    public LightingTileGridLayout Layout => _layout;

    // Upload the authoritative tile state once per frame for shader ray queries.
    public void Update(
        Cave cave,
        CameraController camera,
        Point viewportSize,
        bool showFullMapVisibility,
        IReadOnlyList<OreLightEmitter> emitters)
    {
        var layout = LightingTileGridLayout.Create(camera, viewportSize);
        EnsureTexture(layout);

        for (var y = 0; y < layout.Height; y++)
        {
            for (var x = 0; x < layout.Width; x++)
            {
                var coordinates = new Point(layout.Origin.X + x, layout.Origin.Y + y);
                var tile = cave.GetTile(new GridPoint(coordinates.X, coordinates.Y));
                var index = (y * layout.Width) + x;
                if (tile is null)
                {
                    _cells[index] = new Color(1f, 0f, 0f, 1f);
                    continue;
                }

                var known = showFullMapVisibility || cave.IsTileRevealed(tile);
                _cells[index] = EncodeCell(tile, known);
            }
        }

        // Building textures can cover open-map cells whose Tile.Built remains null; cover the full
        // completed footprint so those visual layers cannot leak light through their interiors.
        foreach (var building in cave.Buildings)
        {
            if (!LightingTileClassifier.IsBuildingOccluder(building))
            {
                continue;
            }

            for (var tileIndex = 0; tileIndex < building.TileArray.Count; tileIndex++)
            {
                var point = building.TileArray[tileIndex].Coordinates;
                var coordinates = new Point(point.X, point.Y);
                if (!layout.Contains(coordinates))
                {
                    continue;
                }

                var cellIndex = layout.GetIndex(coordinates);
                var cell = _cells[cellIndex];
                _cells[cellIndex] = new Color(1f, cell.G / 255f, cell.B / 255f, 1f);
            }
        }

        Array.Clear(_emissionColors);
        for (var index = 0; index < emitters.Count; index++)
        {
            var emitter = emitters[index];
            var emitterPoint = new Point(emitter.Coordinates.X, emitter.Coordinates.Y);
            if (!layout.Contains(emitterPoint))
            {
                continue;
            }

            var cellIndex = layout.GetIndex(emitterPoint);
            var cell = _cells[cellIndex];
            var strength = Math.Clamp(emitter.Intensity, 0f, 1f);
            // Keep the brighter emitter when several land on one cell, and take its colour with
            // it so strength and colour never come from different deposits.
            if (strength >= cell.B / 255f)
            {
                _emissionColors[cellIndex] = emitter.LightColor;
            }

            _cells[cellIndex] = new Color(
                cell.R / 255f,
                cell.G / 255f,
                MathF.Max(cell.B / 255f, strength),
                1f);
        }

        Texture!.SetData(_cells);
        EmissionColorTexture!.SetData(_emissionColors);
    }

    public static Color EncodeCell(Tile? tile, bool known, float emission = 0f)
    {
        // Only full-height blockers go in the grid: it is the off-screen fallback, and a short
        // caster's occlusion depends on how far the receiver is, which cannot be evaluated there.
        var blocker = tile is null || !known || LightingTileClassifier.BlocksLightAtAnyDistance(tile);
        return new Color(
            blocker ? 1f : 0f,
            known && tile is not null ? 1f : 0f,
            Math.Clamp(emission, 0f, 1f),
            1f);
    }

    public void Dispose()
    {
        Texture?.Dispose();
        Texture = null;
        EmissionColorTexture?.Dispose();
        EmissionColorTexture = null;
        _cells = [];
        _emissionColors = [];
        GC.SuppressFinalize(this);
    }

    private void EnsureTexture(LightingTileGridLayout layout)
    {
        if (Texture is not null && layout == _layout)
        {
            return;
        }

        Texture?.Dispose();
        EmissionColorTexture?.Dispose();
        Texture = new Texture2D(
            _graphicsDevice,
            layout.Width,
            layout.Height,
            false,
            SurfaceFormat.Color);
        EmissionColorTexture = new Texture2D(
            _graphicsDevice,
            layout.Width,
            layout.Height,
            false,
            SurfaceFormat.Color);
        _layout = layout;
        _cells = new Color[layout.Width * layout.Height];
        _emissionColors = new Color[layout.Width * layout.Height];
    }
}
