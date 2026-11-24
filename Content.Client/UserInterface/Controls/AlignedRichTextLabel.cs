using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Controls;

/// <summary>
///     A rich text control that can horizontally align every wrapped line individually.
/// </summary>
/// <remarks>
///     <para>
///         The engine version this fork targets (RobustToolbox v289) has no per-line alignment on
///         <see cref="RichTextLabel"/>, and its wrapping internals (<c>RichTextEntry</c> / <c>WordWrap</c>) are
///         internal. This control therefore re-implements word wrapping on top of the public <see cref="Font"/> and
///         <see cref="MarkupTagManager"/> APIs, recording the resolved font and colour of every rune, and then draws
///         each line with its own alignment offset. That gives true per-line centring instead of centring the block.
///     </para>
///     <para>
///         Any markup tag resolved by the <see cref="MarkupTagManager"/> that changes fonts or colours (bold, italic,
///         colour, ...) is honoured. Tags that supply inline controls are not supported and are skipped.
///     </para>
///     <para>
///         A single word wider than the available width is allowed to overflow rather than being split mid-word,
///         which keeps glyph runs intact for the short strings speech bubbles display.
///     </para>
/// </remarks>
public sealed partial class AlignedRichTextLabel : Control
{
    [Dependency] private IResourceCache _resCache = default!;
    [Dependency] private MarkupTagManager _tagManager = default!;

    private const string FontStyleProperty = "font";
    private const string FontOutlineThicknessStyleProperty = "font-outline-thickness";
    private const string FontOutlineColorStyleProperty = "font-outline-color";

    /// <summary>
    ///     Font used when no <c>font</c> style property is set. Deliberately a real font: the theme default is a
    ///     <see cref="Font.DummyFont"/>, which would silently render nothing.
    /// </summary>
    private const string DefaultFontPath = "/Fonts/NotoSans/NotoSans-Regular.ttf";
    private const int DefaultFontSize = 12;

    private static readonly Color FallbackTextColor = new(200, 200, 200);

    /// <summary>
    ///     Wrapping width used when the control is drawn before it has ever been measured.
    /// </summary>
    private const float FallbackWrapWidth = 1024f;

    /// <summary>
    ///     Horizontal alignment applied to each individual line.
    /// </summary>
    public enum AlignMode : byte
    {
        Left = 0,
        Center = 1,
        Right = 2,
    }

    private enum EvKind : byte
    {
        PushTag,
        PopTag,
        Rune,
    }

    private readonly record struct Ev(EvKind Kind, Rune Rune, Font Font, Color Color, int Advance);

    private readonly record struct DrawRune(Rune Rune, Font Font, Color Color);

    private sealed class Line
    {
        public readonly List<DrawRune> Runes = new();
        public Font Font = default!;
        public float Width;
    }

    private readonly List<Line> _lines = new();

    private FormattedMessage? _message;
    private Type[]? _tagsAllowed;
    private Color _defaultColor = FallbackTextColor;
    private Font? _fallbackFont;

    private float _contentWidth;
    private float _contentHeight;
    private float _builtWidth = float.NaN;
    private float _builtScale = float.NaN;
    private float _wrapWidth = float.NaN;
    private bool _linesValid;

    private AlignMode _align = AlignMode.Center;
    private float _lineHeightScale = 1f;
    private float? _outlineThickness;
    private Color? _outlineColor;

    /// <summary>
    ///     Alignment applied to each wrapped line. Defaults to <see cref="AlignMode.Center"/>.
    /// </summary>
    public AlignMode Align
    {
        get => _align;
        set
        {
            if (_align == value)
                return;

            _align = value;
            InvalidateArrange();
        }
    }

    /// <summary>
    ///     Multiplier applied to the distance between consecutive lines.
    /// </summary>
    public float LineHeightScale
    {
        get => _lineHeightScale;
        set
        {
            if (MathHelper.CloseToPercent(_lineHeightScale, value))
                return;

            _lineHeightScale = value;
            InvalidateLines();
        }
    }

