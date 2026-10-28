using Robust.Shared.Configuration;

namespace Content.Shared._Qippe.QVars;

public sealed partial class QVars
{
    public static readonly CVarDef<bool> ChromaticAberrationEnabled =
        CVarDef.Create("overlays.chromatic_aberration_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> ChromaticAberrationIntensity =
        CVarDef.Create("overlays.chromatic_aberration_intensity", 1.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> ChromaticAberrationThreshold =
        CVarDef.Create("overlays.chromatic_aberration_threshold", 0.8f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
