using UnityEngine;
using UnityEngine.Playables;
using TMPro;

public class WalkthroughDirector : MonoBehaviour
{
    public PlayableDirector timelineDirector;
    public TextMeshProUGUI subtitleText;
    
    [System.Serializable]
    public struct SubtitleClip
    {
        public float timeTrigger;
        public string text;
    }
    
    public SubtitleClip[] subtitles;
    private int currentSubtitleIndex = 0;
    
    void Start()
    {
        if (timelineDirector != null)
        {
            timelineDirector.Play();
        }
        
        if (subtitleText != null)
        {
            subtitleText.text = "";
        }
    }

    void Update()
    {
        if (timelineDirector == null || subtitleText == null) return;
        
        // Simple subtitle driver based on timeline time
        if (currentSubtitleIndex < subtitles.Length)
        {
            if (timelineDirector.time >= subtitles[currentSubtitleIndex].timeTrigger)
            {
                subtitleText.text = subtitles[currentSubtitleIndex].text;
                currentSubtitleIndex++;
            }
        }
        
        // Hide subtitles when timeline is done
        if (timelineDirector.state != PlayState.Playing)
        {
            subtitleText.text = "";
        }
    }
    
    public void SkipCutscene()
    {
        if (timelineDirector != null)
        {
            timelineDirector.time = timelineDirector.duration;
        }
    }
}
