namespace TriloGame.Game.Core.Entities;

internal sealed class MiningPostSelectionMetrics
{
    public string Purpose { get; init; } = string.Empty;

    public string? PreferredPostKey { get; set; }

    public string? NearestOwnerPostKey { get; set; }

    public int CandidateCount { get; set; }

    public int FullScanFallbackCount { get; set; }

    public bool ReusedPreferredPost { get; set; }

    public bool UsedAdjacencyFallback { get; set; }
}