    /// <summary>
    ///     When set, draws a text outline of this thickness behind the glyphs.
    /// </summary>
    public float? OutlineThickness
    {
        get => _outlineThickness;
        set
        {
            if (_outlineThickness == value)
                return;

            _outlineThickness = value;
            InvalidateLines();
        }
    }

    /// <summary>
    ///     Colour of the text outline. Defaults to black when <see cref="OutlineThickness"/> is set.
    /// </summary>
    public Color? OutlineColor
    {
        get => _outlineColor;
        set
        {
            if (_outlineColor == value)
                return;

            _outlineColor = value;
            InvalidateLines();
        }
    }

    public AlignedRichTextLabel()
    {
        IoCManager.InjectDependencies(this);
    }

    /// <summary>
    ///     Sets the formatted message displayed by this control.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="tagsAllowed">
    ///     The set of allowed markup tag types. If <c>null</c>, every tag the tag manager knows about is allowed.
    /// </param>
    /// <param name="defaultColor">Colour used for text without an explicit colour. Defaults to a light grey.</param>
    public void SetMessage(FormattedMessage message, Type[]? tagsAllowed = null, Color? defaultColor = null)
    {
        _message = new FormattedMessage(message);
        _tagsAllowed = tagsAllowed;
        _defaultColor = defaultColor ?? FallbackTextColor;
        InvalidateLines();
    }

    /// <summary>
    ///     Sets the message from a permissively parsed markup string.
    /// </summary>
    public void SetMessage(string markup, Type[]? tagsAllowed = null, Color? defaultColor = null)
    {
        SetMessage(FormattedMessage.FromMarkupPermissive(markup), tagsAllowed, defaultColor);
    }

    public void Clear()
    {
        _message = null;
        InvalidateLines();
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();
        InvalidateLines();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _wrapWidth = ResolveWrapWidth(availableSize.X);
        EnsureLines(_wrapWidth);

        return new Vector2(_contentWidth, _contentHeight);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        // Lines are destroyed whenever the stylesheet updates, and a restyle invalidates this control's measure.
        // Bubbles are drawn from an overlay and are not inside the UI tree, so the UI manager's queued measure pass
        // skips them and nothing would ever rebuild the lines. Rebuild lazily here so drawing is always correct.
        EnsureLines(float.IsNaN(_wrapWidth) ? ResolveWrapWidth(Size.X) : _wrapWidth);

        if (_lines.Count == 0)
            return;

        var outline = TextOutline.FromOverrides(GetOutlineThickness(), GetOutlineColor());

        if (outline != null)
            DrawPass(handle, outline.Value);

        DrawPass(handle, null);
    }

    /// <summary>
    ///     Wrapping width to lay the message out with for a given available width.
    /// </summary>
    private float ResolveWrapWidth(float availableWidth)
    {
        if (!float.IsNaN(availableWidth) && !float.IsInfinity(availableWidth) && availableWidth > 0f)
            return availableWidth;

        return MaxWidth is > 0f and < float.PositiveInfinity ? MaxWidth : FallbackWrapWidth;
    }

    /// <summary>
    ///     Rebuilds the wrapped lines if the message, the stylesheet or the UI scale changed.
    /// </summary>
    private void EnsureLines(float wrapWidth)
    {
        if (_linesValid &&
            MathHelper.CloseToPercent(_builtWidth, wrapWidth) &&
            MathHelper.CloseToPercent(_builtScale, UIScale))
        {
            return;
        }

        RebuildLines(wrapWidth);
    }

