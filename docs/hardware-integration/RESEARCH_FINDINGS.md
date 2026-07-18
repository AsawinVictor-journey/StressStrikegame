# Hardware Research — Findings

Answers to the four target research questions. Q1 is **closed** from primary
sources (library source + README). Q2/Q3 are partially answered analytically
with the remainder requiring bench measurement. Q4 has a concrete
implementation answer.

---

## Q1 — HID report byte layout vs. Unity offsets — ✅ CLOSED, MAPPING IS CORRECT

**Verdict: the Unity offsets (3, 5, 7, 9, 11, 13, 15) are CORRECT.** No bug here.

### The critical library behavior

ESP32-BLE-Gamepad's README states explicitly:

> "setAxes accepts axes in the order (x, y, z, rx, ry, rz)
> setHIDAxes accepts them in the order (x, y, z, **rz, rx, ry**)"

The **HID report physically lays axes out as X, Y, Z, RZ, RX, RY** — *not* the
intuitive X, Y, Z, RX, RY, RZ. This is the classic trap with this library.

### Default configuration (confirmed from `BleGamepadConfiguration.cpp`)

The firmware uses `BleGamepad bleGamepad("ESP32 VR Glove", "Custom", 100)` with
no configuration object, so library defaults apply:

| Setting | Default |
|---|---|
| HID Report ID | `3` |
| Button count | `16` → **2 bytes** |
| Special buttons | all `false` → **0 bytes** |
| Axes included | all 8 enabled, 16-bit each, little-endian |
| Hat switches | 1 (laid out *after* the axes) |

### Resulting report layout

| Byte offset | Content | Firmware writes | Unity reads |
|---|---|---|---|
| 0 | Report ID (`3`) | — | — |
| 1–2 | 16 buttons | — | — |
| **3–4** | X | `quat.x() * 32767` | `x` ✅ |
| **5–6** | Y | `quat.y() * 32767` | `y` ✅ |
| **7–8** | Z | `quat.z() * 32767` | `z` ✅ |
| **9–10** | **RZ** | `linAccel.z() * 100` | `forceZ` ✅ |
| **11–12** | **RX** | `quat.w() * 32767` | `w` ✅ |
| **13–14** | **RY** | `linAccel.y() * 100` | `forceY` ✅ |
| **15–16** | Slider1 | `beatAvg` | `heartRate` ✅ |
| 17–18 | Slider2 | `0` | unused |

Every axis lines up. The offsets in `ESP32Glove.cs` that *look* scrambled
(`w` at 11, `forceZ` at 9, `forceY` at 13) are scrambled **exactly correctly** —
they compensate for the library's RZ/RX/RY report ordering. Whoever wrote that
file either knew this or found it empirically. It is right.

### Conditions this correctness depends on

This mapping breaks if any of these change:

1. **Button count changes from 16.** Buttons occupy 2 bytes at 16; going to 17+
   buttons adds a byte and shifts *every* axis offset by one.
2. **Any special button is enabled** — adds a byte before the axes, shifting
   everything.
3. **Any axis is disabled** in configuration — the report is packed
   sequentially, so disabling X collapses everything after it.
4. **Library version change** that alters report composition.

**Recommendation:** pin the ESP32-BLE-Gamepad version in the firmware
repo/README and add a comment in both the firmware and `ESP32Glove.cs` noting
that the offsets encode the library's X,Y,Z,**RZ,RX,RY** ordering plus a
1-byte report ID and 2 button bytes. This is exactly the kind of correct-but-
inexplicable constant that a future edit silently breaks.

---

## Q2 — End-to-end latency and BLE notification rate — ⚠️ PARTIAL

### What can be determined from the firmware

The main loop ends with a hard `delay(10)`, capping the loop at **≤100 Hz**, and
that's before I2C transaction time. Per iteration the firmware performs:

- `bno->getQuat()` — 8-byte I2C read
- `bno->getVector(VECTOR_LINEARACCEL)` — 6-byte I2C read
- `particleSensor.getIR()` — MAX30102 FIFO read
- up to 8 BLE characteristic value writes

