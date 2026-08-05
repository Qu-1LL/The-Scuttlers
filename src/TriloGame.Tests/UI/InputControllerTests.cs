using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TriloGame.Game.UI.Input;

namespace TriloGame.Tests.UI;

public sealed class InputControllerTests
{
    [Fact]
    public void BeginFrame_UsesMappedPointerCoordinatesForMovementAndDrag()
    {
        var input = new InputController();

        input.BeginFrame(new KeyboardState(), new MouseState(), new Point(120, 90));
        input.BeginDrag();
        input.BeginFrame(new KeyboardState(), new MouseState(), new Point(145, 110));
        input.UpdateDrag(threshold: 10f, dragButtonHeld: true);

        Assert.Equal(new Point(145, 110), input.MousePoint);
        Assert.Equal(new Point(25, 20), input.MouseDelta);
        Assert.Equal(new Point(120, 90), input.DragStartPoint);
        Assert.True(input.Dragging);
    }
}
