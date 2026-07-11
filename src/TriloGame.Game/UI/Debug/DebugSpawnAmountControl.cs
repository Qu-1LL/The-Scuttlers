using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RenderingLibrary.Graphics;
using TriloGame.Game.UI.Gum;
using TriloGame.Game.UI.Input;

namespace TriloGame.Game.UI.Debug;

public enum DebugSpawnControlInteraction
{
    None,
    Consumed,
    SpawnRequested
}

public readonly record struct DebugSpawnAmountControlLayout(
    Rectangle Bounds,
    Rectangle ActionBounds,
    Rectangle ValueBounds,
    Rectangle IncrementBounds,
    Rectangle DecrementBounds);

public sealed class DebugSpawnAmountControl
{
    public const int MinimumAmount = 1;
    public const int MaximumAmount = 999;
    internal const string IncrementLabel = "+";
    internal const string DecrementLabel = "-";

    private string _editBuffer;
    private int _amountBeforeEditing;
    private bool _replaceOnNextDigit;

    public DebugSpawnAmountControl(string entityLabel, int initialAmount = MinimumAmount)
    {
        if (string.IsNullOrWhiteSpace(entityLabel))
        {
            throw new ArgumentException("A spawn control requires an entity label.", nameof(entityLabel));
        }

        EntityLabel = entityLabel.Trim();
        Amount = Math.Clamp(initialAmount, MinimumAmount, MaximumAmount);
        _editBuffer = Amount.ToString();
    }

    public string EntityLabel { get; }

    public string ActionLabel => $"Spawn {EntityLabel}";

    public int Amount { get; private set; }

    public bool IsEditing { get; private set; }

    internal string DisplayValueText => IsEditing ? $"{_editBuffer}|" : Amount.ToString();

    public static DebugSpawnAmountControlLayout BuildLayout(Rectangle bounds)
    {
        var inset = Math.Max(3, Math.Min(5, bounds.Height / 8));
        var inner = new Rectangle(
            bounds.X + inset,
            bounds.Y + inset,
            Math.Max(0, bounds.Width - (inset * 2)),
            Math.Max(0, bounds.Height - (inset * 2)));
        var stepperWidth = Math.Clamp(inner.Width / 9, 20, 26);
        var numericWidth = Math.Clamp(inner.Width * 2 / 5, 76, 96);
        numericWidth = Math.Min(numericWidth, inner.Width);
        var numericX = inner.Right - numericWidth;
        var valueBounds = new Rectangle(
            numericX,
            inner.Y,
            Math.Max(0, numericWidth - stepperWidth - 2),
            inner.Height);
        var stepperX = valueBounds.Right + 2;
        var upperHeight = inner.Height / 2;
        var incrementBounds = new Rectangle(stepperX, inner.Y, stepperWidth, upperHeight);
        var decrementBounds = new Rectangle(
            stepperX,
            incrementBounds.Bottom,
            stepperWidth,
            Math.Max(0, inner.Bottom - incrementBounds.Bottom));
        var actionBounds = new Rectangle(
            inner.X,
            inner.Y,
            Math.Max(0, numericX - inner.X - 4),
            inner.Height);

        return new DebugSpawnAmountControlLayout(
            bounds,
            actionBounds,
            valueBounds,
            incrementBounds,
            decrementBounds);
    }

    public DebugSpawnControlInteraction HandlePointerReleased(Rectangle bounds, Point point)
    {
        var layout = BuildLayout(bounds);
        if (layout.IncrementBounds.Contains(point))
        {
            CommitEdit();
            SetAmount(Amount + 1);
            return DebugSpawnControlInteraction.Consumed;
        }

        if (layout.DecrementBounds.Contains(point))
        {
            CommitEdit();
            SetAmount(Amount - 1);
            return DebugSpawnControlInteraction.Consumed;
        }

        if (layout.ValueBounds.Contains(point))
        {
            BeginEdit();
            return DebugSpawnControlInteraction.Consumed;
        }

        if (layout.ActionBounds.Contains(point))
        {
            CommitEdit();
            return DebugSpawnControlInteraction.SpawnRequested;
        }

        CommitEdit();
        return DebugSpawnControlInteraction.None;
    }

    public bool HandleKeyboard(InputController input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!IsEditing)
        {
            return false;
        }

        var handled = false;
        foreach (var key in input.CurrentKeyboard.GetPressedKeys())
        {
            if (!input.PreviousKeyboard.IsKeyUp(key))
            {
                continue;
            }

            handled |= HandleKey(key);
        }

