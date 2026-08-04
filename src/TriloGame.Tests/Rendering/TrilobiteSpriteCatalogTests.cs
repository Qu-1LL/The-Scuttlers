using Microsoft.Xna.Framework.Graphics;
using TriloGame.Game.Rendering;

namespace TriloGame.Tests.Rendering;

public sealed class TrilobiteSpriteCatalogTests
{
    [Theory]
    [InlineData("miner", "MinerTrilobite")]
    [InlineData("farmer", "FarmerTrilobite")]
    [InlineData("builder", "BuilderTrilobite")]
    [InlineData("fighter", "FighterTrilobite")]
    public void EachRoleAsksForItsOwnSprite(string assignment, string expectedKey)
    {
        Assert.Equal(expectedKey, TrilobiteSpriteCatalog.GetRoleTextureKey(assignment));
    }

    [Theory]
    [InlineData("unassigned")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("quartermaster")]
    public void UnassignedAndUnknownRolesUseTheDefaultSprite(string? assignment)
    {
        Assert.Equal(
            TrilobiteSpriteCatalog.DefaultTextureKey,
            TrilobiteSpriteCatalog.GetRoleTextureKey(assignment));
    }

    // A role whose art has not been drawn yet must fall back rather than resolve to a key nothing
    // registered - an unregistered key draws nothing at all, so the trilobite would vanish.
    [Fact]
    public void ResolveFallsBackToTheDefaultWhenARolesArtIsMissing()
    {
        var sprites = new SpriteFactory();
        sprites.Register(TrilobiteSpriteCatalog.DefaultTextureKey, (Texture2D)null!);

        Assert.Equal(
            TrilobiteSpriteCatalog.DefaultTextureKey,
            TrilobiteSpriteCatalog.ResolveTextureKey(sprites, "miner"));
    }

    [Fact]
    public void ResolveUsesTheRoleSpriteOnceItsArtIsRegistered()
    {
        var sprites = new SpriteFactory();
        sprites.Register(TrilobiteSpriteCatalog.DefaultTextureKey, (Texture2D)null!);
        sprites.Register("FarmerTrilobite", (Texture2D)null!);

        Assert.Equal("FarmerTrilobite", TrilobiteSpriteCatalog.ResolveTextureKey(sprites, "farmer"));
        // Every other role still has no art of its own and keeps the default.
        Assert.Equal(
            TrilobiteSpriteCatalog.DefaultTextureKey,
            TrilobiteSpriteCatalog.ResolveTextureKey(sprites, "builder"));
        Assert.Equal(
            TrilobiteSpriteCatalog.DefaultTextureKey,
            TrilobiteSpriteCatalog.ResolveTextureKey(sprites, "unassigned"));
    }

    // The load-time registration list and the per-role lookup have to agree, or a role's art would
    // ship in the content pipeline and never be loaded.
    [Fact]
    public void EveryRoleKeyTheGameRegistersIsOneARoleCanResolveTo()
    {
        var keys = TrilobiteSpriteCatalog.GetRoleTextureKeys();

        Assert.Equal(4, keys.Count);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.DoesNotContain(TrilobiteSpriteCatalog.DefaultTextureKey, keys);
        foreach (var assignment in new[] { "miner", "farmer", "builder", "fighter" })
        {
            Assert.Contains(TrilobiteSpriteCatalog.GetRoleTextureKey(assignment), keys);
        }
    }
}
