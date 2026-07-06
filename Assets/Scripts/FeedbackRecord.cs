using System;

[Serializable]
public class FeedbackRecord
{
    public string playerId;
    public long timestamp;
    public string workHappening;
    public int workStress;
    public string ventText;
    public string jobType;
    public ExerciseBlocker blocker;
    public float caloriesBurned;
    public float hrv;
}

[Serializable]
public enum ExerciseBlocker { None, Motivation, NoTime, Injury }
