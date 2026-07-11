using Microsoft.Xna.Framework;

namespace TriloGame.Game.UI.Research;

internal sealed class ResearchTreeViewerController
{
    private readonly ResearchTreeViewportState _viewport = new();

    public Vector2 PanOffset => _viewport.PanOffset;

    public float Zoom => _viewport.Zoom;

    public void Reset()
    {
        _viewport.Reset();
    }

    public bool HandleWheel(
        Point point,
        int delta,
        Rectangle viewportBounds,
        ResearchTreeViewNode? root,
        ResearchTreeRenderConfig config)
    {
        if (root is null || !viewportBounds.Contains(point))
        {
            return false;
        }

        _viewport.ZoomAt(point, delta, viewportBounds, root, config);
        return true;
    }

    public bool HandlePanPointerDown(Point point, Rectangle viewportBounds, ResearchTreeViewNode? root)
    {
        if (root is null || !viewportBounds.Contains(point))
        {
            return false;
        }

        _viewport.BeginPan(point);
        return true;
    }

    public void HandlePanPointerDrag(Point point)
    {
        _viewport.DragPan(point);
    }

    public bool HandlePanPointerUp(
        Rectangle viewportBounds,
        ResearchTreeViewNode? root,
        ResearchTreeRenderConfig config)
    {
        if (root is null)
        {
            return false;
        }

        return _viewport.EndPan(viewportBounds, root, config);
    }
}
