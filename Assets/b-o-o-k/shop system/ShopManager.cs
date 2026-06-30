using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    [Header("Shop Buttons (Drag your UI buttons here)")]
    public Button buyHealthPotionButton;
    public Button buySwordButton;

    private void Start()
    {
        // Hook up our buttons to the purchasing methods
        if (buyHealthPotionButton != null)
        {
            buyHealthPotionButton.onClick.AddListener(BuyHealthPotion);
        }

        if (buySwordButton != null)
        {
            buySwordButton.onClick.AddListener(BuySword);
        }
    }

    private void BuyHealthPotion()
    {
        int cost = 20;

        // Ask CoinManager if we have enough coins
        if (CoinManager.Instance.HasEnoughCoins(cost))
        {
            CoinManager.Instance.SpendCoins(cost);
            Debug.Log("Purchased Health Potion successfully!");
            // Here you would add the item to the player's inventory
        }
        else
        {
            Debug.Log("Failed to buy Health Potion. Need more coins.");
        }
    }

    private void BuySword()
    {
        int cost = 150;

        if (CoinManager.Instance.HasEnoughCoins(cost))
        {
            CoinManager.Instance.SpendCoins(cost);
            Debug.Log("Purchased Sword successfully!");
        }
        else
        {
            Debug.Log("Failed to buy Sword. Need more coins.");
        }
    }
}