    private void DrawPass(DrawingHandleScreen handle, TextOutline? outline)
    {
        var uiScale = UIScale;
        var baselineFont = GetFont();
        var y = 0f;

        foreach (var line in _lines)
        {
            var offset = GetLineOffset(line.Width);
            var baseline = new Vector2(offset, y + baselineFont.GetAscent(uiScale));

            foreach (var rune in line.Runes)
            {
                var advance = outline != null
                    ? rune.Font.DrawCharOutline(handle, rune.Rune, baseline, uiScale, outline.Value)
                    : rune.Font.DrawChar(handle, rune.Rune, baseline, uiScale, rune.Color);

                baseline += new Vector2(advance, 0f);
            }

            y += GetLineHeight(baselineFont, uiScale);
        }
    }

    private float GetLineOffset(float lineWidth)
    {
        var remaining = Size.X - lineWidth;
        if (remaining <= 0f)
            return 0f;

        var offset = _align switch
        {
            AlignMode.Right => remaining,
            AlignMode.Center => remaining / 2f,
            _ => 0f,
        };

        return MathF.Floor(MathF.Max(0f, offset));
    }

    private float GetLineHeight(Font font, float uiScale)
    {
        return font.GetLineHeight(uiScale) * _lineHeightScale;
    }

    private void InvalidateLines()
    {
        _linesValid = false;
        _builtWidth = float.NaN;
        _builtScale = float.NaN;
        _lines.Clear();
        _contentWidth = 0f;
        _contentHeight = 0f;
        InvalidateMeasure();
    }

    private Font GetFont()
    {
        if (TryGetStyleProperty<Font>(FontStyleProperty, out var font))
            return font;

        // Falling back to the theme default would use a DummyFont, whose metrics are all zero, which silently
        // collapses the label. Use a real font instead so text never becomes invisible.
        return _fallbackFont ??= _resCache.GetFont(DefaultFontPath, DefaultFontSize);
    }

    /// <summary>
    ///     Outline thickness to draw with, preferring the explicit property over the stylesheet value.
    /// </summary>
    private float? GetOutlineThickness()
    {
        if (OutlineThickness is { } thickness)
            return thickness;

        return TryGetStyleProperty<float>(FontOutlineThicknessStyleProperty, out var styleThickness)
            ? styleThickness
            : null;
    }

    /// <summary>
    ///     Outline colour to draw with, preferring the explicit property over the stylesheet value.
    /// </summary>
    private Color? GetOutlineColor()
    {
        if (OutlineColor is { } color)
            return color;

        return TryGetStyleProperty<Color>(FontOutlineColorStyleProperty, out var styleColor)
            ? styleColor
            : null;
    }

    /// <summary>
    ///     Resolves every rune of the message to the font and colour it will be drawn with, then greedily wraps the
    ///     result into lines of at most <paramref name="maxWidth"/> pixels.
    /// </summary>
    private void RebuildLines(float maxWidth)
    {
        _lines.Clear();
        _contentWidth = 0f;
        _contentHeight = 0f;
        _builtWidth = maxWidth;
        _builtScale = UIScale;
        _linesValid = true;

        if (_message == null || _message.Count == 0)
            return;

        var uiScale = UIScale;
        var defaultFont = GetFont();
        var events = BuildEvents(defaultFont, uiScale);
        if (events.Count == 0)
            return;

        var spans = WrapEvents(events, maxWidth);

        foreach (var (start, end) in spans)
        {
            var line = new Line { Font = defaultFont };
            var hasFirstFont = false;

            for (var i = start; i < end; i++)
            {
                var ev = events[i];
                if (ev.Kind != EvKind.Rune)
                    continue;

                if (!hasFirstFont)
                {
                    line.Font = ev.Font;
                    hasFirstFont = true;
                }

                line.Runes.Add(new DrawRune(ev.Rune, ev.Font, ev.Color));
                line.Width += ev.Advance;
            }

            if (!hasFirstFont)
                line.Font = defaultFont;

            _contentWidth = MathF.Max(_contentWidth, line.Width);
            _contentHeight += GetLineHeight(defaultFont, uiScale);
            _lines.Add(line);
        }
    }

