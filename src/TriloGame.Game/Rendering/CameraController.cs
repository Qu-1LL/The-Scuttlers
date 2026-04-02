using Microsoft.Xna.Framework;

namespace TriloGame.Game.Rendering;

public sealed class CameraController
{
    public float CurrentScale { get; set; } = 1f;

    public Vector2 CameraOrigin { get; private set; }

    public Vector2 ViewCenter { get; private set; }

    public void SetViewport(int width, int height)
    {
        ViewCenter = new Vector2(width / 2f, height / 2f);
    }

    public void SetOrigin(Vector2 origin)
    {
        CameraOrigin = origin;
    }

    public void HandleViewportResize(int oldWidth, int oldHeight, int newWidth, int newHeight)
    {
        var oldCenter = new Vector2(oldWidth / 2f, oldHeight / 2f);
        var newCenter = new Vector2(newWidth / 2f, newHeight / 2f);
        CameraOrigin += (oldCenter - newCenter) * (1f / CurrentScale);
        ViewCenter = newCenter;
    }

    public void PanByScreenDelta(float dx, float dy)
    {
        CameraOrigin -= new Vector2(dx, dy) * (1f / CurrentScale);
    }

    public Vector2 WorldToScreen(Vector2 world)
    {
        return ViewCenter + ((world - CameraOrigin) * CurrentScale);
    }

    public Vector2 ScreenToWorld(Point screen)
    {
        return CameraOrigin + ((screen.ToVector2() - ViewCenter) * (1f / CurrentScale));
    }
}