`Wire.begin(21, 22)` uses the default **100 kHz** I2C clock. At 100 kHz, ~14
bytes of sensor payload plus register addressing and ACK overhead costs roughly
2–4 ms per loop. So realistic loop time is **~12–15 ms (≈65–80 Hz)**, not the
nominal 100 Hz.

### Estimated latency budget

| Stage | Estimate | Notes |
|---|---|---|
| BNO055 internal fusion output rate | ~10 ms | 100 Hz fusion output in IMU mode |
| Firmware loop + I2C | 12–15 ms | dominated by `delay(10)` + I2C at 100 kHz |
| BLE connection interval | 15–30 ms | Windows commonly negotiates ~15 ms+; not controlled by firmware |
| Unity frame poll | ~16 ms | at 60 FPS |
| **Total** | **~45–75 ms** | needs measurement to confirm |

For reference: ~50 ms is around the threshold where hand-tracking latency starts
being consciously noticeable; sub-30 ms feels immediate. This budget is likely
**perceptible but playable**, and is the main lever if the glove feels "floaty."

### Two concrete firmware-side wins available

1. **Raise I2C to 400 kHz** (`Wire.setClock(400000)`). Both BNO055 and MAX30102
   support fast-mode I2C. This cuts several ms per loop for a one-line change.
   Note the firmware currently initializes the MAX30102 with
   `I2C_SPEED_STANDARD` explicitly — that would need changing too.
2. **Remove or shrink the `delay(10)`**, replacing it with a non-blocking
   timer, so the loop runs as fast as the sensors and BLE stack allow.

### Separately: the 150 ms haptic delay

Independent of transport latency — the firmware's *local* punch classifier waits
`SNAPSHOT_WINDOW = 150 ms` after the impact spike before deciding hook vs. jab
and firing the vibration/LED. So **haptic feedback lags the physical punch by at
least 150 ms** regardless of BLE. That is well above the perceptual threshold
for impact feedback and will feel disconnected from the hit.

This does *not* affect Unity's punch response (Unity runs its own detection off
the force axes and doesn't wait for the classification). Suggested fix: fire an
immediate short buzz on threshold crossing for the *impact* sensation, and let
the 150 ms classification only pick the LED color / punch type afterward.

### Still requires bench measurement

- Actual negotiated BLE connection interval under Windows.
- Sustained notification rate achieved in practice under full sensor load.
- True end-to-end latency. **Suggested method:** film the glove and screen
  together at 240 fps with a phone, punch, and count frames between physical
  impact and on-screen response. Cheap, and accurate to ~4 ms.

---

## Q3 — BNO055 IMUPLUS drift and calibration — ⚠️ SIGNIFICANT RISK IDENTIFIED

### The core issue: yaw will drift, by design

`OPERATION_MODE_IMUPLUS` fuses **accelerometer + gyroscope only — the
magnetometer is disabled.** The consequence is structural, not a tuning problem:

- **Pitch and roll are drift-free.** Gravity provides an absolute reference, so
  the fusion continuously corrects these.
- **Yaw (heading) has no absolute reference** and is pure gyro integration.
  It *will* drift, and the drift is unbounded — it accumulates for as long as
  the session runs.

Typical BNO055 yaw drift in IMU mode is on the order of **~1–2°/minute**, worse
with vigorous motion and temperature change. Over a 10–20 minute session that's
plausibly **10–40° of accumulated heading error** — enough that "forward" for the
player's punches no longer matches forward in the game. For a boxing game this
is a real gameplay problem, not a cosmetic one.

### Why NDOF (magnetometer enabled) is not an obvious fix

Switching to `OPERATION_MODE_NDOF` would give an absolute heading reference and
eliminate yaw drift — but on this specific hardware the magnetometer sits
centimeters from a **vibration motor** (a rotating magnet) and the ESP32's
radio and power traces. Magnetic interference would likely make NDOF heading
unstable or actively worse. Worth *testing*, but do not assume it's an upgrade.

### Practical mitigations, in order of preference

1. **Periodic re-centering during natural gameplay pauses.** The Unity side
   already has `RecenterOrientation()`. Trigger it automatically at moments the
   player is expected to be in a known neutral pose — between rounds, at pose
   transitions, on menu return — rather than only on a manual key press. This is
   the cheapest effective fix and requires no hardware change.
