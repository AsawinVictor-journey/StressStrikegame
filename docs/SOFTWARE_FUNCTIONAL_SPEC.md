# StressStrike — Software Functional Specification

How the software actually works, function by function. Derived by reading the
source, not from design intent — where the code contradicts the design docs,
this file follows the code.

---

## 0. Signal chain overview

```
PHYSICAL           ESP32 firmware          Unity Input           Game logic
────────           ──────────────          ───────────           ──────────
hand motion   ──►  BNO055 fusion      ──►  ESP32Glove       ──►  HandInputProvider
                   quat + linAccel         (HID axes)             │
                                                                  ├─► PunchDetector ──► PunchController ──► HandTarget
                                                                  ├─► HandRotation
                                                                  └─► YogaTracker
pulse         ──►  MAX30102 beatAvg  ──►  ESP32Glove.heartRate ──► BiometricEngine ──► HeartRateYogaFlowManager
```

Two rules the architecture enforces everywhere:

1. **The IMU never reports position.** `HandInputProvider` exposes acceleration
   and orientation only. Nothing is allowed to double-integrate acceleration
   into a position estimate (dead-reckoning drift trap). The only "position"
   in the game is a bounded simulation in `HandTarget`.
2. **Hardware is optional.** Every glove consumer falls back to keyboard/mouse,
   so the entire game is playable with no hardware connected.

---

## 1. Input layer

### 1.1 `ESP32Glove : InputDevice`

Registers a custom Unity Input System device matched on HID vendorId 58626 /
productId 48043.

| Member | Type | Meaning |
|---|---|---|
| `x`, `y`, `z`, `w` | `AxisControl` | BNO055 fused quaternion, normalized to ±1 |
| `forceY`, `forceZ` | `AxisControl` | Linear acceleration (gravity removed), ×100 on the wire |
| `heartRate` | `AxisControl` | Averaged BPM from MAX30102 |

`static ESP32Glove()` — registers the layout at editor load.
`FinishSetup()` — binds the child controls after Unity constructs the device.

**Un-normalization convention:** Unity crushes 16-bit HID axes to ±1.
Quaternion axes are used as-is; force axes are restored with `× 327.67`;
heart rate is restored with `× 32767`.

### 1.2 `HandInputProvider` (abstract)

The seam that makes hardware optional.

| Method | Returns | Contract |
|---|---|---|
| `GetAcceleration()` | `Vector3` | Motion intent in hand-local axes, m/s². Sustained input holds near a constant magnitude; a punch is a short spike far above it. |
| `GetOrientation()` | `Quaternion` | Fused orientation. Default identity. |
| `ProvidesOrientation` | `bool` | Whether `GetOrientation()` is a real signal. Consumers use this instead of guessing from the returned value. |

### 1.3 `KeyboardHandInput : HandInputProvider`

Despite the name this is the **production input path** — keyboard simulator with
the glove layered on top when present. Both sources stay live simultaneously.

| Method | When | What it does |
|---|---|---|
| `Update()` | per frame | Polls `punchKey` down/up. On release computes charge `t = held / chargeMaxTime`, sets `currentSpikeAccel = Lerp(minPunchAccel, maxPunchAccel, t)` and opens `punchTimer` for `punchSpikeDuration`. Also handles `recenterKey`. |
| `FixedUpdate()` | per physics step | Decrements `punchTimer`, then calls `UpdateGlovePunch()`. |
| `UpdateGlovePunch()` | per physics step | **Edge-detects** glove force into the same transient spike a mouse punch produces. |
| `GetAcceleration()` | polled | Keyboard directional accel + `Vector3.forward * currentSpikeAccel` while `punchTimer > 0`. |
| `GetOrientation()` | polled | `Inverse(zeroOffset) * rawGloveQuat`, with NaN/zero-packet guard. |
| `TryAcquireDevice()` | on enable / device change | Binds by **enumeration index** (`gloveSide`) — see known issue below. |

**Why glove force is edge-detected rather than passed through:** the raw force
reading is a slow analog signal that can sit above threshold for many frames.
Fed continuously it (a) jams `PunchDetector`'s re-arm latch and (b) pins the
hand at its forward bound. So `UpdateGlovePunch()` fires **one** spike per
upward crossing of `glovePunchTrigger`, then requires the force to fall back
below `gloveForceDeadzone` before re-arming (`gloveArmed` latch). Downstream, a
glove punch is indistinguishable from a mouse punch.

**Known issue:** `TryAcquireDevice()` matches gloves by connection order, not by
hardware identity. With two gloves, left/right can swap between sessions.

---

## 2. Punch pipeline (Rage Room)

