using UnityEngine;
using TMPro;

public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }

    [Header("Coin Data")]
    public int currentCoins = 100; // Starting with some coins for testing
    private const string COIN_SAVE_KEY = "Player_Coins"; // Key for saving/loading

    [Header("UI Elements")]
    public TextMeshProUGUI coinTextUI;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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
        // Check if we have saved data. If yes, load it. If no, keep the default (100).
        if (PlayerPrefs.HasKey(COIN_SAVE_KEY))
        {
            currentCoins = PlayerPrefs.GetInt(COIN_SAVE_KEY);
        }

        // --- ADDED FOR TESTING: Always ensure we have plenty of money to test! ---
        if (currentCoins < 1000)
        {
            currentCoins = 10000;
            SaveCoins();
        }
    }
}
