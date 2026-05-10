using System.Collections.Generic;

namespace GoodVibes;

/// <summary>
/// Snapshot of a VibeSource activation handed to the SourceActivating hook.
/// Mutable struct; subscribers may rewrite fields before passing through.
///
/// Tags: optional freeform labels attached by the firing source. No built-in source
/// populates Tags in v1; the field is wired so future addons or sources can set tags
/// and rules can filter on them via tag_any / tag_all match conditions. Default null.
/// </summary>
public struct VibeActivation
{
    public string Identifier;
    public float Power;
    public string PowerMode;
    public float Time;
    public string TimeMode;
    public float PunctuateTime;
    public float? BasePower;
    public float? BaseTime;
    public List<string>? Tags;
}
