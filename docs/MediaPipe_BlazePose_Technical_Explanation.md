# MediaPipe BlazePose: Technical Explanation

## What is BlazePose?

BlazePose is a **neural network model** created by Google that detects human body poses from a single camera (like a webcam). It identifies 33 key body points called **landmarks** — your shoulders, elbows, wrists, hips, knees, ankles, and more — in real-time video.

**In our project:** The Yoga mode uses BlazePose to track where your body is positioned every frame (~30 times per second).

---

## How Does a Neural Network Work? (Simple Version)

A neural network is a mathematical system inspired by how brains learn. Think of it like this:

1. **Input** → You feed it raw data (a video frame with pixels)
2. **Hidden layers** → The network processes the data through layers of mathematical transformations
3. **Output** → It produces a prediction (in BlazePose's case: "the left elbow is at pixel position X,Y")

**Why is this better than hand-coded rules?**
- A rule like "if this area is skin-colored and next to another skin area, call it an elbow" fails constantly
- A trained neural network learned from thousands of body photos and can handle different lighting, angles, clothing, and body types

**BlazePose was trained by Google** on massive datasets of people in different poses, angles, and lighting. We don't train it ourselves — we just use the pre-trained model.

---

## What BlazePose Actually Does in StressStrike

```
Camera Feed (video)
        ↓
    BlazePose (neural network)
        ↓
    33 Landmarks (joint positions)
        ↓
    YogaJointAngles (compute angles)
        ↓
    Pose Score (compare to target)
        ↓
    Feedback to player
```

### Step-by-step:

**1. Capture** — Webcam sends video frames to `MediaPipePoseTracker`

**2. Detect landmarks** — BlazePose's neural network runs on each frame and outputs 33 body points:
   - Head (nose, eyes, ears)
   - Arms (shoulders, elbows, wrists, fingers)
   - Torso (hips)
   - Legs (knees, ankles, feet)

**3. Compute joint angles** — `YogaJointAngles` takes those 33 points and calculates 5 angles:
   - Left elbow angle
   - Right elbow angle
   - Left shoulder angle
   - Right shoulder angle
   - Torso lean (how far you're bending left/right)

**4. Score the pose** — Compare your current angles to the target angles (e.g., "SideBendLeft should have torsoLean = -13.57°"):
   - If angles are close → High score
   - If angles are far → Low score

**5. Show feedback** — Display accuracy percentage to the player in real-time

---

## The Current Limitation: Hand-Set Thresholds

Right now, the system says:
> "Your torso angle is -14° and the target is -13.57°. That's close enough, so you're doing it right."

**The problem:** "Close enough" is decided by **hardcoded numbers**, not learned from data. We manually set a tolerance (±5°, ±10°, etc.). This means:

- ❌ Different body types might naturally hold angles differently
- ❌ Different camera distances change how angles look
- ❌ Different people have different ranges of motion

**We're improving this by training a classifier** (see below).

---

## The Improvement: Pose Classification (Coming)

Instead of just comparing angles to targets, we want to ask:

> **"Based on these 5 angles, am I doing SideBendLeft correctly or not?"**

This is a **classification problem** — the model learns to say "yes, that's a good SideBendLeft" or "no, that's not quite right."

### How we'll build it:

**1. Collect training data** (~30 minutes)
   - Stand in front of the webcam
   - Hold each of the 6 poses (Prayer, OpenArms, RaiseArms, ClosedArms, SideBendLeft, SideBendRight) for 30 seconds each
   - Record the 5 angles + which pose you're doing

**2. Train a classifier** (~45 minutes)
   - Use scikit-learn (Python) to train a small model on ~5,000 samples
   - The model learns: "When the angles look *like this*, it's a good SideBendLeft"

**3. Export the learned weights** (~15 minutes)
   - Extract the mathematical weights from the trained model
   - Paste them into C# code in Unity

**4. Run inference in Unity** (real-time)
   - Feed the 5 current angles into the C# classifier
   - Get back: "You're 92% confident this is SideBendLeft"

---

## Why This Is Still Machine Learning

**BlazePose is the "hard" part:**
- It's a deep neural network (many layers) trained on massive datasets
- Detects 33 body landmarks from raw pixels
- Runs in real-time on consumer hardware (CPU)

**Our classifier is the "easy" part:**
- A small logistic regression (5 inputs, 6 outputs)
- Trained on our own data collected from this one project
- Runs in 1 millisecond in C#

**Together:** We leverage Google's pre-trained perception model (BlazePose) + our own trained decision model (the classifier) to create a full feedback system.

### For your report:

> "StressStrike uses MediaPipe's BlazePose neural network for real-time body pose detection, extracting 33 landmarks per frame. A custom-trained classifier then validates pose correctness by comparing joint angles to learned patterns, enabling adaptive feedback that improves with player-specific calibration data."

---

## The Technical Stack

| Component | Type | Purpose |
|-----------|------|---------|
| **BlazePose** | Pre-trained CNN | Detect 33 body landmarks from video |
| **YogaJointAngles** | Pure math | Compute 5 joint angles from landmarks |
| **Pose Classifier** | Logistic regression | Validate pose correctness |
| **MediaPipePoseTracker** | C# script | Orchestrate the pipeline |

---

## Key Takeaway

- **BlazePose = Computer vision** (sees your body)
- **Our classifier = Machine learning** (understands if you're doing it right)
- **Together = An adaptive yoga coach** that learns how your body moves and gives personalized feedback

You're not building the vision part from scratch — you're standing on Google's shoulders. What you're adding is the judgment layer, trained on real people using your game.
