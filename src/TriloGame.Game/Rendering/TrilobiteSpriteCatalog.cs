using TriloGame.Game.Core.Entities;

namespace TriloGame.Game.Rendering;

// Which sprite a trilobite is drawn with, given the job it is doing.
//
// Every role has its own texture key whether or not art exists for it yet, and the key is derived
// from the role name rather than listed against it - so adding a role's art is an export plus a
// Content.mgcb entry, with nothing to change here. Until that art lands the role falls back to the
// unassigned sprite, which is why the resolve step needs the SpriteFactory: an unregistered key
// would otherwise draw nothing at all, and a trilobite that renders as empty space is far worse
// than one wearing the wrong hat.
public static class TrilobiteSpriteCatalog
{
    // The sprite every role falls back to, and the one an unassigned trilobite uses outright.
    public const string DefaultTextureKey = "Trilobite";

    // The key a role WOULD use if its art existed. Not necessarily registered - see ResolveTextureKey.
    public static string GetRoleTextureKey(string? assignment)
    {
        return assignment switch
        {
            TrilobiteRoles.Miner => "MinerTrilobite",
            TrilobiteRoles.Farmer => "FarmerTrilobite",
            TrilobiteRoles.Builder => "BuilderTrilobite",
            TrilobiteRoles.Fighter => "FighterTrilobite",
            _ => DefaultTextureKey
        };
    }

    public static string ResolveTextureKey(SpriteFactory sprites, string? assignment)
    {
        var roleKey = GetRoleTextureKey(assignment);
        return sprites.TryGet(roleKey, out _) ? roleKey : DefaultTextureKey;
    }

    public static string ResolveTextureKey(SpriteFactory sprites, Creature creature)
    {
        return ResolveTextureKey(sprites, creature.Assignment);
    }

    // Every role key the game will look for at load, so registration and resolution cannot drift
    // apart - a role missing from this list would silently never get its art loaded.
    public static IReadOnlyList<string> GetRoleTextureKeys()
    {
        return
        [
            GetRoleTextureKey(TrilobiteRoles.Miner),
            GetRoleTextureKey(TrilobiteRoles.Farmer),
            GetRoleTextureKey(TrilobiteRoles.Builder),
            GetRoleTextureKey(TrilobiteRoles.Fighter)
        ];
    }
}
