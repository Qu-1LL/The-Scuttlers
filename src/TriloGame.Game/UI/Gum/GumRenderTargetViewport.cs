using Gum.Wireframe;
using Gum;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TriloGame.Game.UI.Gum;

public sealed class GumRenderTargetViewport : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly GraphicalUiElement _mainRoot;
    private readonly GumUiRenderer _offscreenRenderer = new();
    private RenderTarget2D? _renderTarget;

    public GumRenderTargetViewport(GraphicsDevice graphicsDevice, GraphicalUiElement mainRoot)
    {
        _graphicsDevice = graphicsDevice;
        _mainRoot = mainRoot;
        _offscreenRenderer.Root.Visible = false;
    }

    public Texture2D Render(Rectangle bounds, Action<GumUiRenderer> drawContent)
    {
        ArgumentNullException.ThrowIfNull(drawContent);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "Viewport bounds must have a positive size.");
        }

        EnsureRenderTarget(bounds.Size);
        _offscreenRenderer.BeginFrame(bounds.Size);
        drawContent(_offscreenRenderer);
        _offscreenRenderer.EndFrame();

        var previousTargets = _graphicsDevice.GetRenderTargets();
        var previousViewport = _graphicsDevice.Viewport;
        var previousMainRootVisible = _mainRoot.Visible;
        var previousOffscreenRootVisible = _offscreenRenderer.Root.Visible;

        try
        {
            _mainRoot.Visible = false;
            _offscreenRenderer.Root.Visible = true;
            _graphicsDevice.SetRenderTarget(_renderTarget);
            _graphicsDevice.Clear(Color.Transparent);
            GumService.Default.Draw();
        }
        finally
        {
            _graphicsDevice.SetRenderTargets(previousTargets);
            _graphicsDevice.Viewport = previousViewport;
            _offscreenRenderer.Root.Visible = previousOffscreenRootVisible;
            _mainRoot.Visible = previousMainRootVisible;
        }

        return _renderTarget!;
    }

    public void Dispose()
    {
        _renderTarget?.Dispose();
        _renderTarget = null;
        _offscreenRenderer.Root.Visible = false;
    }

    private void EnsureRenderTarget(Point size)
    {
        if (_renderTarget is not null &&
            !_renderTarget.IsDisposed &&
            _renderTarget.Width == size.X &&
            _renderTarget.Height == size.Y)
        {
            return;
        }

        _renderTarget?.Dispose();
        _renderTarget = new RenderTarget2D(
            _graphicsDevice,
            size.X,
            size.Y,
            mipMap: false,
            SurfaceFormat.Color,
            DepthFormat.None,
            preferredMultiSampleCount: 0,
            RenderTargetUsage.DiscardContents);
    }
}
