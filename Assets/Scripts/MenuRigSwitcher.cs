using UnityEngine;

public class MenuRigSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class Rig
    {
        public string rigName;
        public GameObject root;

        public GameObject UI;
    }

    [Header("Rigs (index order matters for ShowOnly calls)")]
    public Rig[] rigs;

    [Header("Rig shown on scene start")]
    public int startRigIndex = 0;

    [Header("Level Loop")]
    [Tooltip("First rig index that the Next()/Previous() arrows loop across. Everything below this (e.g. index 0, the hub) is reached only via ShowOnly and isn't part of the loop.")]
    public int loopStartIndex = 1;

    private int currentIndex = -1;

    private void Start()
    {
        // A button in another scene (e.g. UI Mainmenu.unity's "back" button)
        // may have requested a specific rig via PendingBoxingMenuPanel before
        // loading this scene. Honor that over the usual startRigIndex default.
        string pendingRig = PendingBoxingMenuPanel.RigToOpen;
        if (!string.IsNullOrEmpty(pendingRig))
        {
            PendingBoxingMenuPanel.RigToOpen = null;
            if (ShowOnly(pendingRig))
                return;
        }

        ShowOnly(startRigIndex);
    }

    public void ShowOnly(int index)
    {
        if (rigs == null || index < 0 || index >= rigs.Length) return;
        if (index == currentIndex) return;

        for (int i = 0; i < rigs.Length; i++)
        {
            bool active = (i == index);

            if (rigs[i]?.root != null)
                rigs[i].root.SetActive(active);

            if (rigs[i]?.UI != null)
                rigs[i].UI.SetActive(active);
        }

        currentIndex = index;
    }

    // Name-based counterpart to ShowOnly(int), for callers that only know the
    // rig by name (e.g. cross-scene requests via PendingBoxingMenuPanel).
    // Returns false (and leaves the current rig untouched) if no rig with
    // that name is registered yet.
    public bool ShowOnly(string rigName)
    {
        if (rigs == null || string.IsNullOrEmpty(rigName)) return false;

        for (int i = 0; i < rigs.Length; i++)
        {
            if (rigs[i] != null && rigs[i].rigName == rigName)
            {
                ShowOnly(i);
                return true;
            }
        }

        return false;
    }

    // Wrap forward within [loopStartIndex, rigs.Length - 1] so the level pages
    // (Training, Level1, Level2, Level3, ...) always loop instead of dead-ending,
    // regardless of how many levels get added or removed later.
    public void Next()
    {
        if (rigs == null || rigs.Length == 0) return;
        int lastIndex = rigs.Length - 1;
        if (lastIndex < loopStartIndex) return;

        int next = currentIndex + 1;
        if (next > lastIndex) next = loopStartIndex;
        ShowOnly(next);
    }

    // Wrap backward within [loopStartIndex, rigs.Length - 1] — see Next().
    public void Previous()
    {
        if (rigs == null || rigs.Length == 0) return;
        int lastIndex = rigs.Length - 1;
        if (lastIndex < loopStartIndex) return;

        int prev = currentIndex - 1;
        if (prev < loopStartIndex) prev = lastIndex;
        ShowOnly(prev);
    }
}
