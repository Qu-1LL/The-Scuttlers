namespace TriloGame.Game.Core.Buildings;

public sealed record WallType(
    string Name,
    int Health,
    string NoConnectionSprite,
    string OneConnectionSprite,
    string TwoConnectionsStraightSprite,
    string TwoConnectionsBendSprite,
    string ThreeConnectionsSprite,
    string FourConnectionsSprite)
{
    private const int TopBit = 1;
    private const int RightBit = 2;
    private const int BottomBit = 4;
    private const int LeftBit = 8;

    public static WallType Default { get; } = new(
        "Wall",
        15,
        "wall_0",
        "wall_1",
        "wall_2",
        "wall_2_bent",
        "wall_3",
        "wall_4");

    public (string TextureKey, int RotationTurns) ResolveAppearance(int connectionMask)
    {
        return connectionMask switch
        {
            0 => (NoConnectionSprite, 0),
            BottomBit => (OneConnectionSprite, 0),
            LeftBit => (OneConnectionSprite, 1),
            TopBit => (OneConnectionSprite, 2),
            RightBit => (OneConnectionSprite, 3),
            TopBit | BottomBit => (TwoConnectionsStraightSprite, 0),
            LeftBit | RightBit => (TwoConnectionsStraightSprite, 1),
            BottomBit | LeftBit => (TwoConnectionsBendSprite, 0),
            LeftBit | TopBit => (TwoConnectionsBendSprite, 1),
            TopBit | RightBit => (TwoConnectionsBendSprite, 2),
            RightBit | BottomBit => (TwoConnectionsBendSprite, 3),
            BottomBit | LeftBit | RightBit => (ThreeConnectionsSprite, 0),
            LeftBit | TopBit | BottomBit => (ThreeConnectionsSprite, 1),
            TopBit | LeftBit | RightBit => (ThreeConnectionsSprite, 2),
            TopBit | RightBit | BottomBit => (ThreeConnectionsSprite, 3),
            TopBit | RightBit | BottomBit | LeftBit => (FourConnectionsSprite, 0),
            _ => (NoConnectionSprite, 0)
        };
    }
}
