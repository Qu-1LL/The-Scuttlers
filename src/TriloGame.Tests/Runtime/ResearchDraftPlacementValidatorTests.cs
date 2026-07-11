using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Progression;
using TriloGame.Game.Runtime.Systems;

namespace TriloGame.Tests.Runtime;

public sealed class ResearchDraftPlacementValidatorTests
{
    [Fact]
    public void BoundaryProfile_CreatesMirroredBezierSegments()
    {
        var profile = ResearchDraftBoundaryProfile.Default;
        var segments = profile.CreateSegments();

        Assert.Equal(48, segments.Count);
        Assert.Equal(profile.SegmentCount, segments.Count);
        Assert.True(profile.StartX > 0.5f);
        Assert.True(profile.SecondControlX > 5f);
        Assert.True(profile.EndX > 9f);

        for (var index = 0; index < profile.SamplesPerSide; index++)
        {
            var left = segments[index];
            var right = segments[index + profile.SamplesPerSide];

            Assert.Equal(-right.Start.X, left.Start.X, precision: 4);
            Assert.Equal(-right.End.X, left.End.X, precision: 4);
            Assert.Equal(right.Start.Y, left.Start.Y, precision: 4);
            Assert.Equal(right.End.Y, left.End.Y, precision: 4);
        }
    }

    [Fact]
    public void Validate_AllowsACompactBranchNearTheRoot()
    {
        var skillTree = CreateRootOnlySkillTree();
        var branch = new ResearchBranch();
        var branchRoot = branch.SetRoot(new TreeInstanceNode(new SkillNode("Branch Root", "Branch root."), "B1"));
        branch.AddChild(branchRoot, new TreeInstanceNode(new SkillNode("Branch Child", "Branch child."), "B1"));

        var validation = ResearchDraftPlacementValidator.Validate(skillTree, branch, skillTree.Root!);

        Assert.True(validation.CanPlace, Describe(validation));
        Assert.True(validation.IsStructurallyValid);
        Assert.False(validation.Collision.HasCollision);
        Assert.Null(validation.FailureReason);
    }

    [Fact]
    public void Validate_RejectsDraftedNodesTouchingExistingTreeNodes()
    {
        var skillTree = CreateDeepCrowdedSkillTree(out var anchor);
        var branch = new ResearchBranch();
        branch.SetRoot(new TreeInstanceNode(new SkillNode("Crowded Draft", "Draft root."), "Draft"));

        var validation = ResearchDraftPlacementValidator.Validate(skillTree, branch, anchor);

        Assert.False(validation.CanPlace);
        Assert.True(validation.IsStructurallyValid, Describe(validation));
        Assert.False(validation.Collision.HasBoundaryCollision, Describe(validation));
        Assert.NotEmpty(validation.Collision.MovingNodeIds);
        Assert.NotEmpty(validation.Collision.FixedNodeIds);
        Assert.Equal(ResearchDraftPlacementValidator.TreeCollisionFailureReason, validation.FailureReason);
    }

    [Fact]
    public void Validate_RejectsDraftedNodesTouchingTheBoundary()
    {
        var skillTree = CreateRootOnlySkillTree();
        var branch = CreateWideBoundaryCollisionBranch();

        var validation = ResearchDraftPlacementValidator.Validate(skillTree, branch, skillTree.Root!);

        Assert.False(validation.CanPlace);
        Assert.True(validation.IsStructurallyValid, Describe(validation));
        Assert.True(validation.Collision.HasBoundaryCollision, Describe(validation));
        Assert.NotEmpty(validation.Collision.MovingNodeIds);
        Assert.Equal(ResearchDraftPlacementValidator.BoundaryCollisionFailureReason, validation.FailureReason);
    }

    [Fact]
    public void Validate_RejectsDraftedConnectorsCrossingTheBoundary()
    {
        var skillTree = CreateRootOnlySkillTree();
        var branch = CreateWideBoundaryCollisionBranch();

        var validation = ResearchDraftPlacementValidator.Validate(skillTree, branch, skillTree.Root!);

        Assert.False(validation.CanPlace);
        Assert.True(validation.Collision.HasBoundaryCollision, Describe(validation));
        Assert.NotEmpty(validation.Collision.MovingLineIds);
        Assert.NotEmpty(validation.Collision.BoundaryLineIds);
    }