    /// <summary>
    ///     Flattens the message into a stream of tag and rune events, recording the font, colour and advance of every
    ///     drawn rune.
    /// </summary>
    private List<Ev> BuildEvents(Font defaultFont, float uiScale)
    {
        var events = new List<Ev>();
        var context = new MarkupDrawingContext();
        context.Color.Push(_defaultColor);
        context.Font.Push(defaultFont);

        foreach (var node in _message!.Nodes)
        {
            if (node.Name == null)
            {
                AppendRunes(events, node.Value.StringValue, context, defaultFont, uiScale);
                continue;
            }

            if (!_tagManager.TryGetMarkupTagHandler(node.Name, _tagsAllowed, out var handler))
                continue;

            if (!node.Closing)
            {
                events.Add(new Ev(EvKind.PushTag, default, defaultFont, default, 0));
                handler.PushDrawContext(node, context);
                AppendRunes(events, handler.TextBefore(node), context, defaultFont, uiScale);
            }
            else
            {
                AppendRunes(events, handler.TextAfter(node), context, defaultFont, uiScale);
                handler.PopDrawContext(node, context);
                events.Add(new Ev(EvKind.PopTag, default, defaultFont, default, 0));
            }
        }

        return events;
    }

    private static void AppendRunes(
        List<Ev> events,
        string? text,
        MarkupDrawingContext context,
        Font defaultFont,
        float uiScale)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var font = context.Font.Count > 0 ? context.Font.Peek() : defaultFont;
        var color = context.Color.Count > 0 ? context.Color.Peek() : FallbackTextColor;

        var enumerator = text.EnumerateRunes();
        while (enumerator.MoveNext())
        {
            var rune = enumerator.Current;
            var advance = 0;
            if (rune.Value != '\n' && font.TryGetCharMetrics(rune, uiScale, out var metrics))
                advance = metrics.Advance;

            events.Add(new Ev(EvKind.Rune, rune, font, color, advance));
        }
    }

    /// <summary>
    ///     Greedy word wrapping over a flattened event stream. Returns the event index ranges of every line.
    /// </summary>
    private static List<(int Start, int End)> WrapEvents(List<Ev> events, float maxWidth)
    {
        // Split the rune stream into word tokens so breaks only land on word boundaries.
        var tokens = new List<(int Start, int End, float Width, bool Newline)>();
        var currentStart = -1;
        var currentWidth = 0f;

        for (var i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            if (ev.Kind != EvKind.Rune)
                continue;

            if (ev.Rune.Value == '\n')
            {
                if (currentStart >= 0)
                {
                    tokens.Add((currentStart, i, currentWidth, false));
                    currentStart = -1;
                    currentWidth = 0f;
                }

                tokens.Add((i, i + 1, 0f, true));
                continue;
            }

            if (currentStart < 0)
                currentStart = i;

            currentWidth += ev.Advance;

            if (IsWordBoundary(ev.Rune))
            {
                tokens.Add((currentStart, i + 1, currentWidth, false));
                currentStart = -1;
                currentWidth = 0f;
            }
        }

        if (currentStart >= 0)
            tokens.Add((currentStart, events.Count, currentWidth, false));

        var lines = new List<(int Start, int End)>();
        var lineStart = 0;
        var lineWidth = 0f;

        foreach (var token in tokens)
        {
            if (token.Newline)
            {
                lines.Add((lineStart, token.Start));
                lineStart = token.End;
                lineWidth = 0f;
                continue;
            }

            // Never start a line with a break, otherwise the first token of every line would be pushed down.
            if (lineWidth > 0f && lineWidth + token.Width > maxWidth)
            {
                lines.Add((lineStart, token.Start));
                lineStart = token.Start;
                lineWidth = 0f;
            }

            lineWidth += token.Width;
        }

        lines.Add((lineStart, events.Count));
        return lines;
    }

    private static bool IsWordBoundary(Rune rune)
    {
        return rune.Value is ' ' or '-';
    }
}
