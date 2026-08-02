using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Economy;
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

    // Tile rectangle covering the camera footprint plus enough margin for the ray march to reach its
    // full distance without running off the edge of the data - see OreLightSettings.TileGridPadding-
    // Tiles for why the margin has to be the light range rather than a cell or two.
    public static LightingTileGridLayout Create(CameraController camera, Point viewportSize, float lightRangeTiles)
    {
        camera.GetVisibleWorldBounds(viewportSize, out var topLeft, out var bottomRight);
        var minWorldX = MathF.Min(topLeft.X, bottomRight.X);
        var minWorldY = MathF.Min(topLeft.Y, bottomRight.Y);
        var maxWorldX = MathF.Max(topLeft.X, bottomRight.X);
        var maxWorldY = MathF.Max(topLeft.Y, bottomRight.Y);
        // Padding tracks the RANGE rather than a fixed constant, because the range is no longer fixed:
        // the ray intervals widen with probe spacing at wide zoom (see GetBaseIntervalWorld). It uses
        // the capped range, not the raw reach - light past OreLightRangeTiles has already faded to
        // nothing, so covering it would cost cells for no light. That cap is what keeps this bounded:
        // at the widest rung it is 64 tiles rather than the 136 the uncapped reach would ask for.
        var padding = (int)MathF.Ceiling(MathF.Max(1f, lightRangeTiles)) + 2;

        // Size is derived from the viewport's own world EXTENT, not from the gap between two
        // independently floored tile coordinates. Those two floors cross their boundaries on
        // different frames while panning, so a size computed from their difference oscillates
        // between N and N+1 on arbitrary frames - and every oscillation used to destroy and
        // reallocate both grid textures mid-pan. Derived this way the size depends only on the zoom,
        // so panning moves the ORIGIN and nothing else, and EnsureTexture can keep its buffers.
        var width = (int)MathF.Ceiling((maxWorldX - minWorldX) / TileConstants.TileSize) + 2 + (padding * 2);
        var height = (int)MathF.Ceiling((maxWorldY - minWorldY) / TileConstants.TileSize) + 2 + (padding * 2);

        return new LightingTileGridLayout(
            new Point(ToTileCoordinate(minWorldX) - padding, ToTileCoordinate(minWorldY) - padding),
            new Point(Math.Max(1, width), Math.Max(1, height)));
    }

    // Matches WorldToTileCoordinate in RadianceCascade.fx, so a world position resolves to the same
    // cell on the CPU and in the shader.
    internal static int ToTileCoordinate(float world)
    {
        return (int)MathF.Floor((world + TileConstants.TileHalfSize) / TileConstants.TileSize);
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

    // Which way light arrives at a world position, and how directional it is, read off the same
    // emission data the cascades march through.
    //
    // This is the grid, not the drawn-emitter list: the grid spans the whole lighting footprint,
    // so a deposit well off screen still steers the shadow, and it never popped as the camera
    // panned. The old caller searched the SCREEN-culled emitter list within 14 tiles and, finding
    // nothing in a cave this sparse, fell back to a hardcoded "straight down" - which is why
    // creature shadows appeared stuck at one angle.
    //
    // Strength is the anisotropy: the length of the pull vector over the total pull, so it is 1 when
    // every contribution agrees on a direction and 0 when they cancel. Weak or contradictory light
    // therefore reports weak rather than defaulting to some arbitrary angle, and the caller can fade
    // the shadow out instead of drawing a wrong one.
    public bool TryGetLightDirection(
        Vector2 worldPosition,
        float radiusTiles,
        out Vector2 direction,
        out float strength,
        out float lightDistance)
    {
        return TryGetLightDirection(
            _cells, _layout, worldPosition, radiusTiles, out direction, out strength, out lightDistance);
    }

    // Split out from the instance method so it can be tested without a GraphicsDevice: the cells are
    // only ever populated through Update, which needs one.
    internal static bool TryGetLightDirection(
        IReadOnlyList<Color> cells,
        LightingTileGridLayout layout,
        Vector2 worldPosition,
        float radiusTiles,
        out Vector2 direction,
        out float strength,
        out float lightDistance)
    {
        direction = Vector2.Zero;
        strength = 0f;
        lightDistance = 0f;
        if (cells.Count == 0)
        {
            return false;
        }

        var centre = new Point(
            LightingTileGridLayout.ToTileCoordinate(worldPosition.X),
            LightingTileGridLayout.ToTileCoordinate(worldPosition.Y));
        var radius = Math.Max(1, (int)MathF.Ceiling(radiusTiles));
        var pull = Vector2.Zero;
        var total = 0f;
        var weightedDistance = 0f;

        for (var y = -radius; y <= radius; y++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                var coordinates = new Point(centre.X + x, centre.Y + y);
                if (!layout.Contains(coordinates))
                {
                    continue;
                }

                // B holds emission strength - see EncodeCell.
                var emission = cells[layout.GetIndex(coordinates)].B / 255f;
                if (emission <= 0f)
                {
                    continue;
                }

                var cellCentre = new Vector2(
                    coordinates.X * TileConstants.TileSize,
                    coordinates.Y * TileConstants.TileSize);
                var toLight = cellCentre - worldPosition;
                var distanceSquared = toLight.LengthSquared();
                if (distanceSquared < 1f)
                {
                    continue;
                }

                // Inverse square, matching the falloff the cascades themselves produce.
                var distance = MathF.Sqrt(distanceSquared);
                var weight = emission / distanceSquared;
                pull += (toLight / distance) * weight;
                total += weight;
                // Weighted by the same inverse square, so this lands on the light that actually
                // dominates rather than the average of everything in range.
                weightedDistance += distance * weight;
            }
        }

        if (total <= 0f)
        {
            return false;
        }

        // Relative to the total, never an absolute epsilon.
        //
        // These weights are inverse-square in WORLD units, where a tile is 512 across - so a deposit
        // four tiles away contributes about 2e-7. An absolute cutoff of 1e-4 rejected every real
        // emitter in the cave and this returned false unconditionally, which on screen is
        // indistinguishable from a shadow that draws but is too faint to see.
        //
        // The meaningful quantity is the ratio: pull length over total weight is the anisotropy, and
        // it is scale-free. Only exact cancellation needs guarding here; anything merely weak is the
        // caller's threshold to apply.
        var length = pull.Length();
        if (length <= total * 0.000001f)
        {
            return false;
        }

        direction = pull / length;
        strength = Math.Clamp(length / total, 0f, 1f);
        lightDistance = weightedDistance / total;
        return true;
    }

    // Upload the authoritative tile state once per frame for shader ray queries.
    //
    // Emission is resolved INSIDE this loop rather than from a pre-collected emitter list. The list
    // used to come from the drawn tile set, which only covered the screen plus a couple of tiles, so
    // deposits further out emitted nothing and switched on as the camera panned them into view.
    // Collecting over the lighting footprint instead fixed that but cost a second full enumeration of
    // several thousand tiles per frame - and this loop has already fetched every one of those tiles.
    // Reading the ore off the tile in hand makes the wider coverage essentially free.
    public void Update(
        Cave cave,
        CameraController camera,
        Point viewportSize,
        bool showFullMapVisibility,
        WorldSpriteEffectSystem spriteEffects,
        OreLightColorPalette palette,
        float lightRangeTiles,
        BuildingOccluderCoverage? coverages = null)
    {
        var layout = LightingTileGridLayout.Create(camera, viewportSize, lightRangeTiles);
        EnsureTexture(layout);
        Array.Clear(_emissionColors);

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
                var emission = GetEmissionStrength(tile, known, spriteEffects, palette, out var emissionColor);
                if (emission > 0f)
                {
                    _emissionColors[index] = emissionColor;
                }

                _cells[index] = EncodeCell(tile, known, emission);
            }
        }

        // Building textures can cover open-map cells whose Tile.Built remains null; cover the full
        // completed footprint so those visual layers cannot leak light through their interiors.
        //
        // Weighted by how much of each cell the sprite actually covers, so a building occludes in its
        // own shape rather than as its bounding box. The radar is the case that forced this: a 4x4
        // footprint around a round dish was a hard-cornered rectangle of shadow. See
        // BuildingOccluderCoverage.
        foreach (var building in cave.Buildings)
        {
            if (!LightingTileClassifier.IsBuildingOccluder(building) || building.Location is not { } origin)
            {
                continue;
            }

            var transmission = LightingTileClassifier.GetBuildingOccluderHeight(building).GetLightTransmission();
            var turns = building.GetDisplayRotationTurns();
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
                // Unexplored cells stay fully opaque whatever stands on them, or light leaks in from
                // space the player has not revealed.
                if (cell.G == 0)
                {
                    continue;
                }

                var coverage = coverages?.GetCoverage(
                    building.TextureKey, point.X - origin.X, point.Y - origin.Y, turns) ?? 1f;
                // Fully covered cells attenuate by the material's own transmission; uncovered ones
                // are left clear. Anything between interpolates, which is what stops the dish's edge
                // cells reading as a hard step.
                var cellTransmission = MathHelper.Lerp(1f, transmission, Math.Clamp(coverage, 0f, 1f));

                // Combined against the TERRAIN's own opacity, deliberately not against what is
                // already in the cell. EncodeCell resolves a tile's height through Tile.Built, so the
                // first pass has already written this building's full rectangular footprint here;
                // taking the maximum with that would reinstate the rectangle and discard every bit of
                // coverage weighting. Reading the terrain directly keeps the one property the maximum
                // was there for - a wall standing on solid rock must not become a window - without
                // letting the building's own bounding box back in.
                var terrainOpacity = 1f - LightingTileClassifier
                    .GetTerrainOccluderHeight(building.TileArray[tileIndex])
                    .GetLightTransmission();
                var opacity = MathF.Max(terrainOpacity, 1f - cellTransmission);
                _cells[cellIndex] = new Color(opacity, cell.G / 255f, cell.B / 255f, 1f);
            }
        }

        Texture!.SetData(_cells);
        EmissionColorTexture!.SetData(_emissionColors);
    }

    // Emission strength for a cell, recording its colour as a side effect so strength and colour can
    // never come from different deposits. Mirrors LightingSourceCollector.CollectOreEmitters, which
    // still feeds the SCREEN-space passes (the drawn halo sprites and creature contact shadows);
    // those are legitimately limited to what is on screen, unlike the ray march.
    internal static float GetEmissionStrength(
        Tile tile,
        bool known,
        WorldSpriteEffectSystem spriteEffects,
        OreLightColorPalette? palette,
        out Color color)
    {
        color = Color.White;
        if (!known || !tile.IsOreTile() || !OreType.TryGet(tile.Base, out var oreType))
        {
            return 0f;
        }

        var intensity = OreLightSettings.OreIntensity;
        if (string.Equals(oreType.Name, OreType.LUMENITE.Name, StringComparison.Ordinal))
        {
            intensity *= spriteEffects.GetAlphaMultiplier(
                oreType.Name,
                WorldSceneRenderer.GetWorldSpritePhaseOffsetSeconds(oreType.Name, tile.Coordinates));
        }

        color = palette?.GetLightColor(oreType.Name) ?? Color.White;
        return Math.Clamp(intensity, 0f, 1f);
    }

    public static Color EncodeCell(Tile? tile, bool known, float emission = 0f)
    {
        // R is OPACITY, not a blocker flag: 0 passes light untouched, 1 stops it dead, and anything
        // between attenuates. It used to be binary, with a single WallTransmission constant applied
        // in the shader to every cell that tripped it - which is why solid rock and a built wall were
        // indistinguishable to the ray march however they were labelled here.
        //
        // Only full-height blockers go in the grid: it is the off-screen fallback, and a short
        // caster's occlusion depends on how far the receiver is, which cannot be evaluated there.
        // Unknown and off-map cells stay fully opaque so light cannot leak in from unexplored space.
        var transmission = tile is null || !known
            ? 0f
            : LightingTileClassifier.GetOccluderHeight(tile).GetLightTransmission();
        return new Color(
            1f - Math.Clamp(transmission, 0f, 1f),
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

    // Reallocates on SIZE only. The origin changes every time the camera pans a tile, and tying
    // allocation to it meant two Texture2D disposals plus two creations on those frames - GPU
    // allocation churn in the middle of exactly the camera motion that most needs to stay smooth.
    // The contents are fully rewritten by SetData every frame regardless, so a moved origin needs no
    // new storage. Size now depends only on the zoom (see Create), so this fires on zoom and
    // viewport changes and essentially never while panning.
    private void EnsureTexture(LightingTileGridLayout layout)
    {
        if (Texture is not null && layout.Size == _layout.Size)
        {
            _layout = layout;
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
