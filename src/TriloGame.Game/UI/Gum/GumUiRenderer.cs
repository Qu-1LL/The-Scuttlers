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
    private readonly List<ContainerRuntime> _clippingContainers = [];
    private int _filledRectangleCount;
    private int _roundedRectangleCount;
    private int _textCount;
    private int _spriteCount;
    private int _clippingContainerCount;

    public GumUiRenderer()
        : this(addToManagers: true)
    {
    }

    internal GumUiRenderer(bool addToManagers)
    {
        Root = new ContainerRuntime
        {
            Name = "GameUiRoot"
        };
        ConfigureElement(Root);
        if (addToManagers)
        {
            Root.AddToManagers();
        }
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
        _clippingContainerCount = 0;
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

        for (var index = _clippingContainerCount; index < _clippingContainers.Count; index++)
        {
            _clippingContainers[index].Visible = false;
            _clippingContainers[index].Children.Clear();
        }
    }

    public void AddFilledRectangle(Rectangle bounds, Color color)
    {
        AddFilledRectangle(Root, bounds, color);
    }

    public void AddFilledRectangle(ContainerRuntime parent, Rectangle bounds, Color color)
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
        rectangle.Rotation = 0f;
        parent.Children.Add(rectangle);
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
        rectangle.Color = color;
        GumRoundedRectangleRuntimeShape.Apply(
            rectangle,
            Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2),
            isFilled: true,
            strokeWidth: 1f);
        Root.Children.Add(rectangle);
    }

    public void AddRoundedOutline(Rectangle bounds, Color color, int thickness, int radius)
    {
        AddRoundedOutline(Root, bounds, color, thickness, radius);
    }

    public void AddRoundedOutline(ContainerRuntime parent, Rectangle bounds, Color color, int thickness, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || thickness <= 0 || color.A == 0)
        {
            return;
        }

        var rectangle = GetRoundedRectangle(_roundedRectangleCount++);
        rectangle.Visible = true;
        rectangle.X = bounds.X;
        rectangle.Y = bounds.Y;
        rectangle.Width = bounds.Width;
        rectangle.Height = bounds.Height;
        rectangle.Color = color;
        GumRoundedRectangleRuntimeShape.Apply(
            rectangle,
            Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2),
            isFilled: false,
            strokeWidth: thickness);
        parent.Children.Add(rectangle);
    }

    public void AddRoundedFrame(Rectangle bounds, Color fill, Color border, int thickness, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        AddRoundedRectangle(bounds, fill, radius);
        if (thickness <= 0)
        {
            return;
        }

        AddRoundedOutline(bounds, border, thickness, radius);
    }

    public void AddRoundedFrame(ContainerRuntime parent, Rectangle bounds, Color fill, Color border, int thickness, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        AddRoundedRectangle(parent, bounds, fill, radius);
        if (thickness <= 0)
        {
            return;
        }

        AddRoundedOutline(parent, bounds, border, thickness, radius);
    }

    public void AddLine(Vector2 start, Vector2 end, Color color, int thickness = 2)
    {
        AddLine(Root, start, end, color, thickness);
    }

    public void AddLine(ContainerRuntime parent, Vector2 start, Vector2 end, Color color, int thickness = 2)
    {
        if (color.A == 0 || thickness <= 0)
        {
            return;
        }

        var delta = end - start;
        var distance = delta.Length();
        if (distance <= 0f)
        {
            var singlePointBounds = new Rectangle(
                (int)MathF.Round(start.X) - (thickness / 2),
                (int)MathF.Round(start.Y) - (thickness / 2),
                thickness,
                thickness);
            AddFilledRectangle(parent, singlePointBounds, color);
            return;
        }

        var layout = CreateLineLayout(start, end, thickness);
        var rectangle = GetFilledRectangle(_filledRectangleCount++);
        rectangle.Visible = true;
        rectangle.X = layout.X;
        rectangle.Y = layout.Y;
        rectangle.Width = layout.Width;
        rectangle.Height = layout.Height;
        rectangle.Color = color;
        rectangle.Rotation = layout.Rotation;
        parent.Children.Add(rectangle);
    }

    internal static GumLineLayout CreateLineLayout(Vector2 start, Vector2 end, int thickness)
    {
        if (thickness <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(thickness), "Line thickness must be positive.");
        }

        var delta = end - start;
        var distance = delta.Length();
        if (distance <= 0f)
        {
            throw new ArgumentException("Line start and end points must differ.", nameof(end));
        }

        // Gum uses positive rotation as counterclockwise in screen space, so
        // screen-space Y deltas need to be inverted when converting to an angle.
        var rotationRadians = MathF.Atan2(-delta.Y, delta.X);
        var halfThickness = thickness / 2f;
        return new GumLineLayout(
            start.X - (MathF.Sin(rotationRadians) * halfThickness),
            start.Y - (MathF.Cos(rotationRadians) * halfThickness),
            MathF.Max(1f, distance),
            MathF.Max(1f, thickness),
            MathHelper.ToDegrees(rotationRadians));
    }

    public void AddText(
        Rectangle bounds,
        string text,
        Color color,
        HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment verticalAlignment = VerticalAlignment.Center,
        int fontSize = 18,
        int maxLines = 0,
        string? fontFamily = null,
        string? customFontFile = null,
        float fontScale = 1f)
    {
        AddText(Root, bounds, text, color, horizontalAlignment, verticalAlignment, fontSize, maxLines, fontFamily, customFontFile, fontScale);
    }

    public void AddText(
        ContainerRuntime parent,
        Rectangle bounds,
        string text,
        Color color,
        HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment verticalAlignment = VerticalAlignment.Center,
        int fontSize = 18,
        int maxLines = 0,
        string? fontFamily = null,
        string? customFontFile = null,
        float fontScale = 1f)
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
        textRuntime.UseCustomFont = !string.IsNullOrWhiteSpace(customFontFile);
        textRuntime.CustomFontFile = customFontFile ?? string.Empty;
        textRuntime.Font = fontFamily ?? GumTextStyleCatalog.DefaultFontFamily;
        textRuntime.FontSize = fontSize;
        textRuntime.FontScale = MathF.Max(0.01f, fontScale);
        textRuntime.MaxNumberOfLines = maxLines;
        textRuntime.Text = text;
        parent.Children.Add(textRuntime);
    }

    public void AddSprite(Rectangle bounds, Texture2D texture, Color? color = null)
    {
        AddSprite(bounds, texture, new Rectangle(0, 0, texture.Width, texture.Height), color);
    }

    public void AddSprite(Rectangle bounds, Texture2D texture, Rectangle sourceRectangle, Color? color = null)
    {
        AddSprite(Root, bounds, texture, sourceRectangle, color);
    }

    public ContainerRuntime AddClippingContainer(Rectangle bounds)
    {
        var container = GetClippingContainer(_clippingContainerCount++);
        container.Visible = true;
        container.X = bounds.X;
        container.Y = bounds.Y;
        container.Width = bounds.Width;
        container.Height = bounds.Height;
        container.ClipsChildren = true;
        container.Children.Clear();
        Root.Children.Add(container);
        return container;
    }

    public void AddSprite(ContainerRuntime parent, Rectangle bounds, Texture2D texture, Rectangle sourceRectangle, Color? color = null)
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
        sprite.SourceRectangle = sourceRectangle;
        sprite.Color = color ?? Color.White;
        parent.Children.Add(sprite);
    }

    public void AddRoundedRectangle(ContainerRuntime parent, Rectangle bounds, Color color, int radius)
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
        rectangle.Color = color;
        GumRoundedRectangleRuntimeShape.Apply(
            rectangle,
            Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2),
            isFilled: true,
            strokeWidth: 1f);
        parent.Children.Add(rectangle);
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

    private ContainerRuntime GetClippingContainer(int index)
    {
        while (_clippingContainers.Count <= index)
        {
            var container = new ContainerRuntime
            {
                Visible = false
            };
            ConfigureElement(container);
            _clippingContainers.Add(container);
        }

        return _clippingContainers[index];
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

internal readonly record struct GumLineLayout(
    float X,
    float Y,
    float Width,
    float Height,
    float Rotation);