Four decoupled modules. Each knows nothing about the others' internals.

### 2.1 `PunchDetector`

Classifies the acceleration signal into discrete punch events. Knows nothing
about hitboxes or movement.

```
FixedUpdate():
    mag = input.GetAcceleration().magnitude
    if mag < punchThreshold:  armed = true;  return      // re-arm
    if !armed || cooldownTimer > 0: return
    armed = false;  cooldownTimer = cooldown
    OnPunch?.Invoke(Clamp01(mag / fullStrengthAccel))
```

The re-arm rule is the important part: a spike that stays above threshold for
several frames fires **exactly one** event, not one per frame.

Defaults: `punchThreshold = 60`, `fullStrengthAccel = 150`, `cooldown = 0.25s`.
Constraint: `punchThreshold` must sit below the provider's weakest spike
(`minPunchAccel = 70`) with margin above resting noise.

### 2.2 `PunchController`

Subscribes to `OnPunch` and produces three effects.

| Handler | Effect |
|---|---|
| `HandlePunch(strength)` | Saves `prePunchLocalPos`, calls `handTarget.BeginPunch(strength)`, enables the hitbox collider, opens the window (`hitboxDisableAt = now + hitboxDuration`). |
| `HandleHit(collision)` | On contact: closes the window immediately and calls `BeginRetract(prePunchLocalPos)`. |
| `Update()` | On timeout with no contact: closes the window and retracts anyway. |

Invariant: **a thrown punch always retracts** — on hit, or on timeout. The
hitbox is a separate collider enabled only during the window, so incidental
brushing from the hand's persistent collider never registers as a strike.

### 2.3 `HandTarget` — the kinematic anchor

Owns a bounded workspace simulation. It does *not* track real hand position and
cannot — an IMU can't measure position. It integrates motion intent into a
velocity and walks a virtual anchor inside a box.

`FixedUpdate()` is a three-way state machine:

| State | Behavior |
|---|---|
| `retracting` | Position-driven `Lerp(retractFrom, retractTo, t)` over `retractDuration` (0.12s). Input ignored. |
| `extending` | Position-driven `Lerp(extendFrom, extendTo, t)`. Holds at full extension until `BeginRetract`. |
| free | `accel = input.GetAcceleration()` → optional recovery spring → `velocity.Step(accel, dt)` → `localPos += v*dt` → clamp to bounds → **zero the velocity on any axis that clamped**. |

The clamp-then-zero step matters: without it, stored momentum snaps the anchor
off a wall the instant input stops, and the hand jerks away from whatever it was
pressing against. It's also the signal `RageRoomCameraRotation` reads to detect
"player is still pushing past the edge."

| Public method | Contract |
|---|---|
| `BeginPunch(strength)` | Lunges forward `punchDistance` (0.5 m) **always** — charge scales *speed* (`Lerp(minPunchSpeed, punchSpeed, strength)`), not reach. Duration = distance / speed. |
| `BeginRetract(targetLocalPos)` | Snap-back on a fixed timer, zeroes the integrator so leftover velocity can't resume carrying the hand forward. |
| `AddImpulse(kick)` | General-purpose velocity kick (e.g. external knockback). |

Design note: charge scaling speed rather than distance means every punch has
identical reach, and a charged punch simply arrives faster — which makes it land
with higher impact velocity, so damage/force scaling downstream happens for free.

Hand-to-hand separation runs at the end via `Physics.ComputePenetration`, pushing
apart by at most `maxPushDist` (0.15 m).

### 2.4 `ImuVelocityIntegrator` (plain class, not a MonoBehaviour)

The whole acceleration→velocity layer, isolated and testable.

```
Step(acceleration, dt):
    1. deadzone   — magnitude < deadzone (0.5) → zero      // noise can't accumulate
    2. smoothing  — Lerp(filteredAccel, raw, smoothing)    // 1 = no smoothing
    3. integrate  — v = Velocity + filteredAccel * dt
    4. damping    — v *= dampingFactor (0.9)               // bias bleeds off every step
    5. clamp      — ClampMagnitude(v, maxSpeed = 8)
```

Step 4 is the anti-drift mechanism: damping applied every step independent of
acceleration means sensor bias bleeds off instead of accumulating. This is what
prevents the classic undamped dead-reckoning failure.

### 2.5 `PhysicsHandController` — the physical hand

**Has no `FixedUpdate`.** It builds a `ConfigurableJoint` in `Start()` and lets
PhysX do everything.

