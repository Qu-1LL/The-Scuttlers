using Microsoft.Xna.Framework;
using TriloGame.Game.UI.Research;

namespace TriloGame.Tests.UI;

public sealed class ResearchTreeCollisionDetectorTests
{
    [Fact]
    public void Detect_FlagsNodeNodeCollisions()
    {
        var result = ResearchTreeCollisionDetector.Detect(
            [new ResearchTreeCollisionNode(10, new Vector2(20f, 20f), 8f)],
            [],
            [new ResearchTreeCollisionNode(3, new Vector2(31f, 20f), 8f)],
            []);

        Assert.True(result.HasCollision);
        Assert.Contains(10, result.MovingNodeIds);
        Assert.Contains(3, result.FixedNodeIds);
    }

    [Fact]
    public void Detect_FlagsNodeLineCollisions()
    {
        var result = ResearchTreeCollisionDetector.Detect(
            [new ResearchTreeCollisionNode(4, new Vector2(50f, 54f), 5f)],
            [],
            [],
            [new ResearchTreeCollisionLine(7, new Vector2(20f, 50f), new Vector2(80f, 50f), 3f)]);

        Assert.True(result.HasCollision);
        Assert.Contains(4, result.MovingNodeIds);
        Assert.Contains(7, result.FixedLineIds);
    }

    [Fact]
    public void Detect_FlagsLineLineIntersections()
    {
        var result = ResearchTreeCollisionDetector.Detect(
            [],
            [new ResearchTreeCollisionLine(2, new Vector2(20f, 20f), new Vector2(80f, 80f), 3f)],
            [],
            [new ResearchTreeCollisionLine(9, new Vector2(80f, 20f), new Vector2(20f, 80f), 3f)]);

        Assert.True(result.HasCollision);
        Assert.Contains(2, result.MovingLineIds);
        Assert.Contains(9, result.FixedLineIds);
    }

    [Fact]
    public void Detect_IgnoresConfiguredFixedAnchorNodeForMovingLine()
    {
        var result = ResearchTreeCollisionDetector.Detect(
            [],
            [new ResearchTreeCollisionLine(2, new Vector2(20f, 20f), new Vector2(80f, 20f), 3f, IgnoredFixedNodeId: 5)],
            [new ResearchTreeCollisionNode(5, new Vector2(20f, 20f), 10f)],
            []);

        Assert.False(result.HasCollision);
    }

    [Fact]
    public void DetectHitboxes_IgnoresConnectorEndpoints()
    {
        var result = ResearchTreeCollisionDetector.DetectHitboxes(
            [
                ResearchTreeHitbox.Node(1, ResearchTreeHitboxOwner.Fixed, new Vector2(20f, 20f), 10f),
                ResearchTreeHitbox.Connector(
                    2,
                    ResearchTreeHitboxOwner.Moving,
                    new Vector2(20f, 20f),
                    new Vector2(80f, 20f),
                    3f,
                    new ResearchTreeHitboxEndpoint(ResearchTreeHitboxOwner.Fixed, 1),
                    ResearchTreeHitboxEndpoint.None)
            ],
            includeFixedFixedPairs: true,
            includeMovingMovingPairs: true);

        Assert.False(result.HasCollision);
    }

    [Fact]
    public void DetectHitboxes_CanEvaluateProjectedFixedFixedPairs()
    {
        var result = ResearchTreeCollisionDetector.DetectHitboxes(
            [
                ResearchTreeHitbox.Node(1, ResearchTreeHitboxOwner.Fixed, new Vector2(20f, 20f), 8f),
                ResearchTreeHitbox.Node(2, ResearchTreeHitboxOwner.Fixed, new Vector2(30f, 20f), 8f)
            ],
            includeFixedFixedPairs: true,
            includeMovingMovingPairs: false);

        Assert.True(result.HasCollision);
        Assert.Contains(1, result.FixedNodeIds);
        Assert.Contains(2, result.FixedNodeIds);
    }

    [Fact]
    public void DetectHitboxes_CanEvaluateMovingMovingPairs()
    {
        var result = ResearchTreeCollisionDetector.DetectHitboxes(
            [
                ResearchTreeHitbox.Node(1, ResearchTreeHitboxOwner.Moving, new Vector2(20f, 20f), 8f),
                ResearchTreeHitbox.Node(2, ResearchTreeHitboxOwner.Moving, new Vector2(30f, 20f), 8f)
            ],
            includeFixedFixedPairs: false,
            includeMovingMovingPairs: true);

        Assert.True(result.HasCollision);
        Assert.Contains(1, result.MovingNodeIds);
        Assert.Contains(2, result.MovingNodeIds);
    }
}