    [Fact]
    public void DetectHitboxes_RejectsCollisionsWithinDraftedBranch()
    {
        var collision = ResearchDraftPlacementValidator.DetectHitboxes(
            [
                ResearchDraftPlacementHitbox.Node(
                    1,
                    ResearchDraftPlacementHitboxOwner.Moving,
                    new Vector2(0f, 0f),
                    0.2f),
                ResearchDraftPlacementHitbox.Node(
                    2,
                    ResearchDraftPlacementHitboxOwner.Moving,
                    new Vector2(0.3f, 0f),
                    0.2f)
            ],
            padding: 0f);

        Assert.True(collision.HasCollision);
        Assert.Contains(1, collision.MovingNodeIds);
        Assert.Contains(2, collision.MovingNodeIds);
    }

    [Fact]
    public void DetectHitboxes_RejectsCollisionsIntroducedByProjectedTreeReflow()
    {
        var collision = ResearchDraftPlacementValidator.DetectHitboxes(
            [
                ResearchDraftPlacementHitbox.Node(
                    1,
                    ResearchDraftPlacementHitboxOwner.Fixed,
                    new Vector2(0f, 0f),
                    0.2f),
                ResearchDraftPlacementHitbox.Node(
                    2,
                    ResearchDraftPlacementHitboxOwner.Fixed,
                    new Vector2(0.3f, 0f),
                    0.2f)
            ],
            padding: 0f);

        Assert.True(collision.HasCollision);
        Assert.Contains(1, collision.FixedNodeIds);
        Assert.Contains(2, collision.FixedNodeIds);
    }

    [Fact]
    public void DetectHitboxes_RejectsConnectorsThatCrossAwayFromSharedEndpoint()
    {
        var sharedEndpoint = new ResearchDraftPlacementHitboxEndpoint(
            ResearchDraftPlacementHitboxOwner.Moving,
            10);
        var collision = ResearchDraftPlacementValidator.DetectHitboxes(
            [
                ResearchDraftPlacementHitbox.Connector(
                    1,
                    ResearchDraftPlacementHitboxOwner.Moving,
                    new Vector2(1f, 0f),
                    new Vector2(10f, 0f),
                    0.03f,
                    sharedEndpoint,
                    ResearchDraftPlacementHitboxEndpoint.None),
                ResearchDraftPlacementHitbox.Connector(
                    2,
                    ResearchDraftPlacementHitboxOwner.Moving,
                    new Vector2(1f, 1f),
                    new Vector2(5f, -1f),
                    0.03f,
                    sharedEndpoint,
                    ResearchDraftPlacementHitboxEndpoint.None)
            ],
            padding: 0f);

        Assert.True(collision.HasCollision);
        Assert.Contains(1, collision.MovingLineIds);
        Assert.Contains(2, collision.MovingLineIds);
    }

