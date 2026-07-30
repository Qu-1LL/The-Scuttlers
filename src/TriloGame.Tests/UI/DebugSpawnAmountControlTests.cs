using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Gum.GueDeriving;
using TriloGame.Game.UI.Debug;
using TriloGame.Game.UI.Gum;

namespace TriloGame.Tests.UI;

public sealed class DebugSpawnAmountControlTests
{
    [Fact]
    public void BuildLayout_KeepsActionValueAndSteppersInsideControl()
    {
        var bounds = new Rectangle(20, 30, 260, 40);

        var layout = DebugSpawnAmountControl.BuildLayout(bounds);

        Assert.True(bounds.Contains(layout.ActionBounds));
        Assert.True(bounds.Contains(layout.ValueBounds));
        Assert.True(bounds.Contains(layout.IncrementBounds));
        Assert.True(bounds.Contains(layout.DecrementBounds));
        Assert.True(layout.ActionBounds.Right < layout.ValueBounds.Left);
        Assert.Equal(layout.IncrementBounds.Bottom, layout.DecrementBounds.Top);
        Assert.True(layout.ValueBounds.Width >= GumTextLayout.Measure("999|", GumTextStyle.Compact).X);
        Assert.Equal("+", DebugSpawnAmountControl.IncrementLabel);
        Assert.Equal("-", DebugSpawnAmountControl.DecrementLabel);
    }

    [Fact]
    public void EditableValue_ReplacesExistingAmountAndReturnsItForSpawn()
    {
        var control = new DebugSpawnAmountControl("Enemy", initialAmount: 1);
        var bounds = new Rectangle(20, 30, 160, 40);
        var layout = DebugSpawnAmountControl.BuildLayout(bounds);

        Assert.Equal(
            DebugSpawnControlInteraction.Consumed,
            control.HandlePointerReleased(bounds, layout.ValueBounds.Center));
        Assert.True(control.HandleKey(Keys.D2));
        Assert.True(control.HandleKey(Keys.D5));
        Assert.True(control.HandleKey(Keys.Enter));

        var interaction = control.HandlePointerReleased(bounds, layout.ActionBounds.Center);

        Assert.Equal(25, control.Amount);
        Assert.Equal(DebugSpawnControlInteraction.SpawnRequested, interaction);
    }

    [Fact]
    public void EditableValue_DisplaysAllThreeDigitsWithoutChangingLayout()
    {
        var control = new DebugSpawnAmountControl("Enemy");
        var bounds = new Rectangle(20, 30, 260, 40);
        var layoutBeforeEditing = DebugSpawnAmountControl.BuildLayout(bounds);

        control.HandlePointerReleased(bounds, layoutBeforeEditing.ValueBounds.Center);
        control.HandleKey(Keys.D9);
        control.HandleKey(Keys.D9);
        control.HandleKey(Keys.D9);
        var layoutWhileEditing = DebugSpawnAmountControl.BuildLayout(bounds);

        Assert.Equal(999, control.Amount);
        Assert.Equal("999|", control.DisplayValueText);
        Assert.Equal(layoutBeforeEditing, layoutWhileEditing);
        Assert.True(
            layoutWhileEditing.ValueBounds.Width >=
            GumTextLayout.Measure(control.DisplayValueText, GumTextStyle.Compact).X);
    }

    [Fact]
    public void SteppersClampAmountToSafeRange()
    {
        var control = new DebugSpawnAmountControl("Trilobite", DebugSpawnAmountControl.MinimumAmount);
        var bounds = new Rectangle(20, 30, 160, 40);
        var layout = DebugSpawnAmountControl.BuildLayout(bounds);

        control.HandlePointerReleased(bounds, layout.DecrementBounds.Center);
        Assert.Equal(DebugSpawnAmountControl.MinimumAmount, control.Amount);

        for (var index = 0; index < DebugSpawnAmountControl.MaximumAmount + 5; index++)
        {
            control.HandlePointerReleased(bounds, layout.IncrementBounds.Center);
        }

        Assert.Equal(DebugSpawnAmountControl.MaximumAmount, control.Amount);
    }

    [Fact]
    public void Draw_LabelsSpawnTargetAndRendersEditableQuantityControls()
    {
        var control = new DebugSpawnAmountControl("Enemy", initialAmount: 12);
        var gumUi = new GumUiRenderer(addToManagers: false);
        gumUi.BeginFrame(new Point(320, 180));

        control.Draw(gumUi, new Rectangle(20, 30, 180, 44), Point.Zero);

        Assert.Equal("Spawn Enemy", control.ActionLabel);
        Assert.Equal(12, control.Amount);
        Assert.Equal(4, gumUi.Root.Children.OfType<TextRuntime>().Count());
    }
}
