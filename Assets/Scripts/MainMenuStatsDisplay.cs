using TMPro;
using UnityEngine;

/// <summary>
/// Drives the MainMenu Statistic panel's Level and Coins labels from live data —
/// PlayerProgression.Level and PlayerProgression.Coins (which itself mirrors
/// CoinManager.currentCoins, the shop's actual wallet; see PlayerProgression's class header).
///
/// Polls in Update() with a cached-compare, rather than subscribing to events: coins can
/// change from several places (session rewards, shop purchases) with no single "coins
/// changed" event to hook, and PlayerProgression.OnLevelUp only covers Level going up, not
/// this display simply becoming active again after the player returns from a mode. Same
/// cheap cached-value pattern GameManager's timer label already uses elsewhere in this
/// project — the TMP text is only touched when the displayed value actually changes.
/// </summary>
public class MainMenuStatsDisplay : MonoBehaviour
{
    public TMP_Text levelText;
    public TMP_Text coinsText;

    int lastShownLevel = int.MinValue;
    int lastShownCoins = int.MinValue;

    void Update()
    {
        int level = PlayerProgression.Instance != null ? PlayerProgression.Instance.Level : 1;
        int coins = PlayerProgression.Instance != null ? PlayerProgression.Instance.Coins : 0;

        if (level != lastShownLevel)
        {
            lastShownLevel = level;
            if (levelText != null)
                levelText.text = "Level " + level;
        }

        if (coins != lastShownCoins)
        {
            lastShownCoins = coins;
            if (coinsText != null)
                coinsText.text = coins + " Coins";
        }
    }
}
