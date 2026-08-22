using UnityEditor;
using UnityEngine;

/// <summary>
/// One-off reset tools, run from inside the actual Editor session (not from an external
/// registry check, which may not see the same PlayerPrefs store this Editor instance uses).
/// </summary>
public static class ResetSaveData
{
    [MenuItem("Tools/Save Data/Clear Coins Only")]
    public static void ClearCoinsOnly()
    {
        // Matches CoinManager.COIN_SAVE_KEY.
        PlayerPrefs.DeleteKey("Player_Coins");
        PlayerPrefs.Save();
        Debug.Log("[ResetSaveData] Cleared 'Player_Coins'. Next Play will start at CoinManager's default (150).");
    }

    [MenuItem("Tools/Save Data/Clear ALL Save Data (Coins, High Score, Brief-COPE, Shop Unlocks, Cloud Consent)")]
    public static void ClearAll()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Clear ALL Save Data",
            "This wipes every PlayerPrefs key this project uses: coins, Boxing high score, " +
            "Brief-COPE survey result, shop item unlocks, and cloud-sync consent/player ID. " +
            "This cannot be undone. Continue?",
            "Clear Everything",
            "Cancel");

        if (!confirmed) return;

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[ResetSaveData] Cleared all PlayerPrefs data for this project.");
    }
}
