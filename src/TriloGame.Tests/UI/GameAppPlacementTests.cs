using System.Reflection;
using TriloGame.Game;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Shared.Math;

namespace TriloGame.Tests.UI;

public sealed class GameAppPlacementTests
{
    [Fact]
    public void CreatePlacementScaffolding_PreservesRequestedRotationAcrossFreshBuilds()
    {
        var session = new GameSession();
        var factory = new Factory(game => new AlgaeFarm(game), session);
        var createPlacement = typeof(GameApp).GetMethod(
            "CreatePlacementScaffolding",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(createPlacement);

        var scaffolding = (Scaffolding)createPlacement!.Invoke(null, [session, factory, 1])!;

        Assert.Equal(1, scaffolding.GetDisplayRotationTurns());
        Assert.Equal(1, scaffolding.TargetBuilding.GetDisplayRotationTurns());
        Assert.Equal(new GridPoint(3, 2), scaffolding.Size);
        Assert.Equal(new GridPoint(3, 2), scaffolding.TargetBuilding.Size);
    }
}
