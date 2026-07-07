using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UIHitBox : MonoBehaviour
{
    void Start()
    {
        // 0.5f means clicks only register where transparency is less than 50%
        GetComponent<Image>().alphaHitTestMinimumThreshold = 0.5f; 
    }
}
