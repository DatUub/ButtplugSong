namespace GoodVibes;

/// <summary>
/// Snapshot of a VibeSource activation handed to the SourceActivating hook.
/// Mutable struct; subscribers may rewrite fields before passing through.
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
}
