using Gum.Converters;
using Gum.DataTypes;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;

namespace TriloGame.Game.UI.Gum;

public sealed class GumUiRenderer
{
    private readonly List<ColoredRectangleRuntime> _filledRectangles = [];
    private readonly List<RoundedRectangleRuntime> _roundedRectangles = [];
    private readonly List<TextRuntime> _texts = [];
    private readonly List<SpriteRuntime> _sprites = [];
    private int _filledRectangleCount;
    private int _roundedRectangleCount;
    private int _textCount;
    private int _spriteCount;

    public GumUiRenderer()
    {
        Root = new ContainerRuntime
        {
            Name = "GameUiRoot"
        };
        ConfigureElement(Root);
        Root.AddToManagers();
    }

    public ContainerRuntime Root { get; }

    public void BeginFrame(Point viewport)
    {
        Root.Width = viewport.X;
        Root.Height = viewport.Y;
        Root.Children.Clear();
        _filledRectangleCount = 0;
        _roundedRectangleCount = 0;
        _textCount = 0;
        _spriteCount = 0;
    }

    public void EndFrame()
    {
        for (var index = _filledRectangleCount; index < _filledRectangles.Count; index++)
        {
            _filledRectangles[index].Visible = false;
        }

        for (var index = _roundedRectangleCount; index < _roundedRectangles.Count; index++)
        {
            _roundedRectangles[index].Visible = false;
        }

        for (var index = _textCount; index < _texts.Count; index++)
        {
            _texts[index].Visible = false;
        }

        for (var index = _spriteCount; index < _sprites.Count; index++)
        {
            _sprites[index].Visible = false;
        }
    }

    public void AddFilledRectangle(Rectangle bounds, Color color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || color.A == 0)
        {
            return;
        }

