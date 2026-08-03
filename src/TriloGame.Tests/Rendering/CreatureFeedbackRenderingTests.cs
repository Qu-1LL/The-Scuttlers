using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Combat;
using TriloGame.Game.Core.Economy;
using TriloGame.Game.Core.Entities;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Rendering;
using TriloGame.Game.Shared.Math;
using TriloGame.Game.Shared.State;

namespace TriloGame.Tests.Rendering;

public sealed class CreatureFeedbackRenderingTests
{
    [Fact]
    public void CombatDebugShapes_UseRequestedTranslucentColors()
    {
        var mining = WorldSceneRenderer.GetMiningStrikeColor();
        var combat = WorldSceneRenderer.GetCombatHitboxColor();

        Assert.Equal(new Color(255, 0, 255, 76), mining);
        Assert.Equal(new Color(255, 32, 32, 76), combat);
    }

    [Fact]
    public void DamageFlash_TintsCreatureRedAndReturnsToWhite()
    {
        Assert.Equal(Color.White, WorldSceneRenderer.GetCreatureDamageColor(0f));
        Assert.Equal(Color.Red, WorldSceneRenderer.GetCreatureDamageColor(1f));
    }

    [Fact]
    public void DamageFlash_UsesBoostedOpacityDuringFalloff()
    {
        var runtime = new GameSessionRuntimeState();
        runtime.RestartDamageFlash(creatureId: 7);
        runtime.AdvancePresentation(GameSessionRuntimeState.DamageFlashDurationMs / 2d);

        Assert.Equal(0.675f, runtime.GetDamageFlashAlpha(7), precision: 3);
    }

    [Fact]
    public void InventoryBackpack_UsesCarriedItemTextureKey()
    {
        var session = new GameSession();
        var trilobite = new Trilobite("Carrier", GridPoint.Zero, session);

        Assert.Null(WorldSceneRenderer.GetInventoryBackpackTextureKey(trilobite));

        trilobite.AddToInventory(ResourceName.Malachite, 1);

        Assert.Equal(OreType.MALACHITE.Name, WorldSceneRenderer.GetInventoryBackpackTextureKey(trilobite));
    }

    [Fact]
    public void InventoryBackpack_UsesTextureKeyForEachCarriedResourceSlot()
    {
        var session = new GameSession();
        var trilobite = new Trilobite("Carrier", GridPoint.Zero, session);
        trilobite.AddToInventory(ResourceName.Magnetite, 2);
        trilobite.AddToInventory(ResourceName.Sandstone, 3);

        Assert.Equal(OreType.MAGNETITE.Name, WorldSceneRenderer.GetInventoryBackpackTextureKey(trilobite, 0));
        Assert.Equal(OreType.MAGNETITE.Name, WorldSceneRenderer.GetInventoryBackpackTextureKey(trilobite, 1));
        Assert.Equal(OreType.SANDSTONE.Name, WorldSceneRenderer.GetInventoryBackpackTextureKey(trilobite, 2));
        Assert.Equal(OreType.SANDSTONE.Name, WorldSceneRenderer.GetInventoryBackpackTextureKey(trilobite, 3));
        Assert.Equal(OreType.SANDSTONE.Name, WorldSceneRenderer.GetInventoryBackpackTextureKey(trilobite, 4));
        Assert.Null(WorldSceneRenderer.GetInventoryBackpackTextureKey(trilobite, 5));
    }

    [Fact]
    public void InventoryBackpack_PositionStaysCenteredOnCreature()
    {
        var center = new Vector2(500f, 500f);

        var position = WorldSceneRenderer.GetInventoryBackpackWorldPosition(center, 0f);

        Assert.Equal(center, position);
    }

    [Fact]
    public void InventoryBackpack_FiveSlotsArePlacedTopToBottomInCreatureLocalSpace()
    {
        var center = new Vector2(500f, 500f);
        const float facingRadians = 0f;
        var backpackCenter = WorldSceneRenderer.GetInventoryBackpackWorldPosition(center, facingRadians);

        var first = WorldSceneRenderer.GetInventoryBackpackSlotWorldPosition(center, facingRadians, 0, 5);
        var middle = WorldSceneRenderer.GetInventoryBackpackSlotWorldPosition(center, facingRadians, 2, 5);
        var last = WorldSceneRenderer.GetInventoryBackpackSlotWorldPosition(center, facingRadians, 4, 5);

        Assert.Equal(backpackCenter, middle);
        Assert.Equal(first.X, middle.X, precision: 3);
        Assert.Equal(middle.X, last.X, precision: 3);
        Assert.True(first.Y < middle.Y);
        Assert.True(last.Y > middle.Y);
        Assert.Equal(middle.Y - first.Y, last.Y - middle.Y, precision: 3);
    }

    [Fact]
    public void InventoryBackpack_TopToBottomColumnRotatesWithTrilobite()
    {
        var center = new Vector2(500f, 500f);
        const float facingRadians = MathF.PI / 2f;
        var backpackCenter = WorldSceneRenderer.GetInventoryBackpackWorldPosition(center, facingRadians);

        var first = WorldSceneRenderer.GetInventoryBackpackSlotWorldPosition(center, facingRadians, 0, 5);
        var middle = WorldSceneRenderer.GetInventoryBackpackSlotWorldPosition(center, facingRadians, 2, 5);
        var last = WorldSceneRenderer.GetInventoryBackpackSlotWorldPosition(center, facingRadians, 4, 5);

        Assert.Equal(backpackCenter, middle);
        Assert.Equal(first.Y, middle.Y, precision: 3);
        Assert.Equal(middle.Y, last.Y, precision: 3);
        Assert.True(first.X > middle.X);
        Assert.True(last.X < middle.X);
        Assert.Equal(first.X - middle.X, middle.X - last.X, precision: 3);
    }

    [Fact]
    public void InventoryBackpack_IconsUseCreatureFacingRotation()
    {
        const float facingRadians = MathF.PI * 0.75f;

        Assert.Equal(facingRadians, WorldSceneRenderer.GetInventoryBackpackIconRotationRadians(facingRadians));
    }
}
