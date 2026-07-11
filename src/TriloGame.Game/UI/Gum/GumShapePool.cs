using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;

namespace TriloGame.Game.UI.Gum;

public sealed class GumShapePool
{
    private readonly List<RoundedRectangleRuntime> _roundedShapes = [];
    private int _roundedShapeCount;

    public ContainerRuntime Container { get; } = new();

    public void BeginFrame()
    {
        _roundedShapeCount = 0;
    }

    public void EndFrame()
    {
        for (var index = _roundedShapeCount; index < _roundedShapes.Count; index++)
        {
            _roundedShapes[index].Visible = false;
        }
    }

    public void AddRoundedRectangle(Rectangle bounds, Color color, int radius, bool isFilled = true, float strokeWidth = 1f)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var shape = GetRoundedShape(_roundedShapeCount++);
        shape.Visible = true;
        shape.X = bounds.X;
        shape.Y = bounds.Y;
        shape.Width = bounds.Width;
        shape.Height = bounds.Height;
        shape.Color = color;
        GumRoundedRectangleRuntimeShape.Apply(shape, radius, isFilled, strokeWidth);
    }

    private RoundedRectangleRuntime GetRoundedShape(int index)
    {
        while (_roundedShapes.Count <= index)
        {
            var shape = new RoundedRectangleRuntime
            {
                Visible = false
            };
            Container.Children.Add(shape);
            _roundedShapes.Add(shape);
        }

        return _roundedShapes[index];
    }
}
