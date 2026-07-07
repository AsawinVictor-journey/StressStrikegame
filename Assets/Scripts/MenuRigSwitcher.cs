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

    private int currentIndex = -1;

    private void Start()
    {
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
}
