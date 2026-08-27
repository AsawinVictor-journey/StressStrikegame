using UnityEngine;

/// <summary>
/// PlayerPrefs-backed IPlayerDataStore - the "real backend" InMemoryDataStore was
/// always a placeholder for (see PlayerProgression's class header).
///
/// InMemoryDataStore keeps PlayerData in a plain field, so XP and Level reset to
/// 0/1 on every app launch. That made Level unusable as something to show or talk
/// about: the main menu always saw Level 1 no matter how much the player had
/// played. This stores the same PlayerData as JSON under one key, so progression
/// finally survives a restart.
///
/// Swapping this in requires no change to PlayerProgression beyond the one line in
/// Awake() that picks the store, and no change to any UI - which is exactly the
/// boundary IPlayerDataStore exists to provide.
/// </summary>
public class PlayerPrefsDataStore : IPlayerDataStore
{
    private const string PrefsKey = "Player_Progression";

    public PlayerData Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json)) return new PlayerData();

        try
        {
            var data = JsonUtility.FromJson<PlayerData>(json);
            if (data == null) return new PlayerData();

            // A saved Level of 0 would come from a corrupt/hand-edited value and
            // would break XPIntoCurrentLevel's (Level - 1) arithmetic, so floor it
            // at the game's actual starting level rather than trusting the file.
            if (data.Level < 1) data.Level = 1;
            if (data.XP < 0) data.XP = 0;
            return data;
        }
        catch
        {
            return new PlayerData(); // corrupt value - start fresh rather than throwing on load
        }
    }

    public void Save(PlayerData data)
    {
        if (data == null) return;

        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}