        var rectangle = GetFilledRectangle(_filledRectangleCount++);
        rectangle.Visible = true;
        rectangle.X = bounds.X;
        rectangle.Y = bounds.Y;
        rectangle.Width = bounds.Width;
        rectangle.Height = bounds.Height;
        rectangle.Color = color;
        Root.Children.Add(rectangle);
    }

    public void AddRectangleOutline(Rectangle bounds, Color color, int thickness)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || thickness <= 0 || color.A == 0)
        {
            return;
        }

        AddFilledRectangle(new Rectangle(bounds.X, bounds.Y, bounds.Width, Math.Min(thickness, bounds.Height)), color);
        AddFilledRectangle(new Rectangle(bounds.X, bounds.Bottom - Math.Min(thickness, bounds.Height), bounds.Width, Math.Min(thickness, bounds.Height)), color);
        AddFilledRectangle(new Rectangle(bounds.X, bounds.Y, Math.Min(thickness, bounds.Width), bounds.Height), color);
        AddFilledRectangle(new Rectangle(bounds.Right - Math.Min(thickness, bounds.Width), bounds.Y, Math.Min(thickness, bounds.Width), bounds.Height), color);
    }

    public void AddRoundedRectangle(Rectangle bounds, Color color, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || color.A == 0)
        {
            return;
        }

        var rectangle = GetRoundedRectangle(_roundedRectangleCount++);
        rectangle.Visible = true;
        rectangle.X = bounds.X;
        rectangle.Y = bounds.Y;
        rectangle.Width = bounds.Width;
        rectangle.Height = bounds.Height;
        rectangle.CornerRadius = Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2);
        rectangle.Color = color;
        rectangle.IsFilled = true;
        rectangle.StrokeWidth = 1f;
        Root.Children.Add(rectangle);
    }

    public void AddRoundedFrame(Rectangle bounds, Color fill, Color border, int thickness, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        AddRoundedRectangle(bounds, border, radius);
        if (thickness <= 0)
        {
            return;
        }

        var innerBounds = new Rectangle(
            bounds.X + thickness,
            bounds.Y + thickness,
            Math.Max(0, bounds.Width - (thickness * 2)),
            Math.Max(0, bounds.Height - (thickness * 2)));
        if (innerBounds.Width <= 0 || innerBounds.Height <= 0)
        {
            return;
        }

        AddRoundedRectangle(innerBounds, fill, Math.Max(0, radius - thickness));
    }

    public void AddText(
        Rectangle bounds,
        string text,
        Color color,
        HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment verticalAlignment = VerticalAlignment.Center,
        int fontSize = 18,
        int maxLines = 0)
    {
        if (string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0 || color.A == 0)
        {
            return;
        }

        var textRuntime = GetText(_textCount++);
        textRuntime.Visible = true;
        textRuntime.X = bounds.X;
        textRuntime.Y = bounds.Y;
        textRuntime.Width = bounds.Width;
        textRuntime.Height = bounds.Height;
        textRuntime.Color = color;
        textRuntime.HorizontalAlignment = horizontalAlignment;
        textRuntime.VerticalAlignment = verticalAlignment;
        textRuntime.FontScale = 1f;
        textRuntime.FontSize = fontSize;
        textRuntime.MaxNumberOfLines = maxLines;
        textRuntime.Text = text;
        Root.Children.Add(textRuntime);
    }

    public void AddSprite(Rectangle bounds, Texture2D texture, Color? color = null)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var sprite = GetSprite(_spriteCount++);
        sprite.Visible = true;
        sprite.X = bounds.X;
        sprite.Y = bounds.Y;
        sprite.Width = bounds.Width;
        sprite.Height = bounds.Height;
        sprite.Texture = texture;
        sprite.Color = color ?? Color.White;
        Root.Children.Add(sprite);
    }

    private ColoredRectangleRuntime GetFilledRectangle(int index)
    {
        while (_filledRectangles.Count <= index)
        {
            var rectangle = new ColoredRectangleRuntime
            {
                Visible = false
            };
            ConfigureElement(rectangle);
            _filledRectangles.Add(rectangle);
        }

        return _filledRectangles[index];
    }

    private RoundedRectangleRuntime GetRoundedRectangle(int index)
    {
        while (_roundedRectangles.Count <= index)
        {
            var rectangle = new RoundedRectangleRuntime
            {
                Visible = false
            };
            ConfigureElement(rectangle);
            _roundedRectangles.Add(rectangle);
        }

        return _roundedRectangles[index];
    }

    private TextRuntime GetText(int index)
    {
        while (_texts.Count <= index)
        {
            var text = new TextRuntime
            {
                Visible = false
            };
            ConfigureElement(text);
            _texts.Add(text);
        }

        return _texts[index];
    }

    private SpriteRuntime GetSprite(int index)
    {
        while (_sprites.Count <= index)
        {
            var sprite = new SpriteRuntime
            {
                Visible = false
            };
            ConfigureElement(sprite);
            _sprites.Add(sprite);
        }

        return _sprites[index];
    }

    private static void ConfigureElement(ContainerRuntime container)
    {
        ConfigureElement((GraphicalUiElement)container);
    }

    private static void ConfigureElement(ColoredRectangleRuntime rectangle)
    {
        ConfigureElement((GraphicalUiElement)rectangle);
    }

    private static void ConfigureElement(RoundedRectangleRuntime rectangle)
    {
        ConfigureElement((GraphicalUiElement)rectangle);
    }

    private static void ConfigureElement(TextRuntime text)
    {
        ConfigureElement((GraphicalUiElement)text);
        text.HorizontalAlignment = HorizontalAlignment.Left;
        text.VerticalAlignment = VerticalAlignment.Center;
    }

    private static void ConfigureElement(SpriteRuntime sprite)
    {
        ConfigureElement((GraphicalUiElement)sprite);
    }

    private static void ConfigureElement(GraphicalUiElement element)
    {
        element.X = 0f;
        element.Y = 0f;
        element.Width = 0f;
        element.Height = 0f;
        element.XUnits = GeneralUnitType.PixelsFromSmall;
        element.YUnits = GeneralUnitType.PixelsFromSmall;
        element.WidthUnits = DimensionUnitType.Absolute;
        element.HeightUnits = DimensionUnitType.Absolute;
        element.XOrigin = HorizontalAlignment.Left;
        element.YOrigin = VerticalAlignment.Top;
    }
}
