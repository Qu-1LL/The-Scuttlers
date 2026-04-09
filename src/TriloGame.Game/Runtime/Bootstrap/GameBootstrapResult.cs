using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Runtime.Bootstrap;

public sealed record GameBootstrapResult(
    GameSession Session,
    GridPoint QueenLocation,
    GridPoint MiningPostLocation);
