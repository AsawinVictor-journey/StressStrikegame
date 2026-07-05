using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public TMP_Text resultText;

    void Awake()
    {
        Instance = this;
    }

    public void ShowResult(string result)
    {
        resultText.text = result;

        CancelInvoke();
        Invoke(nameof(ClearText), 1f);
    }

    void ClearText()
    {
        resultText.text = "";
    }
}