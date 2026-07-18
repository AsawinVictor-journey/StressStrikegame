# StressStrike — Project Brief

> Self-contained document. Everything needed to understand the project is
> included here; no repository access required.

---

# PART 1 — THE PITCH

### One line

**StressStrike is a stress-relief game that measures how you actually cope with
stress, routes you to the right kind of release, and proves it worked by reading
your heart.**

### The problem

Everyone knows exercise and meditation help with stress. Almost nobody knows
*which one they personally need on a given day.* Wellness apps hand you a menu —
breathing exercises, workouts, journaling — and ask you to self-diagnose, at the
exact moment you're least equipped to. So people pick the same thing every time,
or nothing.

The second problem: these apps can't tell whether it worked. They ask "how do
you feel now, 1–5?" A stressed person rates themselves badly. Self-report is the
weakest possible signal, and it's the only one most wellness software has.

### The insight

There is already a validated psychological instrument that answers "how does
this person cope with stress?" — the **Brief-COPE** (Carver, 1997), a 28-item
questionnaire used in real clinical and research settings. It sorts coping
behavior into distinct styles: people who confront stress head-on, people who
avoid it, people who seek meaning or support.

Those styles map remarkably cleanly onto three completely different kinds of
physical release. So we stopped asking players to choose, and let the instrument
choose.

### The product

A player puts on a glove and answers 28 short questions. Based on their coping
profile, the game recommends one of three modes:

- **Boxing** — structured, technical, rhythmic. For people who cope by
  confronting things directly.
- **Rage Room** — unstructured destruction. Smash everything in the room. For
  people who've been bottling it up and avoiding it.
- **Meditate / Yoga** — guided pose-holding and breathing. For people whose
  coping is reflective or meaning-seeking.

They play. Then the game tells them, from their own body, whether it worked.

### The differentiator

The glove is not a controller with a step counter bolted on. It carries a
**9-axis motion sensor** and an **optical heart-rate sensor**, and both feed the
game logic continuously.

That means:

- The game knows your **resting heart rate**, because it calibrates against you
  personally at the start — not a population average.
- It tracks **stress delta** live: how far above your own baseline you are, right
  now, mid-punch.
- Yoga mode is **biometrically gated** — it measures your heart rate *before* the
  session and *after*, and the result screen reports actual measured recovery,
  including a heart-rate-variability estimate. You didn't "complete a 10-minute
  session." You physiologically calmed down by a measurable amount, or you
  didn't.
- The glove talks back: it vibrates on impact and changes color to confirm the
  punch type it detected.

### Why this is defensible

Anyone can build a punching game. Anyone can strap a fitness tracker to it.

What's hard to copy is the **closed loop**: a validated psychological instrument
on the front end deciding *what you need*, custom hardware in the middle
producing *real physiological data*, and that same data on the back end proving
*whether it worked* — with each session's baseline recalibrated to the individual.

That's not a wellness app with a gimmick. That's a measurable intervention that
happens to be fun.

### Where it stands

Working, playable software across all three modes. Custom glove hardware built
and wirelessly integrated. The psychological survey and routing logic are live.
This is a functioning prototype, not a concept deck.

---

# PART 2 — DETAILED BREAKDOWN

## 2.1 System architecture

```
┌─────────────────────────────────────────────────────────────┐
│  PHYSICAL LAYER — ESP32 glove (worn by player)              │
│  BNO055 9-axis IMU  ·  MAX30102 pulse sensor                │
│  RGB status LED  ·  haptic vibration motor                  │
└────────────────────────┬────────────────────────────────────┘
                         │  Bluetooth LE (HID gamepad profile)
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  INPUT LAYER — Unity Input System                           │
│  Custom device: orientation (quaternion), punch force,      │
│  heart rate — all as continuous axes                        │
└────────────────────────┬────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  INTERPRETATION LAYER                                       │
│  BiometricEngine — resting-HR calibration, live stress      │
│    delta, calorie estimate, HRV approximation               │
│  Punch detection — spike detection, strength normalization  │
└────────────────────────┬────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  GAME LAYER                                                 │
│  Menu (3D station selector) + Brief-COPE survey             │
│  → Boxing  |  Rage Room  |  Meditate/Yoga                   │
│  → Biometric result screen                                  │
└─────────────────────────────────────────────────────────────┘
```

