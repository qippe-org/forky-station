using System.Linq;
using System.Numerics;
using Content.Client.Examine;
using Content.Client.Graphics;
using Content.Client.UserInterface.Systems.Chat;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.Chat.SpeechBubble;

/// <summary>
///     Per-viewport bubble state, currently the lerped vertical offset of every visible bubble.
/// </summary>
public sealed class ViewportBubbleOffset : IDisposable
{
    // Offset achieved, used for lerping.
    public readonly Dictionary<SpeechBubble, float> Achieved = [];

    public void Dispose()
    {
        Achieved.Clear();
    }
}

/// <summary>
///     A single bubble queued for layout this frame.
/// </summary>
public sealed class BubbleLayout
{
    public SpeechBubble Bubble = default!;

    // I tried getting this to work with Box2 but I couldn't figure it out.
    public float CenterX;
    public float BaselineY;
    public float Width;
    public float Height;

    public float TargetVerticalOffset;
}

/// <summary>
///     Draws speech bubbles in world space so that a single bubble is visible in every viewport.
/// </summary>
public sealed partial class SpeechBubbleOverlay : Overlay
{
    [Dependency] private IUserInterfaceManager _uiManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly ChatUIController _chatUIController;

    private readonly ExamineSystem _examineSystem;
    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;

    private readonly OverlayResourceCache<ViewportBubbleOffset> _offsetCache = new();

    private readonly List<BubbleLayout> _layoutCache = [];

    private readonly HashSet<SpeechBubble> _seenBubbles = [];

    private const float VerticalMargin = 2f;

    public SpeechBubbleOverlay(
        ExamineSystem examineSystem,
        SpriteSystem sprite,
        SharedTransformSystem transform)
    {
        _examineSystem = examineSystem;
        _sprite = sprite;
        _transform = transform;

        IoCManager.InjectDependencies(this);

        _chatUIController = _uiManager.GetUIController<ChatUIController>();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Frame-update every bubble so that fades advance.
        // FrameUpdate on controls only runs if they are parented to something, and these are not.
        foreach (var (_, controls) in _chatUIController.ActiveSpeechBubbles)
        {
            foreach (var control in controls)
            {
                control.Update(args);
            }
        }

        foreach (var (_, control) in _chatUIController.ActiveSpeechBubbleNameTags)
        {
            control.Update(args);
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        if (args.Viewport.Eye is not { } eye)
            return;

        var offsetCache = _offsetCache.GetForViewport(args.Viewport, _ => new ViewportBubbleOffset());

        var deltaTime = (float)_timing.FrameTime.TotalSeconds;

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();

        _layoutCache.Clear();

        foreach (var (ent, controls) in _chatUIController.ActiveSpeechBubbles)
        {
            if (!_entityManager.TryGetComponent<TransformComponent>(ent, out var xform))
                continue;

            if (!_entityManager.TryGetComponent<SpriteComponent>(ent, out var sprite))
                continue;

            if (xform.MapID != eye.Position.MapId)
                continue;

            var entCoords = _transform.GetMapCoordinates(ent, xform);
            var eyeCoords = eye.Position;

            if (!args.WorldBounds.Contains(entCoords.Position))
                continue;

            if (eye.DrawFov && !_examineSystem.InRangeUnOccluded(entCoords, eyeCoords, args.WorldBounds.Box.MaxDimension, null))
                continue;

            // Get the sprite bounds so that the bubble sits at the bottom of the sprite.
            var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);
            var bounds = _sprite.CalculateBounds((ent, sprite), worldPos, worldRot, eye.Rotation);

            var offset = (-eye.Rotation).ToWorldVec() * bounds.Box.Extents.Y;
            var offsetWorldPos = worldPos - offset;
            var pos = Vector2.Transform(offsetWorldPos, matrix);

            if (_chatUIController.ActiveSpeechBubbleNameTags.TryGetValue(ent, out var nameTag))
            {
                var nameTagPos = pos - new Vector2(nameTag.ContentSize.X / 2f, nameTag.ContentSize.Y);
                _uiManager.RenderControl(args.RenderHandle, nameTag, nameTagPos.Floored());

                pos -= new Vector2(0f, nameTag.ContentSize.Y);
            }

            foreach (var control in controls)
            {
                _layoutCache.Add(new BubbleLayout
                {
                    Bubble = control,
                    CenterX = pos.X,
                    BaselineY = pos.Y,
                    Width = control.ContentSize.X,
                    Height = control.ContentSize.Y,
                });
            }
        }

        ResolveInterleave(_layoutCache);

        _seenBubbles.Clear();
        foreach (var layout in _layoutCache)
        {
            _seenBubbles.Add(layout.Bubble);

            var target = layout.BaselineY - layout.TargetVerticalOffset;
            var achieved = offsetCache.Achieved.GetValueOrDefault(layout.Bubble, target);

            achieved = MathHelper.CloseToPercent(achieved - target, 0, 0.1)
                ? target
                : MathHelper.Lerp(achieved, target, 10 * deltaTime);
            offsetCache.Achieved[layout.Bubble] = achieved;

            var bottomY = layout.BaselineY - achieved;
            var drawPos = new Vector2(layout.CenterX - layout.Width / 2f, bottomY - layout.Height);
            _uiManager.RenderControl(args.RenderHandle, layout.Bubble, drawPos.Floored());
        }

        if (offsetCache.Achieved.Count > _seenBubbles.Count)
        {
            foreach (var key in offsetCache.Achieved.Keys.ToList())
            {
                if (!_seenBubbles.Contains(key))
                    offsetCache.Achieved.Remove(key);
            }
        }
    }

    /// <summary>
    ///     Pushes overlapping bubbles upwards so that they stack instead of being drawn on top of each other.
    /// </summary>
    private static void ResolveInterleave(List<BubbleLayout> bubbles)
    {
        // Sort by spawn time.
        bubbles.Sort((a, b) => b.Bubble.SpawnTime.CompareTo(a.Bubble.SpawnTime));

        var placedBubbles = new List<BubbleLayout>();

        foreach (var bubble in bubbles)
        {
            var left = bubble.CenterX - bubble.Width / 2f;
            var right = bubble.CenterX + bubble.Width / 2f;

            var bottom = bubble.BaselineY;

            foreach (var placedBubble in placedBubbles)
            {
                var placedLeft = placedBubble.CenterX - placedBubble.Width / 2f;
                var placedRight = placedBubble.CenterX + placedBubble.Width / 2f;

                if (placedRight <= left || placedLeft >= right)
                    continue;

                var top = bottom - bubble.Height;
                var placedTop = placedBubble.TargetVerticalOffset - placedBubble.Height;
                if (bottom <= placedTop || top >= placedBubble.TargetVerticalOffset)
                    continue;

                bottom = placedTop - VerticalMargin;
            }

            bubble.TargetVerticalOffset = bottom;

            // Find the next highest bubble.
            var idx = placedBubbles.FindIndex(q => q.TargetVerticalOffset < bottom);
            if (idx < 0)
            {
                // We're the highest.
                placedBubbles.Add(bubble);
            }
            else
            {
                // Put right below the highest.
                placedBubbles.Insert(idx, bubble);
            }
        }
    }
}
