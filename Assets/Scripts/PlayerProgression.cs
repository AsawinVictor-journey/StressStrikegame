using System;
using UnityEngine;

/// <summary>
/// XP / Level / Coin system, shared by every game mode (Boxing, Rage Room, Yoga).
/// All persistence goes through an IPlayerDataStore (see the Awake() assignment below) — this
/// class never reads/writes PlayerPrefs, files, or a database directly.
///
/// NOTE: the daily-login Streak feature (CurrentStreak/LongestStreak, the CheckAndUpdateStreak
/// date logic, and the streak bonus in the coin formula) was intentionally removed from THIS
/// class and has not come back to it. The reason given was that nothing persisted it. That
/// persistence now exists — see PlayerStats (Assets/Scripts/CoachByte/PlayerStats.cs), which
/// tracks currentStreak/longestStreak against a stored local calendar date and is what Coach
/// Byte reads. Still don't reintroduce a streak here: PlayerStats owns it, and the coin
/// formula below deliberately remains streak-free.
///
/// FORMULA RATIONALE
/// ------------------
/// XP per session = 15 (base) + min(performanceNormalized * 30, 30) + min(durationMinutes, 15)
///   - The 15-point base guarantees every session (even a bad one, or one abandoned early)
///     rewards *something* — this game is used by stressed players, and a 0-XP result after
///     spending time in-game is punishing in a way that undermines the point of the app.
///   - Performance contributes up to 30 points so skill/effort is rewarded, but is capped so a
///     single great session can't dwarf consistent play.
///   - Duration contributes up to 15 points (1 XP/minute, capped at 15 min) so players are
///     rewarded for actually using the app to decompress, not just for "winning" — but it's
///     capped so idling for an hour can't be used to farm XP.
///
/// Level requirement = 100 * N (linear, not exponential).
///   - Level 1 is the starting level (0 total XP). Each additional level costs a flat 100 XP,
///     so "the XP required for level N" cited in the spec (100*N) is read as: reaching Level N
///     requires 100*(N-1) cumulative XP. Equivalently, advancing FROM level L TO level L+1
///     always costs exactly 100*L — see AddSessionResult: threshold is a constant 100 per
///     level, which is what keeps the curve linear (no runaway grind at high levels, unlike
///     exponential curves that make late-game leveling feel punishing).
///
/// Coins per session = floor(Score/10) + floor(IntensityUnits*0.5)
///   - Score and intensity (mode-specific "how hard/well did you hit/perform") both convert
///     directly into currency so coins track in-session effort. No streak term (see NOTE above).
///
/// COIN SOURCE OF TRUTH: CoinManager (Assets/b-o-o-k/shop system/CoinManager.cs), not this class.
///   - CoinManager is the wallet the shop actually spends from. PlayerProgression used to keep
///     its own separate Coins total in PlayerData, which meant coins earned outside Boxing
///     never reached the real wallet. AddSessionResult() now calls CoinManager.AddCoins()
///     directly for all three modes, and the Coins property below just mirrors
///     CoinManager.currentCoins for convenience — there is exactly one coin total in the game.
///
/// SINGLETON CHOICE: MonoBehaviour singleton vs ScriptableObject
///   - A ScriptableObject "live data" asset is a nice pattern for editor-time authoring and
///     avoids scene-lifetime concerns, but it doesn't get simulation callbacks and — more
///     importantly — its in-memory field values persist across play sessions IN THE EDITOR
///     only, not in a build; you'd still need an explicit load/save step and a place to run
///     it from. A DontDestroyOnLoad MonoBehaviour singleton needs no extra wiring beyond
///     dropping the prefab/GameObject in the first-loaded scene, survives scene transitions
///     between game modes out of the box, and gives a natural place to load-on-Awake /
///     save-on-change. That's a better fit for "one persistent progression tracker shared
///     across 3 separate mode scenes", so that's what this is.
/// </summary>
public class PlayerProgression : MonoBehaviour
{
    public static PlayerProgression Instance { get; private set; }

