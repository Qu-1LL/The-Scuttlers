using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;

namespace TriloGame.Game.UI.Selection;

internal sealed class BuildingPlacementCursor
{
    private readonly Factory _factory;
    private readonly GameSession _session;
    private int _displayRotationTurns;

    public BuildingPlacementCursor(Factory factory, GameSession session)
    {
        _factory = factory;
        _session = session;
        Scaffolding = CreateScaffolding(0);
    }

    public Scaffolding Scaffolding { get; private set; }

    public Building TargetBuilding => Scaffolding.TargetBuilding;

    public Building GetPlacementCandidate(bool noCostPlacement)
    {
        return noCostPlacement ? TargetBuilding : Scaffolding;
    }

    public Building CreatePlacementCandidate(bool noCostPlacement)
    {
        return noCostPlacement
            ? CreateTargetBuilding(_displayRotationTurns)
            : CreateScaffolding(_displayRotationTurns);
    }

    public int GetDisplayRotationTurns() => _displayRotationTurns;

    public void RotateClockwise()
    {
        Scaffolding.RotateMap();
        _displayRotationTurns = (_displayRotationTurns + 1) % 4;
        ApplyDisplayRotation(Scaffolding, _displayRotationTurns);
    }

    public void RefreshAfterSuccessfulPlacement()
    {
        Scaffolding = CreateScaffolding(_displayRotationTurns);
    }

    private Scaffolding CreateScaffolding(int displayRotationTurns)
    {
        var scaffolding = new Scaffolding(_session, CreateTargetBuilding(0));
        for (var turn = 0; turn < displayRotationTurns; turn++)
        {
            scaffolding.RotateMap();
        }

        ApplyDisplayRotation(scaffolding, displayRotationTurns);
        return scaffolding;
    }

    private Building CreateTargetBuilding(int displayRotationTurns)
    {
        var building = _factory.Build(_session);
        for (var turn = 0; turn < displayRotationTurns; turn++)
        {
            building.RotateMap();
        }

        building.SetDisplayRotationTurns(displayRotationTurns);
        return building;
    }

    private static void ApplyDisplayRotation(Scaffolding scaffolding, int displayRotationTurns)
    {
        scaffolding.SetDisplayRotationTurns(displayRotationTurns);
        scaffolding.TargetBuilding.SetDisplayRotationTurns(displayRotationTurns);
    }
}
