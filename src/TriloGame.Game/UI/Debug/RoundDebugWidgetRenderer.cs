using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using TriloGame.Game.Runtime.Systems;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Game.UI.Debug;

public sealed class RoundDebugWidgetRenderer
{
    private static readonly GumUiFrameStyle TimerNormalFrameStyle = new(new Color(16, 38, 54), new Color(54, 88, 107), 2, 14);
    private static readonly GumUiFrameStyle TimerHoverFrameStyle = new(new Color(22, 50, 71), new Color(125, 179, 196), 2, 14);
    private static readonly GumUiFrameStyle RoundNormalFrameStyle = new(new Color(48, 74, 61), new Color(132, 173, 150), 2, 14);
    private static readonly GumUiFrameStyle RoundHoverFrameStyle = new(new Color(74, 104, 87), new Color(207, 242, 220), 2, 14);
    private static readonly GumUiFrameStyle DisabledRoundFrameStyle = new(new Color(33, 40, 44), new Color(92, 104, 112), 2, 14);

    public void Draw(
        GumUiRenderer gumUi,
        RoundDebugWidgetLayoutInfo layout,
        Point pointer,
        RoundInfo round,
        RoundDebugWidgetAction roundButtonAction)
    {
        if (layout.TimerBounds.Width <= 0 || layout.RoundBounds.Width <= 0)
        {
            return;
        }

        var model = RoundDebugWidgetPresenter.Build(round, roundButtonAction);
        var timerHovered = layout.TimerBounds.Contains(pointer);
        var roundHovered = model.RoundButtonEnabled && layout.RoundBounds.Contains(pointer);

        GumUiChrome.DrawFrame(gumUi, layout.TimerBounds, timerHovered ? TimerHoverFrameStyle : TimerNormalFrameStyle);
        GumUiChrome.DrawFrame(gumUi, layout.RoundBounds, !model.RoundButtonEnabled ? DisabledRoundFrameStyle : roundHovered ? RoundHoverFrameStyle : RoundNormalFrameStyle);

        GumUiText.AddFittedCentered(gumUi, layout.TimerLabelBounds, model.TimerLabel, Color.White, GumTextStyle.Compact);
        GumUiText.AddFittedCentered(gumUi, layout.TimerValueBounds, model.TimerValue, Color.White, GumTextStyle.Small);
        GumUiText.AddFittedCentered(
            gumUi,
            layout.RoundValueBounds,
            model.RoundValue,
            model.RoundButtonEnabled ? Color.White : new Color(183, 191, 196),
            GumTextStyle.Small);
    }
}
