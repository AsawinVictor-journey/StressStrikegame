using UnityEngine;
using UnityEngine.UI;

// Put this on the Exit (EXIT SHOP) button.
public class ShopExitButton : MonoBehaviour
{
    public GameObject shopMenu;   // drag ShopMenu here (the panel, not the "Shop" label)
    public GameObject mainMenu;   // drag MainMenu here

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnExit);
    }

    void OnExit()
    {
        if (shopMenu == null || mainMenu == null)
        {
            Debug.LogError("[ShopExitButton] shopMenu or mainMenu is not assigned in the Inspector.", this);
            return;
        }

        shopMenu.SetActive(false);
        mainMenu.SetActive(true);
    }
}
