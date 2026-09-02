using TriloGame.Game.Shared.Math;

namespace TriloGame.Game.Core.Interaction;

public readonly record struct WorldRectangle(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public int Area => checked(Width * Height);

    public bool Contains(WorldPoint point)
    {
        return point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;
    }
}
