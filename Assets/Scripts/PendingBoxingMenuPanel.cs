using UnityEngine;

// Carries a "which rig to show" instruction across a scene load. Unity scene
// transitions (SceneTransitionManager.LoadScene) can't pass parameters
// directly, so a button in another scene (e.g. UI Mainmenu.unity's "back"
// button) sets RigToOpen before loading BoxingMenu.unity; MenuRigSwitcher
// reads and clears it on Start() to decide which rig to show instead of its
// usual startRigIndex default. No-op if nothing was set (normal scene entry).
public static class PendingBoxingMenuPanel
{
    public static string RigToOpen;
}
