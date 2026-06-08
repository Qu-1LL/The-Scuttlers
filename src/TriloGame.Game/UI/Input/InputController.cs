using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TriloGame.Game.UI.Input;

public sealed class InputController
{
    public KeyboardState CurrentKeyboard { get; private set; }

    public KeyboardState PreviousKeyboard { get; private set; }

    public MouseState CurrentMouse { get; private set; }

    public MouseState PreviousMouse { get; private set; }

    public Point DragStartPoint { get; private set; }

    public bool Dragging { get; private set; }

    public void BeginFrame()
    {
        PreviousKeyboard = CurrentKeyboard;
        PreviousMouse = CurrentMouse;
        CurrentKeyboard = Keyboard.GetState();
        CurrentMouse = Mouse.GetState();
    }

    public Point MousePoint => CurrentMouse.Position;

    public Point MouseDelta => CurrentMouse.Position - PreviousMouse.Position;

    public int WheelDelta => CurrentMouse.ScrollWheelValue - PreviousMouse.ScrollWheelValue;

    public bool LeftPressed => CurrentMouse.LeftButton == ButtonState.Pressed && PreviousMouse.LeftButton == ButtonState.Released;

    public bool LeftReleased => CurrentMouse.LeftButton == ButtonState.Released && PreviousMouse.LeftButton == ButtonState.Pressed;

    public bool LeftHeld => CurrentMouse.LeftButton == ButtonState.Pressed;

    public bool RightPressed => CurrentMouse.RightButton == ButtonState.Pressed && PreviousMouse.RightButton == ButtonState.Released;

    public bool RightReleased => CurrentMouse.RightButton == ButtonState.Released && PreviousMouse.RightButton == ButtonState.Pressed;

    public bool RightHeld => CurrentMouse.RightButton == ButtonState.Pressed;

    public bool KeyPressed(Keys key) => CurrentKeyboard.IsKeyDown(key) && PreviousKeyboard.IsKeyUp(key);

    public bool KeyReleased(Keys key) => CurrentKeyboard.IsKeyUp(key) && PreviousKeyboard.IsKeyDown(key);

    public bool KeyHeld(Keys key) => CurrentKeyboard.IsKeyDown(key);

    public void BeginDrag()
    {
        DragStartPoint = CurrentMouse.Position;
        Dragging = false;
    }

    public void UpdateDrag(float threshold, bool dragButtonHeld)
    {
        if (!dragButtonHeld)
        {
            return;
        }

        var delta = CurrentMouse.Position - DragStartPoint;
        Dragging = Dragging || delta.ToVector2().Length() > threshold;
    }

    public void EndDrag()
    {
        Dragging = false;
    }
}
