using UnityEngine;
using TMPro;

public class Breathing : MonoBehaviour
{
    public Vector3 minScale = new Vector3(1f, 1f, 1f);
    public Vector3 maxScale = new Vector3(1.2f, 1.2f, 1.2f);

    public float inhaleTime = 5f;
    public float exhaleTime = 5f;
    
    public TextMeshProUGUI instructionText;

    float timer;
    bool inhale = true;

    void Start()
    {
        if (instructionText != null) instructionText.text = "Breathe In";
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (inhale)
        {
            float t = timer / inhaleTime;
            // Use SmoothStep for a more natural breathing feel instead of linear Lerp
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.Lerp(minScale, maxScale, smoothT);

            if (timer >= inhaleTime)
            {
                timer = 0;
                inhale = false;
                if (instructionText != null) instructionText.text = "Breathe Out";
            }
        }
        else
        {
            float t = timer / exhaleTime;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.Lerp(maxScale, minScale, smoothT);

            if (timer >= exhaleTime)
            {
                timer = 0;
                inhale = true;
                if (instructionText != null) instructionText.text = "Breathe In";
            }
        }
    }
}