    [Fact]
    public void BoundarySegments_DoNotSelfCollide()
    {
        var profile = ResearchDraftBoundaryProfile.Default;
        var segments = profile.CreateSegments();

        for (var firstIndex = 0; firstIndex < segments.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < segments.Count; secondIndex++)
            {
                var first = segments[firstIndex];
                var second = segments[secondIndex];
                if (AreAdjacent(first, second))
                {
                    continue;
                }

                Assert.False(SegmentsIntersect(first.Start, first.End, second.Start, second.End));
            }
        }
    }

    private static bool AreAdjacent(ResearchDraftBoundarySegment first, ResearchDraftBoundarySegment second)
    {
        return Vector2.DistanceSquared(first.End, second.Start) <= 0.000001f ||
            Vector2.DistanceSquared(first.Start, second.End) <= 0.000001f;
    }

    private static string Describe(ResearchDraftPlacementValidation validation)
    {
        return $"CanPlace={validation.CanPlace}, Structural={validation.IsStructurallyValid}, " +
            $"Reason={validation.FailureReason}, Boundary={validation.Collision.HasBoundaryCollision}, " +
            $"MovingNodes={validation.Collision.MovingNodeIds.Count}, MovingLines={validation.Collision.MovingLineIds.Count}, " +
            $"FixedNodes={validation.Collision.FixedNodeIds.Count}, FixedLines={validation.Collision.FixedLineIds.Count}, " +
            $"BoundaryLines={validation.Collision.BoundaryLineIds.Count}";
    }

    private static bool SegmentsIntersect(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
    {
        var first = firstEnd - firstStart;
        var second = secondEnd - secondStart;
        var denominator = Cross(first, second);
        var betweenStarts = secondStart - firstStart;

        if (MathF.Abs(denominator) <= 0.0001f)
        {
            if (MathF.Abs(Cross(betweenStarts, first)) > 0.0001f)
            {
                return false;
            }

            return RangesOverlap(firstStart.X, firstEnd.X, secondStart.X, secondEnd.X) &&
                   RangesOverlap(firstStart.Y, firstEnd.Y, secondStart.Y, secondEnd.Y);
        }

        var firstRatio = Cross(betweenStarts, second) / denominator;
        var secondRatio = Cross(betweenStarts, first) / denominator;
        return firstRatio is >= 0f and <= 1f &&
               secondRatio is >= 0f and <= 1f;
    }

    private static bool RangesOverlap(float firstStart, float firstEnd, float secondStart, float secondEnd)
    {
        var firstMin = MathF.Min(firstStart, firstEnd);
        var firstMax = MathF.Max(firstStart, firstEnd);
        var secondMin = MathF.Min(secondStart, secondEnd);
        var secondMax = MathF.Max(secondStart, secondEnd);
        return firstMin <= secondMax && secondMin <= firstMax;
    }

    private static float Cross(Vector2 first, Vector2 second)
    {
        return (first.X * second.Y) - (first.Y * second.X);
    }

    internal static ResearchBranch CreateWideBoundaryCollisionBranch()
    {
        var branch = new ResearchBranch();
        var root = branch.SetRoot(new TreeInstanceNode(new SkillNode("Wide Root", "Wide root."), "Boundary"));
        TreeInstanceNode? rightmostChild = null;
        for (var index = 0; index < 32; index++)
        {
            rightmostChild = branch.AddChild(
                root,
                new TreeInstanceNode(new SkillNode($"Wide Child {index}", "Boundary child."), "Boundary"));
        }

        for (var index = 0; index < 32; index++)
        {
            branch.AddChild(
                rightmostChild!,
                new TreeInstanceNode(new SkillNode($"Wide Grandchild {index}", "Boundary grandchild."), "Boundary"));
        }

        return branch;
    }

    private static SkillTree CreateRootOnlySkillTree()
    {
        var skillTree = new SkillTree();
        skillTree.SetRoot(skillTree.IntakeSkillNode(new SkillNode("Hive Core", "Root anchor.")));
        return skillTree;
    }

    private static SkillTree CreateRightShoulderTree(out TreeInstanceNode anchor)
    {
        var skillTree = CreateRootOnlySkillTree();
        var root = skillTree.Root!;
        skillTree.AddChild(root, skillTree.IntakeSkillNode(new SkillNode("Left", "Left branch."), "Existing"), childIndex: 0);
        anchor = skillTree.AddChild(root, skillTree.IntakeSkillNode(new SkillNode("Right", "Right branch."), "Existing"), childIndex: 1);
        skillTree.AddChild(anchor, skillTree.IntakeSkillNode(new SkillNode("Inner", "Inner branch."), "Existing"));
        return skillTree;
    }

    private static SkillTree CreateDeepCrowdedSkillTree(out TreeInstanceNode anchor)
    {
        var skillTree = CreateRootOnlySkillTree();
        var current = skillTree.Root!;
        for (var depth = 0; depth < 3; depth++)
        {
            current = skillTree.AddChild(
                current,
                skillTree.IntakeSkillNode(new SkillNode($"Spine {depth}", "Existing spine."), "Existing"));
        }

        anchor = current;
        for (var index = 0; index < 24; index++)
        {
            skillTree.AddChild(
                anchor,
                skillTree.IntakeSkillNode(new SkillNode($"Crowded {index}", "Existing crowded child."), "Existing"));
        }

        return skillTree;
    }
}