Why: script-driven force control runs in FixedUpdate, which executes *before*
PhysX's constraint solver. The controller acts, then the solver reacts a step
later — so the controller can always undo the solver's contact impulse, object
mass stops mattering, and the hand "fights physics." A `ConfigurableJoint` is a
constraint, solved *simultaneously* with contacts in the same iteration.

Result: when contact resistance exceeds the joint's `maximumForce`, the solver
gives force to the contact and the hand stalls naturally — no code involved.

| Setting | Value | Role |
|---|---|---|
| `positionSpring` | 3000 N/m | Free-air tracking stiffness |
| `positionDamper` | 110 N·s/m | ≈ critical damping at mass 1 kg |
| `maximumForce` | 250 N | The hand's push budget — the single resistance parameter |
| Z drive | spring ×8, damper ×3, force ×8 | Stiffer forward so objects can't push the hand back |
| Angular motion | all `Locked` | `HandRotation` owns orientation |
| `projectionMode` | `None` | Never teleport — the hand/anchor gap under load *is* the resistance feedback |

---

## 3. Brief-COPE survey and mode routing

### 3.1 `GameModeRecommendation.Recommend(answers)` → `ModeRecommendation`

Pure function. No LLM, no network, fully deterministic.

```
1. subscaleScores = BriefCopeData.ScoreSubscales(answers)     // 28 answers → 14 subscales
2. bucketScores   = BriefCopeData.ScoreBuckets(subscaleScores) // 14 → 3 buckets
3. topBucket = argmax over FIXED order [Approach, Avoidant, Context]
4. route:
     Avoidant  → Meditate
     Approach  → Boxing
     Context   → Religion ? Meditate : RageRoom
5. reason = ReasonBySubscale[ TopSubscaleInBucket(scores, topBucket) ]
```

Two subtleties that are easy to break:

- **Step 3 iterates a fixed array**, not dictionary order, so an exact tie
  resolves deterministically as Approach > Avoidant > Context.
- **Step 5 uses the top subscale *within the winning bucket*,** not the global
  top subscale. These can diverge, and conflating them produces a reason line
  that contradicts the recommended mode.

### 3.2 Output contract

| Field | Content |
|---|---|
| `mode` | `GameMode` enum |
| `modeName` | Display name — note `Meditate` displays as **"Yoga"** |
| `coachMessage` | Fixed per-mode copy |
| `reason` | Plain-language line for the winning subscale |

Scene routing: `SceneNames` maps `Boxing → "BoxingMenu"`, `RageRoom → "Rage Room"`,
`Meditate → "meditation"`, consumed by `SceneTransitionManager.LoadScene(string)`.

**Safety wording:** sensitive subscales (SubstanceUse, SelfBlame, Denial,
BehavioralDisengagement) are phrased "your answers leaned toward…", never as a
diagnosis, and a `Disclaimer` constant is shown alongside every result.

---

## 4. Biometrics

### 4.1 `BiometricEngine` (persistent singleton)

Bootstraps itself via `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` onto a
`DontDestroyOnLoad` GameObject, so a calibration performed in the menu remains
valid inside every mode. No scene wiring required.

`Update()` sequence:

```
1. acquire/refresh device   — re-fetch if null OR !added (a removed device throws on read)
2. currentBPM = heartRate.ReadValue() * 32767
3. if currentBPM <= 20:  treat as "no pulse", drop isConnected after signalTimeout; return
4. SampleForWindow()        — append to window every sampleInterval, recompute HRV
5. if calibrating:          accumulate; after calibrationTime lock restingBPM; return
6. CalculateStress()        — stressDelta = max(0, currentBPM - restingBPM)
7. CalculateCalories()      — HR-zone rate (≥120 → 10/min, ≥90 → 5/min, else 1.5/min) × dt
```

| Public API | Purpose |
|---|---|
| `BeginSampleWindow()` | Clear the window — call at the start of a measurement period |
| `GetAverageBPM()` | Mean BPM across the window |
| `CalculateFinalHRV()` | SDNN across the window, ms |
| `RestartCalibration()` | Discard baseline and re-run calibration |

**HRV honesty note (documented in the code):** the glove reports BPM, not
beat-to-beat timing. `ComputeSDNN` converts each BPM sample to its implied RR
interval (`60000 / bpm`) and takes the standard deviation. It trends correctly
(calmer = higher) but is **not** clinically comparable to a chest-strap reading.

### 4.2 `HeartRateYogaFlowManager` — six-state session envelope

```
PreGameConnection → PreGameCalibration → YogaSelection
                  → YogaGameplay → PostGameCalibration → Results
```

