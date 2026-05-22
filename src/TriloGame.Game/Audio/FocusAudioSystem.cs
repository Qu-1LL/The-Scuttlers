using Microsoft.Xna.Framework;
using TriloGame.Game.Core.Buildings;
using TriloGame.Game.Core.Constants;
using TriloGame.Game.Core.Simulation;
using TriloGame.Game.Rendering;

namespace TriloGame.Game.Audio;

public sealed class FocusAudioSystem
{
    private const float MinAudibleScale = 0.65f;
    private const float FullAudibleScale = 1.8f;
    private const float AudibleRadiusPixels = 320f;
    private const float PlayThreshold = 0.08f;

    private readonly AudioService _audio;

    public FocusAudioSystem(AudioService audio)
    {
        _audio = audio;
    }

    public void Reset()
    {
        _audio.StopLoop(GameAudioCue.MiningPostFocus);
    }

    public void Update(GameSession session, CameraController camera)
    {
        var posts = session.Cave?.GetMiningPosts();
        if (posts is null || posts.Count == 0)
        {
            _audio.StopLoop(GameAudioCue.MiningPostFocus);
            return;
        }

        var bestScore = 0f;

        for (var index = 0; index < posts.Count; index++)
        {
            var post = posts[index];
            if (post.Location is null)
            {
                continue;
            }

            var postCenter = GetPlacedBuildingWorldCenter(post);
            var screenPosition = camera.WorldToScreen(postCenter);
            var score = CalculateFocusScore(screenPosition, camera.ViewCenter, camera.CurrentScale);

            if (score > bestScore)
            {
                bestScore = score;
            }
        }

        if (bestScore < PlayThreshold)
        {
            _audio.StopLoop(GameAudioCue.MiningPostFocus);
            return;
        }

        _audio.StartLoop(GameAudioCue.MiningPostFocus, Smooth(bestScore));
    }

    private static float CalculateFocusScore(Vector2 screenPosition, Vector2 viewCenter, float scale)
    {
        var zoomFactor = InverseLerp(MinAudibleScale, FullAudibleScale, scale);
        var distance = Vector2.Distance(screenPosition, viewCenter);
        var centerFactor = 1f - Math.Clamp(distance / AudibleRadiusPixels, 0f, 1f);

        return zoomFactor * centerFactor;
    }

    private static Vector2 GetPlacedBuildingWorldCenter(Building building)
    {
        var location = building.Location!.Value;
        return new Vector2(
            (location.X * TileConstants.TileSize) + ((building.Size.X - 1) * TileConstants.TileHalfSize),
            (location.Y * TileConstants.TileSize) + ((building.Size.Y - 1) * TileConstants.TileHalfSize));
    }

    private static float InverseLerp(float min, float max, float value)
    {
        if (max <= min)
        {
            return value >= max ? 1f : 0f;
        }

        return Math.Clamp((value - min) / (max - min), 0f, 1f);
    }

    private static float Smooth(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return value * value * (3f - (2f * value));
    }
}