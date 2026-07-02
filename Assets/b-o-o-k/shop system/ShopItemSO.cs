using UnityEngine;

[CreateAssetMenu(fileName = "New Shop Item", menuName = "Shop System/Shop Item")]
public class ShopItemSO : ScriptableObject
{
    [Header("Item Identification")]
    [Tooltip("Unique ID for saving/loading. MUST be unique for every item! (e.g., 'item_blue_glove')")]
    public string itemID;
    
    [Header("Item Details")]
    public string itemName;
    public int itemPrice;
    
    [Header("Visuals (Optional for your setup)")]
    public Sprite itemIcon;
}
