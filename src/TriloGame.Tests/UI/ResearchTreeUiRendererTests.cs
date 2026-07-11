using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Runtime.Bootstrap;
using TriloGame.Game.UI.Gum;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class ResearchTreeUiRendererTests
{
    [Fact]
    public void CalculateCardTreeLayout_FitsTheWholeCuratedTreeInsideThePreviewBounds()
    {
        var featureTree = Assert.IsType<FeatureTree>(TriloDex.Global.FindFeatureTree("B2"));
        var bounds = new Rectangle(20, 30, 240, 150);

        var layout = ResearchTreeUiRenderer.CalculateCardTreeLayout(
            ResearchTreeViewNode.FromFeatureTree(featureTree),
            bounds,
            ResearchTreeUiRenderer.TreeEntryCardConfig);

        Assert.NotEmpty(layout.Nodes);
        foreach (var node in layout.Nodes)
        {
            Assert.InRange(node.Position.X - layout.Radius, bounds.Left, bounds.Right);
            Assert.InRange(node.Position.X + layout.Radius, bounds.Left, bounds.Right);
            Assert.InRange(node.Position.Y - layout.Radius, bounds.Top, bounds.Bottom);
            Assert.InRange(node.Position.Y + layout.Radius, bounds.Top, bounds.Bottom);
        }
    }

    [Fact]
    public void CalculateCardTreeLayout_SupportsDraftBranchesThroughTheSameTreeEntryPath()
    {
        var branch = new ResearchBranch();
        var root = branch.SetRoot(new TreeInstanceNode(new SkillNode("Root", "Root"), "B1"));
        branch.AddChild(root, new TreeInstanceNode(new SkillNode("Child", "Child"), "B1"));
        var bounds = new Rectangle(0, 0, 180, 120);

        var layout = ResearchTreeUiRenderer.CalculateCardTreeLayout(
            ResearchTreeViewNode.FromResearchBranch(branch),
            bounds,
            ResearchTreeUiRenderer.TreeEntryCardConfig);

        Assert.Equal(2, layout.Nodes.Count);
        Assert.All(layout.Nodes, node => Assert.True(bounds.Contains(node.Position)));
    }

    [Fact]
    public void ClampZoom_AllowsArbitrarilySmallPositiveValuesAndCapsZoomIn()
    {
        Assert.Equal(0.000001f, ResearchTreeUiRenderer.ClampZoom(0.000001f));
        Assert.Equal(1.2f, ResearchTreeUiRenderer.ClampZoom(1.2f));
        Assert.Equal(2.25f, ResearchTreeUiRenderer.ClampZoom(9f));
    }

    [Fact]
    public void CalculateDetailMetrics_UsesTheWholeViewportAsContentBounds()
    {
        var featureTree = Assert.IsType<FeatureTree>(TriloDex.Global.FindFeatureTree("B2"));
        var bounds = new Rectangle(24, 36, 640, 360);

        var metrics = ResearchTreeUiRenderer.CalculateDetailMetrics(
            bounds,
            ResearchTreeViewNode.FromFeatureTree(featureTree),
            zoom: 1f,
            ResearchTreeUiRenderer.ReadOnlyDetailConfig);

        Assert.Equal(bounds, metrics.ContentBounds);
    }

    [Fact]
    public void CalculateDetailMetrics_ScalesNodeRadiusAcrossZoomLevels()
    {
        var featureTree = Assert.IsType<FeatureTree>(TriloDex.Global.FindFeatureTree("B2"));
        var root = ResearchTreeViewNode.FromFeatureTree(featureTree);
        var bounds = new Rectangle(24, 36, 640, 360);

        var zoomedOut = ResearchTreeUiRenderer.CalculateDetailMetrics(
            bounds,
            root,
            zoom: 0.05f,
            ResearchTreeUiRenderer.ReadOnlyDetailConfig);
        var normal = ResearchTreeUiRenderer.CalculateDetailMetrics(
            bounds,
            root,
            zoom: 1f,
            ResearchTreeUiRenderer.ReadOnlyDetailConfig);
        var zoomedIn = ResearchTreeUiRenderer.CalculateDetailMetrics(
            bounds,
            root,
            zoom: 2.25f,
            ResearchTreeUiRenderer.ReadOnlyDetailConfig);

        Assert.True(zoomedOut.NodeRadius < normal.NodeRadius);
        Assert.True(normal.NodeRadius < zoomedIn.NodeRadius);
        Assert.True(zoomedOut.EdgeLength < normal.EdgeLength);
        Assert.True(zoomedIn.EdgeLength > normal.EdgeLength);
    }

    [Fact]
    public void DrawDetail_ScalesRenderedNodeBoundsAcrossZoomLevels()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var root = ResearchTreeViewNode.FromSkillTree(session.SkillTree);
        var bounds = new Rectangle(24, 36, 640, 360);

        var zoomedOutSizes = DrawDetailNodeShapeSizes(session, root, bounds, zoom: 0.05f);
        var zoomedInSizes = DrawDetailNodeShapeSizes(session, root, bounds, zoom: 2.25f);

        var zoomedOutRadius = ResearchTreeUiRenderer.CalculateDetailNodeRadius(0.05f);
        var zoomedInRadius = ResearchTreeUiRenderer.CalculateDetailNodeRadius(2.25f);
        Assert.Contains(
            (zoomedOutRadius * 2, zoomedOutRadius * 2),
            zoomedOutSizes);
        Assert.Contains(
            (zoomedInRadius * 2, zoomedInRadius * 2),
            zoomedInSizes);
        Assert.True(zoomedOutSizes.Max(size => size.Width) < zoomedInSizes.Max(size => size.Width));
    }

    [Fact]
    public void DrawDetail_KeepsRenderedConnectorThicknessFixedAcrossZoomLevels()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var featureTree = Assert.IsType<FeatureTree>(TriloDex.Global.FindFeatureTree("B2"));
        var root = ResearchTreeViewNode.FromFeatureTree(featureTree);
        var bounds = new Rectangle(24, 36, 640, 360);

        var zoomedOutThicknesses = DrawDetailConnectorThicknesses(session, root, bounds, zoom: 0.5f);
        var zoomedInThicknesses = DrawDetailConnectorThicknesses(session, root, bounds, zoom: 2.25f);

        Assert.NotEmpty(zoomedOutThicknesses);
        Assert.Equal(zoomedOutThicknesses.Distinct().OrderBy(thickness => thickness), zoomedInThicknesses.Distinct().OrderBy(thickness => thickness));
        Assert.All(
            zoomedOutThicknesses,
            thickness => Assert.Equal(ResearchTreeUiRenderer.DetailConnectorThickness, thickness));
    }

    [Fact]
    public void DrawDetail_UsesDistinctLockedAvailableAndUnlockedConnectorStyles()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var child = session.SkillTree.AddChild(
            session.SkillTree.Root!,
            session.SkillTree.IntakeSkillNode(new SkillNode("Child", "Child."), "B1"));
        var grandchild = session.SkillTree.AddChild(
            child,
            session.SkillTree.IntakeSkillNode(new SkillNode("Grandchild", "Grandchild."), "B1"));
        var root = ResearchTreeViewNode.FromSkillTree(session.SkillTree);
        var connectorColors = DrawDetailConnectorColors(session, root);
        var availableConnector = ResearchTreeUiRenderer.GetConnectorColor(root.Children[0]);
        var lockedConnector = ResearchTreeUiRenderer.GetConnectorColor(root.Children[0].Children[0]);

        Assert.Contains(availableConnector, connectorColors);
        Assert.Contains(lockedConnector, connectorColors);
        Assert.Equal(210, availableConnector.A);
        Assert.Equal(64, lockedConnector.A);
        Assert.True(ResearchTreeUiRenderer.ShouldDrawLockedMarker(root.Children[0].Children[0]));
        Assert.False(ResearchTreeUiRenderer.ShouldDrawLockedMarker(root.Children[0]));
        Assert.True(ResearchTreeUiRenderer.ShouldDrawAvailableAdornment(root.Children[0]));
        Assert.False(ResearchTreeUiRenderer.ShouldDrawAvailableAdornment(root.Children[0].Children[0]));

        Assert.True(child.TryUnlock(session));
        Assert.True(grandchild.CanUnlock());
        root = ResearchTreeViewNode.FromSkillTree(session.SkillTree);
        var unlockedConnector = ResearchTreeUiRenderer.GetConnectorColor(root.Children[0]);

        Assert.Equal(new Color(247, 221, 92), unlockedConnector);
    }

    [Fact]
    public void SkillTreeNodeStyles_MakeLockedNodesDarkerThanAvailableAndUnlockedNodes()
    {
        var session = new GameSessionBootstrapper().CreateNewGame().Session;
        var child = session.SkillTree.AddChild(
            session.SkillTree.Root!,
            session.SkillTree.IntakeSkillNode(new SkillNode("Child", "Child."), "B1"));
        session.SkillTree.AddChild(
            child,
            session.SkillTree.IntakeSkillNode(new SkillNode("Grandchild", "Grandchild."), "B1"));
        var root = ResearchTreeViewNode.FromSkillTree(session.SkillTree);
        var available = root.Children[0];
        var locked = available.Children[0];

        var availableFill = ResearchTreeUiRenderer.GetNodeFillColor(session, available);
        var lockedFill = ResearchTreeUiRenderer.GetNodeFillColor(session, locked);

        Assert.True(lockedFill.R + lockedFill.G + lockedFill.B < availableFill.R + availableFill.G + availableFill.B);
        Assert.NotEqual(
            ResearchTreeUiRenderer.GetNodeBorderColor(session, locked),
            ResearchTreeUiRenderer.GetNodeBorderColor(session, available));
        Assert.True(ResearchTreeUiRenderer.ShouldDrawLockedMarker(locked));
        Assert.True(ResearchTreeUiRenderer.ShouldDrawAvailableAdornment(available));
    }

    [Fact]
    public void AvailableNodeAdornment_DrawsXMarkerAndUsesVisualOnlyShake()
    {
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(new Point(320, 240));
        var center = new Vector2(120f, 100f);

        ResearchTreeUiRenderer.DrawAvailableNodeMarker(gumUi, center, radius: 17);
        var firstOffset = ResearchTreeUiRenderer.CalculateAvailableNodeShakeOffset("B1:Available", 17, 0d);
        var secondOffset = ResearchTreeUiRenderer.CalculateAvailableNodeShakeOffset("B1:Available", 17, 100d);

        Assert.Equal(
            2,
            gumUi.Root.Children
                .OfType<ColoredRectangleRuntime>()
                .Count(shape => shape.Color.A > 0));
        Assert.NotEqual(firstOffset, secondOffset);
        Assert.InRange(firstOffset.Length(), 0f, 2f);
        Assert.InRange(secondOffset.Length(), 0f, 2f);
    }

    [Fact]
    public void HoverAndSelectionHalos_UseSinglePulsingAndDoubleStableRings()
    {
        var hoverUi = new GumUiRenderer(addToManagers: false);
        hoverUi.BeginFrame(new Point(320, 240));
        ResearchTreeUiRenderer.DrawHoveredNodeHalo(hoverUi, new Vector2(120f, 100f), 17, visualTimeMs: 300d);

        var selectedUi = new GumUiRenderer(addToManagers: false);
        selectedUi.BeginFrame(new Point(320, 240));
        ResearchTreeUiRenderer.DrawSelectedNodeHalo(selectedUi, new Vector2(120f, 100f), 17, visualTimeMs: 300d);

        Assert.Single(hoverUi.Root.Children.OfType<RoundedRectangleRuntime>(), shape => !shape.IsFilled);
        Assert.Equal(2, selectedUi.Root.Children.OfType<RoundedRectangleRuntime>().Count(shape => !shape.IsFilled));
    }

    [Fact]
    public void SelectedNodeHalo_PulsesBothCyanRings()
    {
        var firstUi = new GumUiRenderer(addToManagers: false);
        firstUi.BeginFrame(new Point(320, 240));
        ResearchTreeUiRenderer.DrawSelectedNodeHalo(firstUi, new Vector2(120f, 100f), 17, visualTimeMs: 0d);

        var secondUi = new GumUiRenderer(addToManagers: false);
        secondUi.BeginFrame(new Point(320, 240));
        ResearchTreeUiRenderer.DrawSelectedNodeHalo(secondUi, new Vector2(120f, 100f), 17, visualTimeMs: 300d);

        var firstRings = firstUi.Root.Children.OfType<RoundedRectangleRuntime>().Where(shape => !shape.IsFilled).ToArray();
        var secondRings = secondUi.Root.Children.OfType<RoundedRectangleRuntime>().Where(shape => !shape.IsFilled).ToArray();
        Assert.Equal(2, firstRings.Length);
        Assert.Equal(2, secondRings.Length);
        Assert.NotEqual(firstRings[0].Width, secondRings[0].Width);
        Assert.NotEqual(firstRings[0].Color, secondRings[0].Color);
    }

    [Fact]
    public void CalculateBackgroundTileSize_UsesUnboundedZoomOutScale()
    {
        var belowMinimum = ResearchTreeUiRenderer.CalculateBackgroundTileSize(96, 64, zoom: 0.1f);
        var normal = ResearchTreeUiRenderer.CalculateBackgroundTileSize(96, 64, zoom: 1f);
        var zoomedIn = ResearchTreeUiRenderer.CalculateBackgroundTileSize(96, 64, zoom: 2.25f);
        var aboveMaximum = ResearchTreeUiRenderer.CalculateBackgroundTileSize(96, 64, zoom: 9f);

        Assert.Equal(new Point(10, 6), belowMinimum);
        Assert.Equal(new Point(96, 64), normal);
        Assert.Equal(new Point(216, 144), zoomedIn);
        Assert.Equal(zoomedIn, aboveMaximum);
    }

    [Fact]
    public void CalculateBackgroundStartCoordinate_AnchorsTilesToTheTreeSurfaceOrigin()
    {
        const int viewportLeft = 100;
        const float treeSurfaceOrigin = 421f;

        var normalZoomStart = ResearchTreeUiRenderer.CalculateBackgroundStartCoordinate(viewportLeft, treeSurfaceOrigin, tileLength: 64);
        var zoomedInStart = ResearchTreeUiRenderer.CalculateBackgroundStartCoordinate(viewportLeft, treeSurfaceOrigin, tileLength: 96);

        Assert.True(normalZoomStart <= viewportLeft);
        Assert.True(zoomedInStart <= viewportLeft);
        Assert.True(viewportLeft - normalZoomStart < 64);
        Assert.True(viewportLeft - zoomedInStart < 96);
        Assert.Equal(0, ((int)treeSurfaceOrigin - normalZoomStart) % 64);
        Assert.Equal(0, ((int)treeSurfaceOrigin - zoomedInStart) % 96);
    }

    [Fact]
    public void TreeEntryCardConfig_ExposesDataDrivenFeatureFlags()
    {
        var config = ResearchTreeUiRenderer.TreeEntryCardConfig;

        Assert.False(config.ShowBackButton);
        Assert.True(config.ShowRootNode);
        Assert.True(config.EnableNodeSelection);
        Assert.False(config.EnableBranchDrafting);
        Assert.False(config.EnablePlacementPreview);
    }

    private static IReadOnlyList<(int Width, int Height)> DrawDetailNodeShapeSizes(
        GameSession session,
        ResearchTreeViewNode root,
        Rectangle bounds,
        float zoom)
    {
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(new Point(800, 480));

        ResearchTreeUiRenderer.DrawDetail(
            gumUi,
            session,
            bounds,
            root,
            Vector2.Zero,
            zoom,
            backgroundTexture: null,
            pointerPoint: Point.Zero,
            ResearchTreeUiRenderer.ReadOnlyDetailConfig);

        var nodeRadius = ResearchTreeUiRenderer.CalculateDetailNodeRadius(zoom);
        var maxNodeDiameter = (nodeRadius + ResearchTreeUiRenderer.CalculateDetailNodeBorderThickness(nodeRadius)) * 2;
        return gumUi.Root.Children
            .OfType<RoundedRectangleRuntime>()
            .Where(shape => shape.IsFilled && shape.Width <= maxNodeDiameter && shape.Height <= maxNodeDiameter)
            .Select(shape => (Width: (int)shape.Width, Height: (int)shape.Height))
            .OrderBy(size => size.Width)
            .ThenBy(size => size.Height)
            .ToList();
    }

    private static IReadOnlyList<int> DrawDetailConnectorThicknesses(
        GameSession session,
        ResearchTreeViewNode root,
        Rectangle bounds,
        float zoom)
    {
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(new Point(800, 480));

        ResearchTreeUiRenderer.DrawDetail(
            gumUi,
            session,
            bounds,
            root,
            Vector2.Zero,
            zoom,
            backgroundTexture: null,
            pointerPoint: Point.Zero,
            ResearchTreeUiRenderer.ReadOnlyDetailConfig);

        return gumUi.Root.Children
            .OfType<ColoredRectangleRuntime>()
            .Where(shape => shape.Width > ResearchTreeUiRenderer.DetailConnectorThickness)
            .Select(shape => (int)shape.Height)
            .OrderBy(thickness => thickness)
            .ToList();
    }

    private static IReadOnlyList<Color> DrawDetailConnectorColors(
        GameSession session,
        ResearchTreeViewNode root)
    {
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(new Point(800, 480));

        ResearchTreeUiRenderer.DrawDetail(
            gumUi,
            session,
            new Rectangle(24, 36, 640, 360),
            root,
            Vector2.Zero,
            zoom: 1f,
            backgroundTexture: null,
            pointerPoint: Point.Zero,
            ResearchTreeUiRenderer.ReadOnlyDetailConfig);

        return gumUi.Root.Children
            .OfType<ColoredRectangleRuntime>()
            .Where(shape => shape.Height == ResearchTreeUiRenderer.DetailConnectorThickness &&
                shape.Width > ResearchTreeUiRenderer.DetailConnectorThickness)
            .Select(shape => shape.Color)
            .ToList();
    }
}
