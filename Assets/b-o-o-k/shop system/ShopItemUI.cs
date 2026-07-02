using UnityEngine;
using UnityEngine.UI;

public class ShopItemUI : MonoBehaviour
{
    [Header("Item Configuration")]
    [Tooltip("Set this to glove_1, fish_1, or candy_1")]
    public string itemID;
    public int itemPrice;

    [Header("UI Elements (drag these in manually)")]
    public GameObject buyButtonObj;
    public GameObject equipButtonObj;
    public GameObject equippedButtonObj;

    private const string UNLOCK_SAVE_KEY_PREFIX = "UnlockedItem_";

    private void Start()
    {
        // Wire each button's click. Each object must already have a Button component.
        WireButton(buyButtonObj, OnBuyClicked, "Buy");
        WireButton(equipButtonObj, OnEquipClicked, "Equip");

        // Set the initial visible state from saved data.
        RefreshUIState();
    }

    // Finds the Button on the dragged object and registers its click handler.
    private void WireButton(GameObject obj, UnityEngine.Events.UnityAction handler, string label)
    {
        if (obj == null)
        {
            Debug.LogError($"[ShopItemUI:{itemID}] {label} button GameObject is not assigned in the Inspector.", this);
            return;
        }

        Button btn = obj.GetComponent<Button>();
        if (btn == null)
        {
            Debug.LogError($"[ShopItemUI:{itemID}] '{obj.name}' has no Button component — add one in the Inspector.", obj);
            return;
        }

        btn.onClick.RemoveListener(handler); // guard against double-registration
        btn.onClick.AddListener(handler);
    }

    private void Update()
    {
        // Developer Cheat: Press 'R' to reset the shop so you can test buying again!
        if (Input.GetKeyDown(KeyCode.R))
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            RefreshUIState();
            Debug.Log("Deleted all save data! Shop reset!");
        }
    }

    private void RefreshUIState()
    {
        bool isUnlocked = PlayerPrefs.GetInt(UNLOCK_SAVE_KEY_PREFIX + itemID, 0) == 1;

        if (isUnlocked)
        {
            // Already bought! Show Equip.
            if (buyButtonObj != null) buyButtonObj.SetActive(false);
            if (equipButtonObj != null) equipButtonObj.SetActive(true);
            if (equippedButtonObj != null) equippedButtonObj.SetActive(false);
        }
        else
        {
            // Not bought yet. Show Buy.
            if (buyButtonObj != null) buyButtonObj.SetActive(true);
            if (equipButtonObj != null) equipButtonObj.SetActive(false);
            if (equippedButtonObj != null) equippedButtonObj.SetActive(false);
        }
    }

    private void OnBuyClicked()
    {
        if (CoinManager.Instance != null && CoinManager.Instance.HasEnoughCoins(itemPrice))
        {
            CoinManager.Instance.SpendCoins(itemPrice);

            PlayerPrefs.SetInt(UNLOCK_SAVE_KEY_PREFIX + itemID, 1);
            PlayerPrefs.Save();

            Debug.Log("Purchased item successfully: " + itemID);
            RefreshUIState(); // Buy -> Equip
        }
        else
        {
            Debug.Log("Not enough coins to buy this item! (Or CoinManager is missing)");
        }
    }

    private void OnEquipClicked()
    {
        Debug.Log("Equipped item: " + itemID);

        // Switch UI from Equip -> Equipped
        if (equipButtonObj != null) equipButtonObj.SetActive(false);
        if (equippedButtonObj != null) equippedButtonObj.SetActive(true);
    }
}