## 2.2 Player journey

| Step | What happens | What the system is doing |
|---|---|---|
| 1. Connect | Player puts on glove; it pairs over Bluetooth. Glove LED turns green and double-vibrates to confirm. | Device enumerates as a Bluetooth HID device; game binds to it automatically. |
| 2. Baseline | Player rests briefly. | Samples heart rate for ~10s to establish **this player's** resting BPM — every later stress reading is relative to this, not to a generic average. |
| 3. Survey | 28 short Brief-COPE questions, one at a time, with a halfway checkpoint. Fully skippable. | Scores 14 coping subscales, groups into 3 buckets, picks a recommended mode. Deterministic — a fixed scoring rule, not a black box. |
| 4. Recommendation | Result screen names a recommended mode and explains *why*, in plain language drawn from the player's dominant coping style. Three selectable cards — the recommendation is highlighted, but any mode is playable. | Recommendation highlights the matching station in the 3D menu. |
| 5. Play | Boxing, Rage Room, or Yoga. | Glove drives hand orientation and punch detection; heart rate streams continuously in the background. |
| 6. Results | Session summary with real physiological outcome. | Post-session heart rate vs. baseline, recovery measurement, calories, HRV estimate. |

## 2.3 The three modes

| | **Boxing** | **Rage Room** | **Meditate / Yoga** |
|---|---|---|---|
| **Recommended for** | Approach-style coping — confronts stressors directly | Avoidant-style coping — has been suppressing/bottling | Meaning-seeking / reflective coping |
| **Fantasy** | Trained, technical, in control | Total permission to destroy | Permission to stop |
| **Core mechanic** | Timed punches at targets; hook vs. jab detected from motion | Destructible objects — deform, break, scatter; scoring per object | Hold guided poses; steadiness and alignment tracked from hand orientation |
| **Physical intensity** | High, structured | High, chaotic | Low |
| **Hardware role** | Punch force + orientation drive strikes | Punch force drives impacts against physics objects | Orientation tracks pose stability; heart rate is the primary measure |
| **Feedback signal** | Haptic + LED per punch type; combo/score | Destruction, debris, score per object | Ambient light shifts with tracked accuracy; result band (Grounding → Centered → Balanced → Radiant) |
| **Success measured by** | Accuracy, power, consistency | Destruction score | **Measured heart-rate recovery vs. baseline** |

## 2.4 The hardware

| Component | Part | Role in the game |
|---|---|---|
| Microcontroller | ESP32 | Reads sensors, classifies punches locally, transmits over Bluetooth LE |
| Motion sensor | **BNO055** 9-axis IMU | Onboard sensor-fusion chip outputs ready-to-use orientation (quaternion) and gravity-compensated linear acceleration. Drives hand rotation and punch force. |
| Pulse sensor | **MAX30102** optical | Beat detection → rolling average BPM, streamed continuously to the game |
| Haptics | Vibration motor | Impact confirmation — short pulse on a jab, longer on a hook |
| Status | RGB LED | Blue = waiting to connect, green = ready, red = jab detected, magenta = hook detected |
| Link | Bluetooth LE, HID gamepad profile | Wireless, no drivers, no dongle — the OS sees a standard input device |

**Why BNO055 specifically:** it has a dedicated onboard processor doing sensor
fusion in hardware, so it outputs stable orientation directly. The cheaper
alternative (MPU6050) outputs only raw accelerometer and gyroscope data, which
would have required writing and tuning a custom fusion filter on the
microcontroller to get usable orientation. This choice removed an entire class
of drift and calibration problems from the software.

**Local intelligence:** the glove classifies punch type itself rather than
shipping raw data for the game to interpret. It watches a short window after an
impact spike and compares sideways acceleration against forward acceleration —
sideways-dominant is a hook, forward-dominant is a jab — then fires the matching
haptic and LED response immediately, without waiting on a round trip.

## 2.5 The biometric layer

This is the part that distinguishes the project, so it's worth spelling out.

