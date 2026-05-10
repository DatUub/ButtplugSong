using ButtplugSong.GUI;
using ButtplugSong.GUI.CustomUI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

    /// <summary>
    /// Returns the identifiers of the currently registered VibeSource instances, with the
    /// trailing " Source" suffix that matches the activation key (e.g. "Heal Source"). Returns
    /// an empty sequence if the GUIManager has not finished initializing.
    /// </summary>
    public static IEnumerable<string> GetRegisteredSources()
    {
        try
        {
            return GUIManager.Instance?.Sources?.Select(s => s.Identifier + " Source")
                ?? Enumerable.Empty<string>();
        }
        catch { return Enumerable.Empty<string>(); }
    }

    /// <summary>
    /// Builds a read-only WaveDisplay preview pre-populated with samples for the given wave
    /// shape, intensity, and duration. Sized 280x40 with the standard pink line and purple
    /// glow used elsewhere in the ButtplugSong UI. Sample count scales with duration, clamped
    /// to [32, 256]. Unknown wave names render as a flat line at intensity.
    /// </summary>
    public static VisualElement CreateWavePreview(string wave, float intensity, float duration)
    {
        int steps = Math.Max(32, Math.Min(256, (int)(duration * 64)));
        var display = new WaveDisplay
        {
            LineColor = new Color(1f, 184f / 255f, 246f / 255f, 0.8f),
            GlowColor = new Color(152f / 255f, 19f / 255f, 143f / 255f, 0.5f),
            LineWidth = 1f,
            GlowWidth = 1.2f,
            MinimumValue = 0f,
            MaximumValue = 1f,
            DefaultValue = 0f,
            LineBufferTop = 0.1f,
            LineBufferBottom = 0.1f,
            RecordSteps = steps,
        };
        display.style.width = 280;
        display.style.height = 40;
        display.ClearRecord();
        for (int i = 0; i < steps; i++)
        {
            float t = steps > 1 ? (float)i / (steps - 1) : 0f;
            display.PushRecordStep(SampleWave(wave, intensity, t));
        }
        return display;
    }

    private static float SampleWave(string wave, float intensity, float t) => wave switch
    {
        "SineSmall"  => intensity * (0.5f + 0.15f * MathF.Sin(t * MathF.PI * 8f)),
        "SineMedium" => intensity * (0.5f + 0.3f  * MathF.Sin(t * MathF.PI * 6f)),
        "SineLarge"  => intensity * (0.5f + 0.5f  * MathF.Sin(t * MathF.PI * 4f)),
        "Pulse"      => intensity * ((t % 0.25f < 0.125f) ? 1f : 0.1f),
        "Ramp"       => intensity * (1f - t),
        "Square"     => intensity * ((t % 0.2f < 0.1f) ? 1f : 0f),
        _            => intensity,
    };
}
