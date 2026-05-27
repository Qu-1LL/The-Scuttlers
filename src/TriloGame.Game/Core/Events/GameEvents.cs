using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Events;

public static class GameEvents
{
    public const string TileMined = "tileMined";
    public const string WallMined = "wallMined";
    public const string LumeniteMined = "LumeniteMined";
    public const string ChitinstoneMined = "ChitinstoneMined";
    public const string MycocoreMined = "MycocoreMined";
}

public sealed record GameEventPayload(
    Cave? Cave,
    string? TileKey,
    GridPoint? Location,
    string? MinedType,
    string? ResourceType,
    object? Source);