        return handled;
    }

    internal bool HandleKey(Keys key)
    {
        if (!IsEditing)
        {
            return false;
        }

        if (TryGetDigit(key, out var digit))
        {
            if (_replaceOnNextDigit)
            {
                _editBuffer = digit.ToString();
                _replaceOnNextDigit = false;
            }
            else if (_editBuffer.Length < MaximumAmount.ToString().Length)
            {
                _editBuffer += digit;
            }

            ApplyEditBuffer();
            return true;
        }

        switch (key)
        {
            case Keys.Back:
                _replaceOnNextDigit = false;
                if (_editBuffer.Length > 0)
                {
                    _editBuffer = _editBuffer[..^1];
                }

                ApplyEditBuffer();
                return true;
            case Keys.Up:
                _replaceOnNextDigit = true;
                SetAmount(Amount + 1);
                _editBuffer = Amount.ToString();
                return true;
            case Keys.Down:
                _replaceOnNextDigit = true;
                SetAmount(Amount - 1);
                _editBuffer = Amount.ToString();
                return true;
            case Keys.Enter:
            case Keys.Tab:
                CommitEdit();
                return true;
            case Keys.Escape:
                CancelEdit();
                return true;
            default:
                return false;
        }
    }

    public void Draw(GumUiRenderer gumUi, Rectangle bounds, Point pointer)
    {
        ArgumentNullException.ThrowIfNull(gumUi);
        var layout = BuildLayout(bounds);
        var actionHovered = layout.ActionBounds.Contains(pointer);
        var incrementHovered = layout.IncrementBounds.Contains(pointer);
        var decrementHovered = layout.DecrementBounds.Contains(pointer);
        var outerFill = actionHovered ? new Color(64, 83, 101) : new Color(36, 50, 64);
        var outerBorder = actionHovered ? new Color(210, 187, 136) : new Color(96, 120, 138);

        gumUi.AddFilledRectangle(bounds, outerFill);
        gumUi.AddRectangleOutline(bounds, outerBorder, 2);
        GumUiText.AddFittedCentered(
            gumUi,
            layout.ActionBounds,
            ActionLabel,
            Color.White,
            GumTextStyle.Compact);

        var valueFill = IsEditing ? new Color(12, 37, 49) : new Color(11, 23, 32);
        var valueBorder = IsEditing ? new Color(105, 220, 232) : new Color(82, 112, 128);
        gumUi.AddFilledRectangle(layout.ValueBounds, valueFill);
        gumUi.AddRectangleOutline(layout.ValueBounds, valueBorder, IsEditing ? 2 : 1);
        GumUiText.AddFittedCentered(
            gumUi,
            layout.ValueBounds,
            DisplayValueText,
            Color.White,
            GumTextStyle.Compact);

        DrawStepper(gumUi, layout.IncrementBounds, IncrementLabel, incrementHovered);
        DrawStepper(gumUi, layout.DecrementBounds, DecrementLabel, decrementHovered);
    }

    private static void DrawStepper(GumUiRenderer gumUi, Rectangle bounds, string glyph, bool hovered)
    {
        gumUi.AddFilledRectangle(bounds, hovered ? new Color(66, 91, 108) : new Color(28, 44, 57));
        gumUi.AddRectangleOutline(bounds, hovered ? new Color(210, 187, 136) : new Color(82, 112, 128), 1);
        GumUiText.AddFittedCentered(gumUi, bounds, glyph, Color.White, GumTextStyle.Compact);
    }

    private void BeginEdit()
    {
        if (!IsEditing)
        {
            _amountBeforeEditing = Amount;
        }

        IsEditing = true;
        _editBuffer = Amount.ToString();
        _replaceOnNextDigit = true;
    }

    private void CommitEdit()
    {
        if (!IsEditing)
        {
            return;
        }

        ApplyEditBuffer();
        IsEditing = false;
        _editBuffer = Amount.ToString();
        _replaceOnNextDigit = false;
    }

    private void CancelEdit()
    {
        SetAmount(_amountBeforeEditing);
        IsEditing = false;
        _editBuffer = Amount.ToString();
        _replaceOnNextDigit = false;
    }

    private void ApplyEditBuffer()
    {
        if (int.TryParse(_editBuffer, out var parsed))
        {
            SetAmount(parsed);
        }
    }

    private void SetAmount(int amount)
    {
        Amount = Math.Clamp(amount, MinimumAmount, MaximumAmount);
    }

    private static bool TryGetDigit(Keys key, out char digit)
    {
        if (key is >= Keys.D0 and <= Keys.D9)
        {
            digit = (char)('0' + (key - Keys.D0));
            return true;
        }

        if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            digit = (char)('0' + (key - Keys.NumPad0));
            return true;
        }

        digit = default;
        return false;
    }
}
