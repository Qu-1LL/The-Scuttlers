using System.Numerics;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public readonly record struct StationSlot(GridPoint? TileOffset = null, Vector2? LocalPixelOffset = null);
