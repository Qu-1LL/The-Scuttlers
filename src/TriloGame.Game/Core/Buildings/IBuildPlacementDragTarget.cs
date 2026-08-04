using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Buildings;

public enum BuildPlacementDragKind
{
    None,
    AxisLine,
    FootprintGrid
}

public interface IBuildPlacementDragTarget
{
    BuildPlacementDragKind DragPlacementKind { get; }

    GridPoint DragPlacementStep { get; }
}
