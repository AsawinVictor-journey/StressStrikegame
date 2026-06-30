using UnityEngine;
using UnityEngine.UI;

public class ShopItemUI : MonoBehaviour
{
    [Header("Item Configuration")]
    [Tooltip("Set this to glove_1, fish_1, or candy_1")]
    public string itemID;
    public int itemPrice;

    [Header("UI Elements (Auto-assigned at runtime)")]
    public GameObject buyButtonObj;
    public GameObject equipButtonObj;
    public GameObject equippedButtonObj;
    
    private const string UNLOCK_SAVE_KEY_PREFIX = "UnlockedItem_";

    private void Awake()
    {
        // Since this script is attached to the Buy button, this.gameObject IS the buy button.
        buyButtonObj = this.gameObject;

        // Look inside our own parent group to find the Equip and Equipped buttons!
        Transform parentGroup = transform.parent;
        if (parentGroup != null)
        {
            // We search through the parent's children. 
            // We use a loop so it's not strictly case-sensitive and ignores whitespace.
            foreach (Transform child in parentGroup)
            {
                string childName = child.name.ToLower();
                if (childName.Contains("equip") && !childName.Contains("equipped"))
                {
                    equipButtonObj = child.gameObject;
                }
                else if (childName.Contains("equipped"))
                {
                    equippedButtonObj = child.gameObject;
                }
            }
        }
    }

    private void Start()
    {
        // Add Button Listeners automatically
        if (buyButtonObj != null)
        {
            Button buyBtn = buyButtonObj.GetComponent<Button>();
            if (buyBtn == null) buyBtn = buyButtonObj.AddComponent<Button>();
            buyBtn.onClick.AddListener(OnBuyClicked);
        }

        if (equipButtonObj != null)
        {
            Button equipBtn = equipButtonObj.GetComponent<Button>();
            if (equipBtn == null) equipBtn = equipButtonObj.AddComponent<Button>();
            equipBtn.onClick.AddListener(OnEquipClicked);
        }
        
        // We no longer need to force position snaps because the Layout Group / Anchors 
        // should now handle it correctly since they share the same parent!

        // Load Save State
        RefreshUIState();
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
            // Already bought! Show equip.
            if (buyButtonObj != null) buyButtonObj.SetActive(false);
            if (equipButtonObj != null) equipButtonObj.SetActive(true);
            if (equippedButtonObj != null) equippedButtonObj.SetActive(false);
        }
        else
        {
            // Not bought yet. Show buy.
            if (buyButtonObj != null) buyButtonObj.SetActive(true);
            if (equipButtonObj != null) equipButtonObj.SetActive(false);
            if (equippedButtonObj != null) equippedButtonObj.SetActive(false);
        }
    }

    private void OnBuyClicked()
    {
        // Assuming CoinManager exists. If not, it will just bypass the check if you remove it.
        // For now we keep your CoinManager logic intact:
        if (CoinManager.Instance != null && CoinManager.Instance.HasEnoughCoins(itemPrice))
        {
            CoinManager.Instance.SpendCoins(itemPrice);
            
            PlayerPrefs.SetInt(UNLOCK_SAVE_KEY_PREFIX + itemID, 1);
            PlayerPrefs.Save();
            
            Debug.Log("Purchased item successfully!");
            RefreshUIState();
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
