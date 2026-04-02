using TriloGame.Game.Core.World;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Events;

public static class GameEvents
{
    public const string TileMined = "tileMined";
    public const string WallMined = "wallMined";
    public const string AlgaeMined = "AlgaeMined";
    public const string SandstoneMined = "SandstoneMined";
    public const string MagnetiteMined = "MagnetiteMined";
    public const string MalachiteMined = "MalachiteMined";
    public const string PeroteneMined = "PeroteneMined";
    public const string IlmeniteMined = "IlmeniteMined";
    public const string CochiniumMined = "CochiniumMined";
}

public sealed record GameEventPayload(
    Cave? Cave,
    string? TileKey,
    GridPoint? Location,
    string? MinedType,
    string? ResourceType,
    object? Source);
