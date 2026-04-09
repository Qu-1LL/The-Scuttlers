using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Shared.State;

public readonly record struct DeathMistRequest(GridPoint OriginTile, int Radius);