    private const int BaseXP = 15;
    private const int PerformanceXPCap = 30;
    private const int DurationXPCap = 15;
    private const int XPPerLevel = 100;

    private IPlayerDataStore store;
    private PlayerData data;

    public int TotalXP => data.XP;
    public int Level => data.Level;

    /// <summary>XP earned so far within the current level (0..XPPerLevel).</summary>
    public int XPIntoCurrentLevel => data.XP - (data.Level - 1) * XPPerLevel;

    /// <summary>XP needed to go from the current level to the next. Constant since the curve is linear — see class header.</summary>
    public int XPToNextLevel => XPPerLevel;

    /// <summary>0-1 fill fraction for a level/XP progress bar.</summary>
    public float LevelProgress01 => Mathf.Clamp01((float)XPIntoCurrentLevel / XPToNextLevel);

    /// <summary>Mirrors CoinManager.currentCoins — see COIN SOURCE OF TRUTH note above.</summary>
    public int Coins => CoinManager.Instance != null ? CoinManager.Instance.currentCoins : 0;

    /// <summary>Fired once per level gained (can fire more than once from a single session if a big XP dump crosses multiple thresholds). Passes the new level.</summary>
    public event Action<int> OnLevelUp;

    public struct SessionRewardResult
    {
        public int XPAwarded;
        public int CoinsAwarded;
        public int TotalCoins;
        public int Level;
        public bool LeveledUp;

        /// <summary>0-1 fill fraction for a level/XP progress bar, as of right after this session was applied.</summary>
        public float LevelProgress01;

        /// <summary>0-1 fill fraction as of right BEFORE this session was applied — the bar's animation start point.</summary>
        public float PreviousLevelProgress01;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // The "real backend" the note below used to defer: PlayerPrefsDataStore persists
        // XP/Level across app launches. InMemoryDataStore reset them every restart, which
        // meant Level was always 1 outside the session that earned it.
        store = new PlayerPrefsDataStore();

        data = store.Load();
    }

    /// <summary>XP formula. Always >= BaseXP, never 0, never negative.</summary>
    public static int CalculateXP(float performanceNormalized01, float durationMinutes)
    {
        float performancePart = Mathf.Min(Mathf.Clamp01(performanceNormalized01) * PerformanceXPCap, PerformanceXPCap);
        float durationPart = Mathf.Min(Mathf.Max(0f, durationMinutes), DurationXPCap);
        int xp = Mathf.RoundToInt(BaseXP + performancePart + durationPart);
        return Mathf.Max(xp, BaseXP);
    }

    /// <summary>Coin formula: floor(score/10) + floor(intensityUnits*0.5). No streak term — see class header.</summary>
    public int CalculateCoins(int score, float intensityUnits)
    {
        int coins = Mathf.FloorToInt(score / 10f)
                  + Mathf.FloorToInt(intensityUnits * 0.5f);
        return Mathf.Max(coins, 0);
    }

    /// <summary>
    /// Applies a session's already-computed XP to the persistent XP/Level total, credits coins
    /// to CoinManager (the single spendable-coin source of truth — see class header), resolves
    /// any level-ups, saves, and returns a summary for the Result Screen UI to display.
    /// </summary>
    public SessionRewardResult AddSessionResult(int xp, int coins)
    {
        xp = Mathf.Max(xp, BaseXP);
        coins = Mathf.Max(coins, 0);

        float previousLevelProgress = LevelProgress01;

        data.XP += xp;

        bool leveledUp = false;
        while (data.XP >= data.Level * XPPerLevel)
        {
            data.Level++;
            leveledUp = true;
            OnLevelUp?.Invoke(data.Level);
        }

        store.Save(data);

        if (CoinManager.Instance != null)
            CoinManager.Instance.AddCoins(coins);

        return new SessionRewardResult
        {
            XPAwarded = xp,
            CoinsAwarded = coins,
            TotalCoins = Coins,
            Level = data.Level,
            LeveledUp = leveledUp,
            LevelProgress01 = LevelProgress01,
            PreviousLevelProgress01 = previousLevelProgress
        };
    }
}
