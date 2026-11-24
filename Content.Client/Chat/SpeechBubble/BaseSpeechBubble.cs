using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.Chat.SpeechBubble;

/// <summary>
///     Base control for speech bubbles and their name tags.
/// </summary>
/// <remarks>
///     Bubbles are not part of the normal UI tree; <see cref="SpeechBubbleOverlay"/> draws them directly so that a
///     single bubble can appear in every viewport and be positioned in world space. That means
///     <see cref="Control.FrameUpdate"/> never runs for them, so the overlay drives <see cref="Update"/> instead.
/// </remarks>
[Virtual]
public partial class BaseSpeechBubble : Control
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    /// <summary>
    ///     The total time a speech bubble stays on screen.
    /// </summary>
    private static readonly TimeSpan TotalTime = TimeSpan.FromSeconds(4f);

    /// <summary>
    ///     The amount of time at the end of the bubble's life at which it starts fading out.
    /// </summary>
    private static readonly TimeSpan FadeOutTime = TimeSpan.FromSeconds(0.25f);

    /// <summary>
    ///     The amount of time at the start of the bubble's life over which it fades in.
    /// </summary>
    private static readonly TimeSpan FadeInTime = TimeSpan.FromSeconds(0.10f);

    /// <summary>
    ///     The time at which this bubble was created.
    /// </summary>
    public TimeSpan SpawnTime;

    /// <summary>
    ///     The time at which this bubble will die.
    /// </summary>
    public TimeSpan DeathTime { get; private set; }

    private readonly EntityUid _senderEntity;

    public event Action<EntityUid, BaseSpeechBubble>? OnDied;

    public Vector2 ContentSize { get; private set; }

    public BaseSpeechBubble()
    {
    }

    public BaseSpeechBubble(EntityUid senderEntity)
    {
        _senderEntity = senderEntity;
        IoCManager.InjectDependencies(this);

        SpawnTime = _timing.RealTime;
        DeathTime = _timing.RealTime + TotalTime;

        // Setup for fade in.
        Modulate = Color.White.WithAlpha(0f);
    }

    /// <summary>
    ///     Advances this bubble's fade state.
    /// </summary>
    /// <remarks>
    ///     These are drawn in an overlay, so the regular <see cref="Control.FrameUpdate"/> does not apply. The
    ///     overlay calls this once per frame instead.
    /// </remarks>
    public void Update(FrameEventArgs args)
    {
        var timeAlive = (float)(_timing.RealTime - SpawnTime).TotalSeconds;
        var timeLeft = (float)(DeathTime - _timing.RealTime).TotalSeconds;

        if (_entityManager.Deleted(_senderEntity) || timeLeft <= 0f)
        {
            // Timer spawn to prevent concurrent modification exception.
            Timer.Spawn(0, Die);
            return;
        }

        // Hide if offscreen.
        if (!_entityManager.TryGetComponent<TransformComponent>(_senderEntity, out var xform) ||
            xform.MapID != _eyeManager.CurrentEye.Position.MapId)
        {
            Modulate = Color.White.WithAlpha(0f);
            return;
        }

        if (timeAlive < FadeInTime.TotalSeconds)
        {
            Modulate = Color.White.WithAlpha(timeAlive / (float)FadeInTime.TotalSeconds);
        }
        else if (timeLeft <= FadeOutTime.TotalSeconds)
        {
            Modulate = Color.White.WithAlpha(timeLeft / (float)FadeOutTime.TotalSeconds);
        }
        else
        {
            // Make opaque otherwise, because it might have been hidden before.
            Modulate = Color.White;
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // If for some reason this isn't drawn in an overlay. This probably shouldn't happen.
        Update(args);
    }

    public void SetDeathTime(TimeSpan time)
    {
        DeathTime = time;
    }

    private void Die()
    {
        if (Disposed)
            return;

        OnDied?.Invoke(_senderEntity, this);
    }

    /// <summary>
    ///     Causes the speech bubble to start fading immediately.
    /// </summary>
    public void FadeNow()
    {
        if (DeathTime > _timing.RealTime)
            DeathTime = _timing.RealTime + FadeOutTime;
    }

    protected void SetContentSize(Vector2 size)
    {
        ContentSize = size;
    }

    /// <summary>
    ///     Prepares the control to be measured and drawn from an overlay, outside the normal UI tree.
    /// </summary>
    protected void OverlaySetup()
    {
        InvalidateStyleSheet();
        ForceRunStyleUpdate();

        InvalidateMeasure();
        InvalidateArrange();

        Measure(Vector2.PositiveInfinity);
        Arrange(UIBox2.FromDimensions(Vector2.Zero, DesiredSize));
    }
}