| State | Action |
|---|---|
| `PreGameCalibration` | `CalibratePreGameRoutine()` — `BeginSampleWindow()`, wait `calibrationDuration` (10s) driving a UI progress bar, then capture `baselineHR` / `baselineHRV` |
| `YogaGameplay` | Records `playStartTime` |
| `PostGameCalibration` | Same routine, captures `postGameHR` / `postGameHRV` |
| `Results` | `CalculateFinalMetrics()` then `heartRateUI.DisplayResults()` |

`CalculateFinalMetrics()`:
```
playDurationMinutes = (Time.time - playStartTime) / 60
avgHR               = (baselineHR + postGameHR) / 2
hrIntensityFactor   = Clamp(avgHR / 70, 0.8, 1.5)
caloriesBurned      = playDurationMinutes * 3.8 * hrIntensityFactor
```
MET-based approximation for yoga (~3.0 kcal/kg/hr), assuming a 70 kg reference
weight — i.e. **not personalized to the player's actual mass.**

When this manager is present, `YogaManager` defers its own result screen to it.

---

## 5. Yoga pose tracking

### `YogaTracker`

Scores how closely the glove's orientation matches a target pose.

| Method | Behavior |
|---|---|
| `Recenter()` | Captures current raw orientation as `zeroOffset`. All scoring is relative to this, so pose targets survive the BNO055's absolute heading shifting between sessions. Returns false with no glove. |
| `GetGloveRotation()` | `Inverse(zeroOffset) * rawQuat` |
| `CalculateAccuracy(cur, tgt)` | `angle = Quaternion.Angle(cur, tgt)`, linear falloff: 0° → 100%, ≥`maxAngle` (60°) → 0% |
| `Update()` | Smooths accuracy toward the new value (`smoothSpeed = 5`), updates UI text, samples into `accuracySamples` every `sampleInterval` (0.2s) |
| `StopTracking()` | Triggers `CalculateSessionResult()` |

`CalculateSessionResult()`:
- `alignment` = mean of samples — how well the pose was matched on average
- `steadiness` = `Clamp(100 - (stdDev / 30) * 100, 0, 100)` — how little the
  accuracy wobbled; stdDev ≥ 30 reads as very shaky, 0 as rock steady

Note it uses the same BNO055→Unity axis remap `(x,y,z,w) → (-y,-z,x,w)` as
`VRGloveProcessor`, duplicated rather than shared.

---

## 6. Boxing hand (`VRGloveProcessor`)

A self-contained alternative to the Rage Room pipeline — glove-only, no
`HandInputProvider` abstraction, its own punch state machine.

```
Update():
    quat  = remap(-qY, -qZ, qX, qW).normalized
    if space pressed: manualZeroOffset = quat          // re-zero
    localRotation = Inverse(manualZeroOffset) * quat

    force = |(forceY, forceZ)| * 327.67
    if Idle && force > punchDeadzone(15) && cooldown <= 0:
        targetPunchDistance = Clamp(force * 0.05, 0, maxPunchDistance = 1.5)
        → Extending (timeToExtend 0.1s) → Retracting (timeToRetract 0.25s) → cooldown 0.1s

    localPosition = anchor + (localRotation * Vector3.forward) * currentPunchDistance
```

Unlike Rage Room, here punch **distance** scales with force, and the hand aims
along its own rotation ("knuckle pointer"). Two different punch models exist in
the project; they are not shared code.

---

## 7. Known issues found while reading

| # | Issue | Impact |
|---|---|---|
| 1 | `autoReport` defaults true in the BLE library; firmware calls 7 setters/loop | ~7× the intended BLE traffic, and 6 of 7 reports carry **torn** state (new X with stale Y/Z/W) → invalid quaternions reaching Unity |
| 2 | Glove bound by enumeration index | Left/right can swap between sessions |
| 3 | BNO055 in `IMUPLUS` has no magnetic reference | Yaw drift is unbounded (~1–2°/min); no auto-recenter exists mid-session, and recenter is keyboard-bound while the player wears gloves |
| 4 | Firmware never reads `getCalibration()` | Poor fusion state is invisible to both player and game |
| 5 | Axis remap `(-y,-z,x,w)` duplicated in 3 files | `VRGloveProcessor`, `YogaTracker`, `KeyboardHandInput` — divergence risk |
| 6 | Calorie formula assumes 70 kg | Not personalized |
| 7 | Many `[TEMP DEBUG]` `Debug.Log` calls in per-frame paths | `PunchDetector`, `KeyboardHandInput`, `HandTarget` log every frame at 2–3 Hz throttle; remove before release (GC pressure + log spam) |
| 8 | Two independent punch models | Rage Room (force→speed, fixed reach) vs Boxing (force→distance) behave differently for the same physical punch |
```
