using ButtplugSong.GUI;
using UnityEngine.UIElements;

namespace ButtplugSong;

/// <summary>
/// Public API surface for addons that depend on ButtplugSong. Stable entry points; the underlying
/// internal classes (GUIManager, etc.) are not part of the public surface and may move.
/// </summary>
public static class AddonAPI
{
    /// <summary>
    /// Registers a tab on the main GUI tab strip. Call from your plugin Awake (or any time after
    /// ButtplugSong has finished loading). Pass any VisualElement; it becomes the tab's body.
    /// The tab persists for the GUIManager lifetime (DontDestroyOnLoad). Pick a distinct label
    /// per addon; collisions are not deduplicated.
    /// </summary>
    public static void RegisterTab(string label, VisualElement content)
        => GUIManager.Instance.RegisterTab(label, content);
}