| Metric | How it's derived | What it's used for |
|---|---|---|
| **Resting BPM** | Averaged over a calibration window at session start | Personal baseline — everything else is relative to this |
| **Current BPM** | Live from the glove's pulse sensor | Real-time exertion state |
| **Stress delta** | Current BPM minus resting BPM | How far above the player's own baseline they are — usable as a live game input (e.g. escalating intensity) |
| **Calories** | Heart-rate-zone-based accumulation over session time | Session summary / fitness framing |
| **HRV estimate (SDNN-style)** | Standard deviation of beat intervals across a sample window | Recovery/calm indicator — higher trends calmer |

Two honest notes, stated plainly because they matter for credibility:

- The HRV figure is an **approximation**, derived from averaged BPM rather than
  true beat-to-beat interval timing. It trends in the correct direction (calmer
  = higher) but is **not** clinically comparable to a chest-strap or ECG reading.
  It should be presented as a wellness indicator, never as a medical measurement.
- The system is **not a medical device** and makes no diagnostic claim. The
  Brief-COPE is used as a *preference-routing* instrument — matching a player to
  an activity they'll benefit from — not as a clinical assessment.

## 2.6 Build status

| Area | Status |
|---|---|
| Three playable modes | ✅ Built and playable |
| Brief-COPE survey + routing logic | ✅ Live in-game, deterministic scoring |
| 3D station-based menu | ✅ Built |
| Glove hardware | ✅ Built — sensors, haptics, LED, wireless link all functioning |
| Glove → game wireless integration | ✅ Working |
| Heart-rate pipeline (baseline, stress, calories, HRV) | ✅ Implemented |
| Yoga biometric pre/post session flow | ✅ Implemented |
| Keyboard/mouse fallback (play without hardware) | ✅ Working — the game is fully playable with no glove, which matters for demos and testing |
| Hardware sensor-mapping verification | ⚠️ Needs a bench-test pass against live hardware |
| Multiplayer | 🔬 Early technical experiment only — **not** a current feature |

## 2.7 Open questions / roadmap

**Near-term engineering**
- Verify the wireless data mapping against live hardware end-to-end (currently
  derived by inspection, not confirmed by measurement).
- Characterize end-to-end latency from physical punch to on-screen response.
- Measure motion-sensor drift over a full session and confirm whether a
  mid-session re-centering step is needed.
- Establish battery life per session and whether it supports the intended
  session length.

**Product**
- Two-glove support (left + right) requires a stable way for the game to tell
  the gloves apart.
- Whether Rage Room gets dedicated hardware (impact sensors on physical props)
  or continues to reuse the glove.
- Session-over-session progress tracking — currently results are per-session;
  trends over time are the obvious next value-add and the strongest retention
  hook.

**Business — not yet defined, flagged deliberately rather than invented**
- Target market and primary customer (consumer / clinic / workplace wellness /
  arcade–location-based entertainment).
- Business model (hardware + software bundle, software with optional hardware,
  subscription, or venue licensing).
- Unit cost and retail pricing for the glove.
- Regulatory posture for wellness-adjacent claims in target markets.

---

## Appendix — Vocabulary for reviewers

| Term | Meaning |
|---|---|
| **Brief-COPE** | A 28-item validated psychological questionnaire (Carver, 1997) measuring coping strategies across 14 subscales. Widely used in stress research. |
| **IMU** | Inertial Measurement Unit — motion sensor combining accelerometer, gyroscope, and (here) magnetometer. |
| **Sensor fusion** | Combining multiple raw sensor streams into a single stable orientation estimate. Done in hardware here. |
| **Quaternion** | A four-number representation of 3D rotation that avoids the gimbal-lock and wraparound problems of angle-based representations. |
| **Linear acceleration** | Acceleration with gravity mathematically removed — so a still hand reads zero regardless of how it's tilted. This is what makes punch detection reliable. |
| **HRV / SDNN** | Heart-rate variability; variation in time between beats. Higher generally indicates a calmer, more recovered state. |
| **BPM baseline** | This player's own resting heart rate, measured at session start, used as the reference point for all stress readings. |
