using Content.Shared._Qippe.QVars;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Qippe.Overlays;

public sealed partial class ChromaticAberrationOverlay : Overlay
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public override bool RequestScreenTexture => true;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance? _instance;
    private ProtoId<ShaderPrototype> _protoId = "ChromaticAberration";
    private bool _enabled;
    private float _intensity;
    private float _threshold;

    public ChromaticAberrationOverlay()
    {
        IoCManager.InjectDependencies(this);

        _enabled = _cfg.GetCVar(QVars.ChromaticAberrationEnabled);
        _intensity = _cfg.GetCVar(QVars.ChromaticAberrationIntensity);
        _threshold = _cfg.GetCVar(QVars.ChromaticAberrationThreshold);

        _cfg.OnValueChanged(QVars.ChromaticAberrationEnabled, value => _enabled = value);
        _cfg.OnValueChanged(QVars.ChromaticAberrationIntensity, value => _intensity = value);
        _cfg.OnValueChanged(QVars.ChromaticAberrationThreshold, value => _threshold = value);

        _instance = _prototypeManager.Index(_protoId).InstanceUnique();
    }
    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_enabled)
            return;

        if (_instance is null || ScreenTexture is null)
            return;

        var handle = args.WorldHandle;

        _instance.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _instance.SetParameter("intensity", _intensity);
        _instance.SetParameter("threshold", _threshold);

        handle.UseShader(_instance);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
