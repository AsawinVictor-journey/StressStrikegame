using UnityEngine;
using UnityEditor;

public class ShopUIFixer : EditorWindow
{
    [MenuItem("Tools/Fix Shop UI Hierarchy")]
    public static void RunFix()
    {
        // Find the ShopMenu canvas
        GameObject shopMenu = GameObject.Find("ShopMenu");
        if (shopMenu == null)
        {
            Debug.LogError("Could not find ShopMenu in the scene!");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        // 1. GLOVE (100)
        FixItem(shopMenu.transform, "Glove_Item", "glove_1", 100, "buy", "equip", "equipped");
        
        // 2. FISH (167)
        FixItem(shopMenu.transform, "Fish_Item", "fish_1", 167, "buy (1)", "equip (2)", "equipped (1)");
        
        // 3. CANDY (45)
        FixItem(shopMenu.transform, "Candy_Item", "candy_1", 45, "buy (2)", "equip (3)", "equipped (2)");

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("Successfully fixed hierarchy and prices! You can Undo (Ctrl+Z) if needed.");
    }

    private static void FixItem(Transform shopMenu, string groupName, string id, int price, string buyName, string equipName, string equippedName)
    {
        Transform buyBtn = shopMenu.Find(buyName);
        Transform equipBtn = shopMenu.Find(equipName);
        Transform equippedBtn = shopMenu.Find(equippedName);

        if (buyBtn == null || equipBtn == null || equippedBtn == null)
        {
            Debug.LogWarning($"Could not find all buttons for {groupName}. Skipping.");
            return;
        }

        // Create Parent Group
        GameObject group = new GameObject(groupName);
        Undo.RegisterCreatedObjectUndo(group, "Create " + groupName);
        group.transform.SetParent(shopMenu, false);
        
        RectTransform buyRect = buyBtn.GetComponent<RectTransform>();
        RectTransform groupRect = group.AddComponent<RectTransform>();
        
        // Match Parent rect to the buy button position/size
        groupRect.position = buyRect.position;
        groupRect.anchoredPosition = buyRect.anchoredPosition;
        groupRect.sizeDelta = buyRect.sizeDelta;
        groupRect.anchorMin = buyRect.anchorMin;
        groupRect.anchorMax = buyRect.anchorMax;

        // Destroy any old ShopItemUI scripts on these buttons
        foreach (var t in new[] { buyBtn, equipBtn, equippedBtn })
        {
            var oldScripts = t.GetComponents<ShopItemUI>();
            foreach (var s in oldScripts) Undo.DestroyObjectImmediate(s);
        }

        // Parent them to the group
        Undo.SetTransformParent(buyBtn, group.transform, "Reparent " + buyName);
        Undo.SetTransformParent(equipBtn, group.transform, "Reparent " + equipName);
        Undo.SetTransformParent(equippedBtn, group.transform, "Reparent " + equippedName);

        // Rename them so ShopItemUI.cs transform.Find() works!
        Undo.RecordObject(buyBtn.gameObject, "Rename buy");
        buyBtn.gameObject.name = "buy";
        Undo.RecordObject(equipBtn.gameObject, "Rename equip");
        equipBtn.gameObject.name = "equip";
        Undo.RecordObject(equippedBtn.gameObject, "Rename equipped");
        equippedBtn.gameObject.name = "equipped";

        // Snap their local positions to zero inside the new parent
        buyRect.anchoredPosition = Vector2.zero;
        equipBtn.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        equippedBtn.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        // Add the fresh ShopItemUI script to the PARENT group
        ShopItemUI newScript = Undo.AddComponent<ShopItemUI>(group);
        newScript.itemID = id;
        newScript.itemPrice = price;
        
        EditorUtility.SetDirty(group);
    }
}
