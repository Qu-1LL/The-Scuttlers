namespace TriloGame.Game.Core.World;

public sealed class ConcentrationField
{
    private readonly double[] _values;

    public ConcentrationField(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        Width = width;
        Height = height;
        _values = new double[width * height];
    }

    public int Width { get; }

    public int Height { get; }

    public int CellCount => _values.Length;

    public double this[int x, int y]
    {
        get => _values[GetIndex(x, y)];
        set => _values[GetIndex(x, y)] = value;
    }

    private int GetIndex(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        return (y * Width) + x;
    }
}
