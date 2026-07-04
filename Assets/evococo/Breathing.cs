using UnityEngine;

public class Breathing : MonoBehaviour
{
    public Vector3 minScale = new Vector3(1f, 1f, 1f);
    public Vector3 maxScale = new Vector3(1.2f, 1.2f, 1.2f);

    public float inhaleTime = 5f;
    public float exhaleTime = 5f;

    float timer;
    bool inhale = true;

    void Update()
    {
        timer += Time.deltaTime;

        if (inhale)
        {
            float t = timer / inhaleTime;
            transform.localScale = Vector3.Lerp(minScale, maxScale, t);

            if (timer >= inhaleTime)
            {
                timer = 0;
                inhale = false;
            }
        }
        else
        {
            float t = timer / exhaleTime;
            transform.localScale = Vector3.Lerp(maxScale, minScale, t);

            if (timer >= exhaleTime)
            {
                timer = 0;
                inhale = true;
            }
        }
    }
}