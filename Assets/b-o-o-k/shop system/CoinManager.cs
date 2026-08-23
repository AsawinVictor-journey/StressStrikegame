using UnityEngine;
using TMPro;

public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }

    [Header("Coin Data")]
    public int currentCoins = 150;
    private const string COIN_SAVE_KEY = "Player_Coins"; // Key for saving/loading

    [Header("UI Elements")]
    public TextMeshProUGUI coinTextUI;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Must survive scene loads: this is the single spendable-coin wallet fed by every
            // mode (Boxing, Rage Room, Yoga) via PlayerProgression.AddSessionResult(), and mode
            // scenes are loaded single (replacing the scene this lives in) rather than
            // additively. Without this, Instance would go null the moment a mode scene loads
            // and every non-Boxing session's coins would be silently dropped.
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        LoadCoins(); // Load coins when game starts
        UpdateCoinUI();
    }

    public void AddCoins(int amount)
    {
        currentCoins += amount;
        SaveCoins(); // Save immediately when coins change
        UpdateCoinUI();
    }

    public bool HasEnoughCoins(int amount)
    {
        return currentCoins >= amount;
    }

    public void SpendCoins(int amount)
    {
        if (HasEnoughCoins(amount))
        {
            currentCoins -= amount;
            SaveCoins(); // Save immediately when coins are spent
            UpdateCoinUI();
        }
        else
        {
            Debug.Log("Not enough coins!");
        }
    }

    private void UpdateCoinUI()
    {
        if (coinTextUI != null)
        {
            coinTextUI.text = currentCoins.ToString();
        }
    }

    // --- PHASE 2: SAVING AND LOADING (PERSISTENT DATA) ---
    
    private void SaveCoins()
    {
        PlayerPrefs.SetInt(COIN_SAVE_KEY, currentCoins);
        PlayerPrefs.Save(); // Force save to device immediately
    }

    private void LoadCoins()
    {
        // Check if we have saved data. If yes, load it. If no, keep the default (150) —
        // a fresh player's starting balance.
        if (PlayerPrefs.HasKey(COIN_SAVE_KEY))
        {
            currentCoins = PlayerPrefs.GetInt(COIN_SAVE_KEY);
        }
    }
}
