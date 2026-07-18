# StressStrike — Hardware Integration Research Package

Prepared for: whoever (human or AI) is researching how the ESP32 glove hardware
should be built/wired/firmware'd to match what the Unity software already expects.

Scope confirmed with the project owner:
- **In scope:** ESP32 glove (BNO055 IMU + punch force), the MAX30102 heart-rate
  sensor (shares the glove's HID report — it is NOT a separate device), and
  Rage Room hardware (currently keyboard-simulated, real hardware not yet built).
- **Transport:** Bluetooth LE, HID-over-GATT, via the `BleGamepad`
  (ESP32-BLE-Gamepad) Arduino library — confirmed by firmware, not just Unity's matcher.
- **Firmware:** exists and is included below (`<firmware_code>`), owned by the
  project owner, editable.

---

## 1. Software Checklist

Things to pull together / verify on the Unity side before or alongside this
package, so the hardware researcher isn't missing context that lives in the
engine rather than in a script file.

- [x] **Unity `InputDevice` layout script** — `Assets/Scripts/Hardware/ESP32Glove (1).cs`
      (included below). Defines the HID vendor/product ID match and the raw
      byte-offset → axis mapping.
- [x] **Consumers of the device** — `VRGloveProcessor.cs` (Boxing-style punch
      state machine) and `KeyboardHandInput.cs` (Rage Room's abstracted hand
      input, glove-optional). Both included below.
- [x] **Biometric consumer** — `BiometricEngine.cs`, a persistent singleton
      that turns the raw `heartRate` axis into BPM/HRV/stress/calories.
      Included below.
- [x] **Firmware source** — `message.txt` (ESP32 `.ino`), included below.
- [ ] **Unity Input System package version** — `Packages/manifest.json` entry
      for `com.unity.inputsystem`. HID report parsing behavior (byte order,
      signed/unsigned handling) can differ across versions. **Not yet pulled
      — grab the exact version string.**
- [ ] **Player Input Settings** — Project Settings → Input System Package →
      whether "Supported Devices" / custom layout registration excludes or
      throttles HID devices. Not yet checked.
- [ ] **BleGamepad library version + its HID report descriptor.** This is the
      single most important missing artifact — see Q1 in the dependency list
      below. The Unity-side byte offsets (`offset = 3, 5, 7, 9, 11, 13, 15`)
      are *assumptions* about how `BleGamepad` lays out X/Y/Z/RX/RY/RZ/Slider1/
      Slider2 in the raw HID report. If the installed BleGamepad version's
      descriptor differs, every axis is silently wrong (values swapped/garbled)
      with no runtime error.
- [ ] **Editor console logs from an actual connected-glove session** — the
      code has several `[TEMP DEBUG]` `Debug.Log` lines already in place
      (`[GloveDebug]`, `[RotDbg]`, `[GloveForce]`, `[PunchDbg]`) specifically
      for diagnosing this exact hardware/software boundary. Capture one run's
      worth of these logs and attach them — they're pre-instrumented for this
      research.
- [ ] **Windows Bluetooth pairing state** — is the ESP32 paired as a native OS
      Bluetooth HID gamepad (so `InputSystem` sees it as a generic `HID`
      device), or is there a bridge/dongle involved? Unity's matcher assumes
      the OS enumerates it directly.
- [ ] **Multi-glove behavior** — `KeyboardHandInput.gloveSide` matches gloves
      by *connection order* (`InputSystem.devices` enumeration index), not by
      any hardware-reported identity. If left + right gloves exist, firmware
      needs to guarantee a stable connection order, or Unity needs a real ID
      (e.g. distinct BLE device name/MAC) to bind to.

---

## 2. Hardware Dependency List

Specific questions the software *implies answers to* but doesn't actually
confirm. Answer these from the hardware/firmware side.

### HID report layout (highest priority — likely bug source)
1. **What is BleGamepad's exact HID report byte layout for this library
   version?** Unity reads: `x`=offset 3, `y`=offset 5, `z`=offset 7,
   `forceZ`=offset 9, `w`=offset 11, `forceY`=offset 13, `heartRate`=offset 15
   (all 2-byte `SHRT`). Firmware writes, in this order: `setX`(quat.x),
   `setY`(quat.y), `setZ`(quat.z), `setRX`(quat.w), `setRY`(linAccel.y),
   `setRZ`(linAccel.z), `setSliders`(beatAvg, 0). **Confirm the byte offset
   BleGamepad actually assigns to X/Y/Z/RX/RY/RZ/Slider1/Slider2 in its HID
   report descriptor** — including whether there's a leading report-ID byte
   (offset 0) that shifts everything, and whether it matches what Unity
   assumes. This is the #1 thing to verify with an actual byte-level BLE
   sniff or the library source, because a silent mismatch produces plausible
   but wrong values (e.g. Y-axis actually landing where Unity reads Z).
2. Is Slider1/Slider2 signed or unsigned in the descriptor? Unity's `SHRT`
   format assumes a signed 16-bit range (-32768..32767) and un-normalizes by
   `× 32767`. `beatAvg` (a `byte`, 0–255) sent through `setSliders` — confirm
   it isn't being clamped/reinterpreted unexpectedly at the signed/unsigned
   boundary.

### Latency / update rate
3. What's the end-to-end latency from a physical punch (IMU spike) to the BLE
   HID report reaching Unity? The firmware loop has a `delay(10)` plus BLE
   stack latency — is round-trip fast enough for `PunchDetector`'s
   150 ms `SNAPSHOT_WINDOW` / 400 ms `COOLDOWN_MS` classification window not
   to feel laggy in-game?
4. What's the actual BLE HID polling/notification rate achieved in practice
   (not the nominal connection interval)? Sustained low rate would make the
   Unity-side edge-detection (`gloveArmed` rising-edge latch in
   `KeyboardHandInput.UpdateGlovePunch`) miss short punches entirely.

### Sensor fusion / calibration
5. BNO055 is run in `OPERATION_MODE_IMUPLUS` (fused orientation + linear
   accel, no magnetometer). Does this mode drift over a play session (no
   absolute heading reference), and if so, how much per minute? Unity's
   `RecenterOrientation()` only re-zeros a runtime offset — it doesn't
   recalibrate the sensor.
6. Is there an onboard calibration routine/gesture required before BNO055
   fusion is trustworthy (BNO055 typically needs a brief figure-8 motion to
   calibrate the magnetometer/gyro even in IMUPLUS mode)? If yes, when/how is
   the player supposed to do this — is there a hardware LED/vibration cue for
   "not yet calibrated," and does Unity need to gate gameplay until it fires?

### Processing power / power budget
7. What's the ESP32's actual loop rate under full load (BNO055 read + MAX30102
   read + BLE HID transmit + LED/vibration state machine, all in one
   `loop()` with a flat `delay(10)`)? Is there headroom, or is this already
   the bottleneck for latency (Q3)?
8. Battery life under continuous BLE + I2C polling — is this glove designed
   for a single play session (~10–20 min) or does it need to survive longer,
   and does that change the `delay(10)` / MAX30102 sample rate trade-off?

### Physical / electrical
9. Confirmed pins: `MOTOR_PIN=26`, `LED_R=19`, `LED_G=5`, `LED_B=18`, I2C on
   `Wire.begin(21, 22)` (SDA=21, SCL=22), BNO055 at I2C address `0x28`
   (fallback `0x29`). **Is this the current/final pinout**, or has the board
   revision changed since this firmware was written? Any pin conflicts with
   the BLE radio or other planned additions (e.g. Rage Room hardware sensors)?
10. Punch-force detection currently reads BNO055 **linear acceleration**
    (gravity-compensated) on X (sideways) and Y (forward) to classify
    hook vs. jab — is there a dedicated force/pressure sensor planned instead
    of/in addition to inferring force from IMU acceleration, especially for
    Rage Room where hits land on a physical object rather than air?

### Rage Room hardware (not yet built)
11. Rage Room's `HandInputProvider` abstraction (`KeyboardHandInput.cs`) is
    built to accept a second `HandInputProvider` implementation with zero
    changes to `PunchDetector`/`PhysicsHandController`/etc. **What hardware is
    actually planned for Rage Room** — the same glove reused, a different
    wearable, or physical impact sensors on destructible props (see
    `ImpactReaction.cs`, `DeformableMesh.cs`, `PunchHitbox.cs`)? This
    determines whether Rage Room needs its own `HandInputProvider` subclass
    or can reuse `KeyboardHandInput` as-is.

### Multi-device / identity
12. If left and right gloves are ever both connected, how does firmware/BLE
    expose a stable per-glove identity (device name, MAC, custom HID PID)? Unity
    currently binds by *enumeration order only* (see Software Checklist item),
    which is not guaranteed stable across reconnects.

---

## 3. Organization Template

Copy this structure for any future hardware research drop — either paste
fresh content into the empty tags, or hand the whole filled version below to
another AI/researcher as-is.

```markdown
<software_overview>
Game: StressStrike (Unity, URP). Three modes: Boxing, Rage Room, Meditate/Yoga.
Hardware-integration surface: one ESP32-based BLE wearable glove combining a
BNO055 9-axis IMU (orientation + punch force) and a MAX30102 pulse sensor
(heart rate), read into Unity via the Input System's generic HID device path.
</software_overview>

<engine_requirements>
- Unity, URP (Universal Render Pipeline) — see project CLAUDE.md.
- com.unity.inputsystem package (exact version: TBD — see Software Checklist).
- Custom InputDevice layout registered via [InputControlLayout], matched by
  HID vendorId=58626, productId=48043.
</engine_requirements>

<input_mapping>
Unity axis   | HID SHRT offset | Firmware source            | Un-normalize
-------------|------------------|------------------------------------------
x            | 3                | quat.x() * 32767 -> setX   | raw / 32767
y            | 5                | quat.y() * 32767 -> setY   | raw / 32767
z            | 7                | quat.z() * 32767 -> setZ   | raw / 32767
forceZ       | 9                | linAccel.z()*100 -> setRZ  | raw * 327.67
w            | 11               | quat.w() * 32767 -> setRX  | raw / 32767
forceY       | 13               | linAccel.y()*100 -> setRY  | raw * 327.67
heartRate    | 15               | beatAvg -> setSliders(a,0) | raw * 32767
(UNVERIFIED against actual BleGamepad HID report descriptor — see Hardware
Dependency List Q1. Byte offsets are Unity-side assumptions, not confirmed
against firmware library internals.)
</input_mapping>

<software_code filename="Assets/Scripts/Hardware/ESP32Glove (1).cs">
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
        heartRate = GetChildControl<AxisControl>("heartRate");
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
</software_code>
<explanation>
Registers a custom Unity `InputDevice` subclass that Unity's Input System
auto-binds whenever a Bluetooth device enumerates with HID vendorId 58626 /
productId 48043 — both values must be confirmed to match whatever
`BleGamepad("ESP32 VR Glove", "Custom", 100)` actually advertises (the
constructor's string args are the device/manufacturer name, not the numeric
IDs — the numeric IDs come from the BleGamepad/NimBLE library's defaults or
config, not shown in the firmware snippet above; **confirm these two numbers
against the library, they're a hard requirement for Unity to recognize the
device at all**). Each `[InputControl]` declares a 2-byte signed axis at a
fixed byte offset into the raw HID input report — this is a direct,
un-negotiated contract with whatever byte layout the firmware/BLE stack
produces. There is no schema exchange; if firmware, library version, or
report descriptor changes, these offsets go stale silently (no error, just
wrong values), which is why Hardware Dependency List Q1 is the top-priority
item in this whole package.
</explanation>

<software_code filename="Assets/Scripts/Hardware/VRGloveProcessor.cs">
using UnityEngine;
using UnityEngine.InputSystem;

public class VRGloveProcessor : MonoBehaviour
{
    [Header("Punch Detection")]
    public float punchDeadzone = 15.0f;
    public float forceToDistanceMultiplier = 0.05f;
    [Tooltip("The absolute maximum distance the virtual hand can travel in meters.")]
    public float maxPunchDistance = 1.5f;

    [Header("Punch Animation Timeline")]
    [Tooltip("Seconds it takes to reach full extension (e.g., 0.1 for a fast jab)")]
    public float timeToExtend = 0.1f;
    [Tooltip("Seconds it takes to snap back to the guard (e.g., 0.25)")]
    public float timeToRetract = 0.25f;
    [Tooltip("Cooldown before you can throw another punch")]
    public float punchCooldown = 0.1f;

    private ESP32Glove gloveDevice;
    private Quaternion manualZeroOffset = Quaternion.identity;
    private Vector3 anchorPosition;

    private enum PunchState { Idle, Extending, Retracting }
    private PunchState currentState = PunchState.Idle;

    private float currentPunchDistance = 0f;
    private float targetPunchDistance = 0f;
    private float animTimer = 0f;
    private float cooldownTimer = 0f;

    void Start() { anchorPosition = transform.localPosition; }

    void Update()
    {
        if (gloveDevice == null)
        {
            gloveDevice = InputSystem.GetDevice<ESP32Glove>();
            if (gloveDevice == null) return;
        }

        float qX = gloveDevice.x.ReadValue();
        float qY = gloveDevice.y.ReadValue();
        float qZ = gloveDevice.z.ReadValue();
        float qW = gloveDevice.w.ReadValue();

        Quaternion rawSensorRotation = new Quaternion(-qY, -qZ, qX, qW);
        rawSensorRotation = rawSensorRotation.normalized;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            manualZeroOffset = rawSensorRotation;
        }

        transform.localRotation = Quaternion.Inverse(manualZeroOffset) * rawSensorRotation;

        float fY = gloveDevice.forceY.ReadValue() * 327.67f;
        float fZ = gloveDevice.forceZ.ReadValue() * 327.67f;
        float totalPunchForce = new Vector2(fY, fZ).magnitude;

        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (currentState == PunchState.Idle && totalPunchForce > punchDeadzone && cooldownTimer <= 0f)
        {
            targetPunchDistance = Mathf.Clamp(totalPunchForce * forceToDistanceMultiplier, 0f, maxPunchDistance);
            currentState = PunchState.Extending;
            animTimer = 0f;
        }

        if (currentState == PunchState.Extending)
        {
            animTimer += Time.deltaTime;
            currentPunchDistance = Mathf.Lerp(0f, targetPunchDistance, animTimer / timeToExtend);
            if (animTimer >= timeToExtend) { currentState = PunchState.Retracting; animTimer = 0f; }
        }
        else if (currentState == PunchState.Retracting)
        {
            animTimer += Time.deltaTime;
            currentPunchDistance = Mathf.Lerp(targetPunchDistance, 0f, animTimer / timeToRetract);
            if (animTimer >= timeToRetract)
            {
                currentState = PunchState.Idle;
                currentPunchDistance = 0f;
                cooldownTimer = punchCooldown;
            }
        }

        Vector3 aimDirection = transform.localRotation * Vector3.forward;
        transform.localPosition = anchorPosition + (aimDirection * currentPunchDistance);
    }
}
</software_code>
<explanation>
Boxing mode's glove consumer. Reads raw quaternion axes and remaps them
`(x,y,z,w) -> (-y,-z,x,w)` — a fixed coordinate-system swap/negation assumed
to convert the BNO055's native axis convention into Unity's left-handed
space. This remap is a software-side guess, not something derived from a
datasheet; if the physical sensor is mounted in a different orientation on
the glove PCB, or the axis convention changes, this remap breaks silently
(rotation reads plausible but wrong). The `* 327.67f` on `forceY`/`forceZ`
exactly undoes the firmware's `linAccel * 100` scaling combined with Unity's
built-in `/32767` HID normalization — confirmed self-consistent with the
firmware in this package, but only for this specific `*100` firmware constant.
If firmware scaling changes, this multiplier must change to match.
</explanation>

<software_code filename="Assets/Scripts/RageRoom/KeyboardHandInput.cs">
[[full file already in repo — see Assets/Scripts/RageRoom/KeyboardHandInput.cs]]
</software_code>
<explanation>
Rage Room's input layer, built against an abstract `HandInputProvider` so the
glove is optional and additive rather than required — keyboard/mouse always
works, and a connected glove's rotation/punch-force is layered on top with no
manual switching. Two things a hardware researcher should know: (1) it
edge-detects the glove's raw force signal into a single instantaneous "punch"
event via `gloveForceDeadzone`/`glovePunchTrigger`/`glovePunchFull` — these
three thresholds were **tuned against this specific firmware's force scaling
and are not derived from any hardware spec**, so if firmware punch-force
scaling changes, these three numbers need re-tuning; (2) it contains several
`[TEMP DEBUG]`-tagged `Debug.Log` calls specifically instrumented for
diagnosing glove/rotation/force mismatches — capturing a play session's
console output with these active is probably the fastest way to validate or
disprove the HID offset assumptions in Q1.
</explanation>

<software_code filename="Assets/Scripts/Hardware/BiometricEngine.cs">
[[full file already in repo — see Assets/Scripts/Hardware/BiometricEngine.cs]]
</software_code>
<explanation>
Persistent singleton that turns the raw `heartRate` HID axis into BPM, a
resting-BPM calibration baseline (first `calibrationTime` seconds of valid
signal), stress delta, calorie estimate, and an SDNN-style HRV approximation.
Notably: it treats any reading `<= 20 BPM` as "sensor not reading a pulse"
(finger not on MAX30102, or glove disconnected) rather than a real
biometric zero, and drops `isConnected` to false after `signalTimeout`
seconds of that. **Firmware/hardware note:** this whole pipeline assumes the
MAX30102's `beatAvg` (already a 4-sample rolling average on the firmware
side) arrives roughly once per detected heartbeat, not at a fixed sample
rate — confirm this against the MAX30102 driver behavior in the firmware if
BPM readings look too sparse or too noisy in Unity.
</explanation>

<firmware_code filename="ESP32 glove firmware (.ino) — from Downloads/message.txt">
#include <Wire.h>
#include <BleGamepad.h>
#include <Adafruit_Sensor.h>
#include <Adafruit_BNO055.h>
#include <utility/imumaths.h>
#include "MAX30105.h"
#include "heartRate.h"

// --- Hardware Pins ---
const int MOTOR_PIN = 26;
const int LED_R = 19;
const int LED_G = 5;
const int LED_B = 18;

// --- Global Objects ---
BleGamepad bleGamepad("ESP32 VR Glove", "Custom", 100);
Adafruit_BNO055* bno;
MAX30105 particleSensor;

// --- BPM Variables ---
const byte RATE_SIZE = 4;
byte rates[RATE_SIZE];
byte rateSpot = 0;
long lastBeat = 0;
float beatsPerMinute;
int beatAvg = 0;

unsigned long lastPrintTime = 0;

// --- Connection States ---
bool wasConnected = false;
unsigned long vibrateStart = 0;
int vibrateDuration = 0;
bool vibrating = false;

// --- Punch Detection Parameters ---
const float PUNCH_TRIGGER = 15.0; // BNO055 outputs in m/s^2 (adjust if needed)
const unsigned long SNAPSHOT_WINDOW = 150;
const unsigned long COOLDOWN_MS = 400;

bool inAction = false;
unsigned long actionStart = 0;
bool inCooldown = false;
unsigned long cooldownStart = 0;

float maxSideways = 0;
float maxForward = 0;

// --- Helper Functions ---
void setColor(int r, int g, int b) {
  digitalWrite(LED_R, !r);
  digitalWrite(LED_G, !g);
  digitalWrite(LED_B, !b);
}

void startVibrate(int duration) {
  digitalWrite(MOTOR_PIN, HIGH);
  vibrateStart = millis();
  vibrateDuration = duration;
  vibrating = true;
}

void updateVibrate() {
  if (vibrating && millis() - vibrateStart > vibrateDuration) {
    digitalWrite(MOTOR_PIN, LOW);
    vibrating = false;
  }
}

void setup() {
  Serial.begin(115200);
  delay(1000);

  // 1. Setup LEDs & Motor
  pinMode(MOTOR_PIN, OUTPUT);
  digitalWrite(MOTOR_PIN, LOW);
  pinMode(LED_R, OUTPUT);
  pinMode(LED_G, OUTPUT);
  pinMode(LED_B, OUTPUT);

  // Boot State (BLUE)
  setColor(0, 0, 1);

  Wire.begin(21, 22);
  Serial.println("Scanning for BNO055...");

  // 2. Ping and Assign BNO055
  Wire.beginTransmission(0x28);
  if (Wire.endTransmission() == 0) {
    bno = new Adafruit_BNO055(55, 0x28, &Wire);
    Serial.println("BNO055 found at 0x28!");
  } else {
    Wire.beginTransmission(0x29);
    if (Wire.endTransmission() == 0) {
      bno = new Adafruit_BNO055(55, 0x29, &Wire);
      Serial.println("BNO055 found at 0x29!");
    } else {
      Serial.println("CRITICAL ERROR: No BNO055 detected.");
      while(1);
    }
  }

  // 3. Safely Boot BNO055
  if (!bno->begin(OPERATION_MODE_IMUPLUS)) {
    Serial.println("CRITICAL ERROR: BNO055 failed to boot into IMU mode!");
    while(1);
  }
  delay(100);
  bno->setExtCrystalUse(true);
  Serial.println("BNO055 IMU Mode Active!");

  // 4. Initialize MAX30102 at STANDARD Speed
  if (!particleSensor.begin(Wire, I2C_SPEED_STANDARD)) {
    Serial.println("MAX30102 failed!");
  } else {
    particleSensor.setup();
    Serial.println("MAX30102 initialized.");
  }

  bleGamepad.begin();
  Serial.println("Full Integration Active! Waiting for Bluetooth...");
}

void loop() {
  updateVibrate();

  bool isConnected = bleGamepad.isConnected();

  // --- Bluetooth Connection State Handler ---
  if (isConnected && !wasConnected) {
    setColor(0, 1, 0); // Green Idle

    // Double Vibrate Sequence
    digitalWrite(MOTOR_PIN, HIGH); delay(150);
    digitalWrite(MOTOR_PIN, LOW); delay(100);
    digitalWrite(MOTOR_PIN, HIGH); delay(150);
    digitalWrite(MOTOR_PIN, LOW);

    Serial.println("Bluetooth Connected!");
    wasConnected = true;
  } else if (!isConnected && wasConnected) {
    setColor(0, 0, 1); // Back to Blue
    wasConnected = false;
    Serial.println("Bluetooth Disconnected.");
  }

  // Only run punch logic if connected
  if (!isConnected) {
    delay(10);
    return;
  }

  // --- A. BNO055 Rotation & Force ---
  imu::Quaternion quat = bno->getQuat();
  imu::Vector<3> linAccel = bno->getVector(Adafruit_BNO055::VECTOR_LINEARACCEL);

  // --- B. MAX30102 Heart Rate ---
  long irValue = particleSensor.getIR();
  if (checkForBeat(irValue) == true) {
    long delta = millis() - lastBeat;
    lastBeat = millis();
    beatsPerMinute = 60 / (delta / 1000.0);

    if (beatsPerMinute < 255 && beatsPerMinute > 20) {
      rates[rateSpot++] = (byte)beatsPerMinute;
      rateSpot %= RATE_SIZE;

      beatAvg = 0;
      for (byte x = 0 ; x < RATE_SIZE ; x++) beatAvg += rates[x];
      beatAvg /= RATE_SIZE;
    }
  }

  // --- C. Bluetooth Transmission (To Unity) ---
  bleGamepad.setX(quat.x() * 32767);
  bleGamepad.setY(quat.y() * 32767);
  bleGamepad.setZ(quat.z() * 32767);
  bleGamepad.setRX(quat.w() * 32767);

  bleGamepad.setRY(linAccel.y() * 100);
  bleGamepad.setRZ(linAccel.z() * 100);

  bleGamepad.setSliders(beatAvg, 0);

  // --- D. Local Punch Detection (Haptics & LED) ---
  float currentSideways = abs(linAccel.x());
  float currentForward  = abs(linAccel.y());
  float totalMag = sqrt(linAccel.x()*linAccel.x() + linAccel.y()*linAccel.y() + linAccel.z()*linAccel.z());

  // Cooldown logic
  if (inCooldown) {
    if (millis() - cooldownStart > COOLDOWN_MS) {
      inCooldown = false;
      setColor(0, 1, 0); // Return to Green Idle
    }
  }
  // Trigger action window
  else if (!inAction && totalMag > PUNCH_TRIGGER) {
    inAction = true;
    actionStart = millis();
    maxSideways = 0;
    maxForward = 0;
  }

  // Action window tracking
  if (inAction) {
    if (currentSideways > maxSideways) maxSideways = currentSideways;
    if (currentForward > maxForward)   maxForward = currentForward;

    if (millis() - actionStart > SNAPSHOT_WINDOW) {

      // Determine punch type
      if (maxSideways > (maxForward * 1.20)) {
        Serial.println(">> HOOK Detected!");
        setColor(1, 0, 1); // Magenta
        startVibrate(150);
      } else {
        Serial.println(">> JAB Detected!");
        setColor(1, 0, 0); // Red
        startVibrate(75);
      }

      inAction = false;
      inCooldown = true;
      cooldownStart = millis();
    }
  }

  // --- E. Combined Monitor Output (Every 200ms) ---
  if (millis() - lastPrintTime > 200) {
    Serial.print("MAG: "); Serial.print(totalMag, 1);
    Serial.print("  |  BPM: "); Serial.println(beatAvg);
    lastPrintTime = millis();
  }

  delay(10);
}
</firmware_code>
<explanation>
This is the authoritative source of truth for everything the Hardware
Dependency List above is asking about: pin assignments, I2C addresses, BLE
axis-to-value mapping (`setX/setY/setZ/setRX/setRY/setRZ/setSliders`), punch
classification thresholds (`PUNCH_TRIGGER`, `SNAPSHOT_WINDOW`,
`COOLDOWN_MS`), and the hook-vs-jab heuristic (`maxSideways > maxForward*1.2`).
Cross-reference every Unity-side assumption above against this file line by
line — the `<input_mapping>` table was built by hand-matching these two files
and should be double-checked, not trusted blindly.
</explanation>

<open_questions>
See "Hardware Dependency List" above — copy the numbered questions relevant
to the current research task here.
</open_questions>

<unity_settings>
- Render pipeline: URP (see project CLAUDE.md — not relevant to hardware I/O
  but included for completeness/environment reproduction).
- Input System package version: TBD.
- Scenes touching hardware: Boxing (VRGloveProcessor), Rage Room
  (KeyboardHandInput), Yoga/Meditate (BiometricEngine, mode-agnostic).
</unity_settings>
```

---

### Notes on what's still missing

- Everything marked `[ ]` (unchecked) in Section 1 and every numbered item in
  Section 2 is a genuine gap — not busywork. Q1 (HID byte-offset layout) is
  the single most likely source of any "rotation/force reads plausible but
  wrong" bug reports, since it's an unverified assumption baked into
  `ESP32Glove (1).cs` with zero runtime validation.
