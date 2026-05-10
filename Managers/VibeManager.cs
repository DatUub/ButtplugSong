using ButtplugSong;
using ButtplugSong.GUI;
using ButtplugSong.GUI.Network;
using ButtplugSong.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GoodVibes;

public class VibeManager
{
    private static VibeManager? _instance;
    public static VibeManager Instance
    {
        get
        {
            if (_instance == null) throw new NotImplementedException("Instantiate properly before referencing! >:(");
            return _instance;
        }
        private set => _instance = value;
    }

    public event Action? PlugReconnectEstablished;

    /// <summary>
    /// Fires before each VibeSource activation lands. Subscribers can transform the activation
    /// or return false to swallow it entirely. Exceptions in subscribers are caught and logged.
    /// Intended as the addon-facing hook for filter / route / replace.
    /// </summary>
    public static event Func<VibeActivation, bool>? SourceActivating;

    internal static bool RaiseSourceActivating(in VibeActivation a)
    {
        if (SourceActivating == null) return true;
        var log = BepInEx.Logging.Logger.CreateLogSource("ButtplugSong.Hooks");
        foreach (Func<VibeActivation, bool> handler in SourceActivating.GetInvocationList())
        {
            try { if (!handler(a)) return false; }
            catch (Exception ex) { log.LogWarning($"SourceActivating subscriber threw: {ex.Message}"); }
        }
        return true;
    }

    internal GUIManager UI;
    internal VibeLogic Logic;
    internal PlugManager plug;

    public float PlugUpdateFrequency = 0.125f; // 1/8th of a second
    private float timeSinceLastPlugUpdate = 0;

    public bool HasDevice => GetDevices().Any();
    public event Action<float, float>? NeedsUpdate;
    public event Action<string>? LogMessage;

    public VibeManager(string modPath, Action<string>? logger = null)
    {
        Instance = this;

        if (logger != null) LogMessage += logger;
        Logic = new VibeLogic(Log);
        UI = GUIManager.CreateAndInitialize();

        Logic.PowerChanged += TargetPowerChanged;
        Logic.PunctuateChanged += PunctuateChanged;
        NeedsUpdate += Logic.DecreaseTimer;
        ModHooks.OnFinishedLoadingModsHook += FinishedLoading;

        ReconnectPlug();
    }

    private void FinishedLoading()
    {
        VibeLogic.Armed = true;
        UI.FinishedLoading();
    }

    public void Update(float realTime, float timerTime)
    {
        NeedsUpdate?.Invoke(realTime, timerTime);
        timeSinceLastPlugUpdate += realTime;
        if (timeSinceLastPlugUpdate > NetworkSettings.UpdateFrequency) ForcePlugUpdate(true);
    }
    public void TargetPowerChanged() => ForcePlugUpdate(false);
    public void PunctuateChanged(bool _) => ForcePlugUpdate(false);
    public void ForcePlugUpdate(bool routineUpdate)
    {
        timeSinceLastPlugUpdate = 0;
        plug.SetPowerLevel(Logic.ActualPower, routineUpdate);
    }

    internal IEnumerable<ButtplugDevice> GetDevices() => plug.GetDevices();
    internal void ReconnectPlug()
    {
        DisconnectPlug();
        plug = new(Log, NetworkSettings.ServerAddress, NetworkSettings.Port, NetworkSettings.RetryAttempts);
        PlugReconnectEstablished?.Invoke();

    }
    internal void DisconnectPlug() => plug?.ShutDown();
    internal void Log(string message) => LogAsync(message, 5);
    internal async void LogAsync(string message, int attempts)
    {

        message = $"[@{DateTime.UtcNow.Minute:D2}:{DateTime.UtcNow.Second:D2}] {message}";
        for (int logAttempts = 0; logAttempts < attempts; logAttempts++)
        {
            try
            {
                LogMessage?.Invoke(message);
                return;
            }
            catch (IOException) //Catch for if logging is writing to a file but something else is holding priority.
            {
                await Task.Delay(50);
            }
        }
    }
}