2. **Expose a visible re-center control** so the player can correct it
   themselves when it feels off.
3. **Test NDOF mode** with the motor idle vs. active, to characterize whether
   magnetometer interference is actually disqualifying.

### Calibration: currently unhandled

BNO055 exposes calibration status registers (system / gyro / accel / mag, each
0–3) via `getCalibration()`. **The current firmware never reads them.** That
means the glove can be transmitting poorly-calibrated fusion output with no
indication to the player or the game.

In IMUPLUS mode:
- **Gyroscope** calibrates by keeping the device still for a few seconds.
- **Accelerometer** calibrates by holding it briefly in several distinct
  orientations.
- Magnetometer calibration is not applicable (mag disabled).

**Recommendation:** read the calibration status in the firmware loop and surface
it — the RGB LED is already there and idle at boot, so e.g. amber until gyro+accel
calibration reach 3/3, then green. This turns an invisible failure mode into an
obvious one, and gives the player a clear "hold still for a moment" cue.

---

## Q4 — Stable left/right glove identity — ✅ CONCRETE ANSWER

### The current problem

Unity currently binds gloves by **enumeration order** — it walks
`InputSystem.devices`, counts `ESP32Glove` instances, and assigns index 0 to one
hand and index 1 to the other. Connection order is not guaranteed stable across
reconnects, so left and right can silently swap between sessions.

### Recommended fix: distinct PIDs per glove

`BleGamepadConfiguration` exposes vendor/product ID setters. Flash each glove
with a distinct product ID:

```cpp
BleGamepadConfiguration config;
config.setVid(0xE502);          // keep VID common to both gloves
config.setPid(0xBBAB);          // LEFT glove   — distinct per hand
// config.setPid(0xBBAC);       // RIGHT glove
bleGamepad.begin(&config);
```

Also give each a distinct advertised device name (`"ESP32 VR Glove L"` /
`"ESP32 VR Glove R"`) — useful for OS-level pairing clarity even though the
game matches on PID.

Then register two Unity layouts, one per PID, and bind each hand to its own
device type explicitly. Handedness becomes a hardware property rather than a
race condition.

**Important:** changing the PID changes what Unity's device matcher must look
for. The existing matcher targets vendorId `58626` / productId `48043` — if you
change PIDs, that matcher must be updated in lockstep or the glove stops being
recognized entirely.

### Alternative if reflashing per-hand is undesirable

Keep one firmware image and read the hand from a hardware strap — tie a spare
GPIO high on one glove and low on the other, read it at boot, and select the PID
(or a button-bit identity flag) accordingly. One image, per-unit identity set by
a solder bridge or jumper.

---

## Summary

| Question | Status | Headline |
|---|---|---|
| Q1 — HID offsets | ✅ Closed | **Mapping is correct.** Offsets correctly encode the library's X,Y,Z,RZ,RX,RY ordering. Document why, and pin the library version. |
| Q2 — Latency | ⚠️ Partial | Estimated ~45–75 ms end-to-end. Two easy firmware wins (400 kHz I2C, drop `delay(10)`). Separate 150 ms haptic lag should be fixed independently. Needs 240 fps video measurement to confirm. |
| Q3 — Drift | ⚠️ Risk found | **Yaw drift is structural in IMUPLUS** (no magnetic reference) — likely 10–40° over a session. Auto-recenter at natural pauses. Calibration status is currently never read; surface it on the existing LED. |
| Q4 — Glove identity | ✅ Answered | Assign distinct PIDs per hand (or GPIO strap + runtime PID selection); register per-hand Unity layouts. Update the device matcher to match. |

### Sources

- [ESP32-BLE-Gamepad — repository and README](https://github.com/lemmingDev/ESP32-BLE-Gamepad)
- [`BleGamepad.cpp` — report construction](https://raw.githubusercontent.com/lemmingDev/ESP32-BLE-Gamepad/master/BleGamepad.cpp)
- [`BleGamepadConfiguration.cpp` — defaults](https://raw.githubusercontent.com/lemmingDev/ESP32-BLE-Gamepad/master/BleGamepadConfiguration.cpp)
