// This file is intentionally empty.
// HandPosition has been replaced by the following scripts:
//
//   KeyboardHandInput.cs      — PC IMU simulator
//   ImuVelocityIntegrator.cs  — acceleration → damped velocity (plain class, owned by HandTarget)
//   HandTarget.cs             — workspace-bounded target Transform the player controls
//   PhysicsHandController.cs  — Rigidbody that chases the target
//   PunchDetector.cs          — classifies acceleration spikes as punches
//   PunchController.cs        — punch lunge + timed hitbox
//
// Remove the HandPosition component from all GameObjects in the scene
// and add the components above instead.
