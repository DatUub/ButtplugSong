using ButtplugSong.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ButtplugSong.Network;

public class DeviceInfo
{
    internal readonly ButtplugDevice Device;
    private readonly Action<string> Log;

    public string Name => Device.Name;
    public uint Id => Device.Index;
    public double? Battery { get; private set; }
    public bool IsEnabled { get; set; } = true;

    //specific feature settings;
    public bool AlternateRotation { get; internal set; } = true;
    public bool RotateClockwise { get; set; } = true;
    public float NeutralTemperature { get; set; } = 0f;
    public float MoveDuration { get; set; } = 1.5f;

    public Dictionary<FeatureType, DeviceFeature> Features { get; } = new Dictionary<FeatureType, DeviceFeature>();

    private float _testTimeRemaining;
    private bool _isTesting;

    public DeviceInfo(ButtplugDevice device, Action<string> log)
    {
        Device = device;
        Log = log;
        PopulateFeatures();

        PlugManager.UpdateDevicePower += ActivateFeatures;
    }

    private void PopulateFeatures()
    {
        // Collect device-reported features
        var reported = new Dictionary<FeatureType, uint>();
        foreach (var rawFeature in Device.Features)
        {
            FeatureType type = ActuatorTypeToFeatureType(rawFeature.ActuatorType);
            if (!reported.ContainsKey(type))
                reported[type] = (uint)(rawFeature.StepCount ?? 0);
        }

        // Create entries for ALL feature types, marking unsupported ones
        foreach (FeatureType type in Enum.GetValues(typeof(FeatureType)))
        {
            bool supported = reported.TryGetValue(type, out uint stepCount);
            Features[type] = new DeviceFeature(this, type, supported, supported ? stepCount : null);
        }
    }

    private static FeatureType ActuatorTypeToFeatureType(string actuatorType)
    {
        return actuatorType switch
        {
            "Vibrate" => FeatureType.Vibrate,
            "Rotate" => FeatureType.Rotate,
            "Oscillate" => FeatureType.Oscillate,
            "Constrict" => FeatureType.Constrict,
            "Spray" => FeatureType.Spray,
            "Position" => FeatureType.Position,
            _ => FeatureType.Vibrate,
        };
    }

    private static string FeatureTypeToActuatorType(FeatureType type)
    {
        return type switch
        {
            FeatureType.Vibrate => "Vibrate",
            FeatureType.Rotate => "Rotate",
            FeatureType.Oscillate => "Oscillate",
            FeatureType.Constrict => "Constrict",
            FeatureType.Spray => "Spray",
            FeatureType.Position => "Position",
            _ => null,
        };
    }

    /// <summary>
    /// Returns the actuator indices this device exposes for the given feature type. A single-motor
    /// device with one vibrate actuator returns [0]; a Lovense Edge 2 returns [0, 1] for Vibrate.
    /// Devices that do not expose the feature return an empty list. Indices come straight from the
    /// raw protocol descriptors and match the Buttplug v3 ScalarCmd / RotateCmd / LinearCmd Index
    /// field, so addons can target a specific motor.
    /// </summary>
    public IReadOnlyList<int> GetActuatorIndices(FeatureType feature)
    {
        string actuatorType = FeatureTypeToActuatorType(feature);
        if (actuatorType == null) return Array.Empty<int>();
        return Device.Features
            .Where(f => f.ActuatorType == actuatorType)
            .Select(f => f.ActuatorIndex)
            .OrderBy(i => i)
            .ToArray();
    }

    public async Task<bool> TryRefreshBattery()
    {
        try
        {
            double? level = await Device.ReadBatteryAsync();
            if (level.HasValue)
            {
                Battery = level.Value;
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void Test(float power, float duration)
    {
        if (!IsEnabled) return;
        _testTimeRemaining = duration;
        _isTesting = true;
        ActivateFeatures(Math.Max(0f, Math.Min(1f, power)), false);
    }

    public void Update(float deltaTime)
    {
        if (!_isTesting) return;
        _testTimeRemaining -= deltaTime;
        if (_testTimeRemaining > 0f) return;

        _isTesting = false;
        _testTimeRemaining = 0f;
        Device.SendStopCmd();
    }

    private void ActivateFeatures(float power, bool routineUpdate)
    {
        if (!IsEnabled) return;
        foreach (var featureType in DeviceFeature.Implemented)
        {
            if (!Features.TryGetValue(featureType, out DeviceFeature feature)) continue;
            if (!feature.IsSupported || !feature.IsEnabled) continue;

            var indices = GetActuatorIndices(featureType);
            if (indices.Count == 0)
            {
                // Feature is advertised but the raw protocol layer has no actuator entries.
                // Fall back to a single broadcast at index 0 so the legacy single-arg path still
                // runs for hand-rolled tests that bypass the protocol parser.
                float p = PlugManager.RaiseDevicePowerComputing(Device.Name, feature.Type, 0, power);
                DispatchSingle(feature.Type, p, broadcast: true);
                continue;
            }

            // Per-actuator dispatch: fires DevicePowerComputing once per (device, feature, index)
            // so addons can route base vs tip on multi-motor devices like the Lovense Edge 2.
            bool advanceRotation = AlternateRotation;
            foreach (int actuatorIndex in indices)
            {
                float p = PlugManager.RaiseDevicePowerComputing(Device.Name, feature.Type, actuatorIndex, power);

                switch (feature.Type)
                {
                    case FeatureType.Vibrate:
                        Device.SendVibrateCmd(actuatorIndex, p);
                        break;
                    case FeatureType.Rotate:
                        if (advanceRotation)
                        {
                            RotateClockwise = !RotateClockwise;
                            advanceRotation = false;
                        }
                        Device.SendRotateCmd(p, RotateClockwise, actuatorIndex);
                        break;
                    case FeatureType.Position:
                        uint durationMs = (uint)(MoveDuration * 1000f);
                        Device.SendLinearCmd(durationMs, p, actuatorIndex);
                        break;
                        //other features not implemented yet - see DeviceFeature.Implemented
                }
            }
        }
    }

    private void DispatchSingle(FeatureType type, float p, bool broadcast)
    {
        switch (type)
        {
            case FeatureType.Vibrate:
                Device.SendVibrateCmd(p);
                break;
            case FeatureType.Rotate:
                if (AlternateRotation) RotateClockwise = !RotateClockwise;
                Device.SendRotateCmd(p, RotateClockwise);
                break;
            case FeatureType.Position:
                uint durationMs = (uint)(MoveDuration * 1000f);
                Device.SendLinearCmd(durationMs, p);
                break;
        }
    }

    public void Unload()
    {
        PlugManager.UpdateDevicePower -= ActivateFeatures;
    }
}