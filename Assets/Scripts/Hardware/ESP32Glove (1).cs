using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Controls;

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
#endif
[InputControlLayout(stateFormat = "HID ", displayName = "ESP32 VR Glove")]
public class ESP32Glove : InputDevice
{
    // The Quaternion Axes
    [InputControl(format = "SHRT", offset = 3)] public AxisControl x { get; protected set; }
    [InputControl(format = "SHRT", offset = 5)] public AxisControl y { get; protected set; }
    [InputControl(format = "SHRT", offset = 7)] public AxisControl z { get; protected set; }
    [InputControl(format = "SHRT", offset = 11)] public AxisControl w { get; protected set; }
    
    // The Punch Force Axes
    [InputControl(format = "SHRT", offset = 13)] public AxisControl forceY { get; protected set; }
    [InputControl(format = "SHRT", offset = 9)] public AxisControl forceZ { get; protected set; }

    // The Biometric Axis (Slider 1)
    [InputControl(format = "SHRT", offset = 15)] public AxisControl heartRate { get; protected set; }

    protected override void FinishSetup()
    {
        base.FinishSetup();
        x = GetChildControl<AxisControl>("x");
        y = GetChildControl<AxisControl>("y");
        z = GetChildControl<AxisControl>("z");
        w = GetChildControl<AxisControl>("w");
        forceY = GetChildControl<AxisControl>("forceY");
        forceZ = GetChildControl<AxisControl>("forceZ");
        heartRate = GetChildControl<AxisControl>("heartRate"); // Register the new biometric channel
    }

    static ESP32Glove()
    {
        InputSystem.RegisterLayout<ESP32Glove>(
            matches: new InputDeviceMatcher()
                .WithInterface("HID")
                .WithCapability("vendorId", 58626)
                .WithCapability("productId", 48043)
        );
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init() { }
}