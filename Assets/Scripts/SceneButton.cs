using UnityEngine;

public class SceneButton : MonoBehaviour
{
    [SerializeField] private string sceneName;

    public void LoadScene()
    {
        SceneTransitionManager.Instance.LoadScene(sceneName);
    }
}