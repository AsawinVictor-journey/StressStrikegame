using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UIHitBox : MonoBehaviour
{
    void Start()
    {
        // 0.5f means clicks only register where transparency is less than 50%.
        // This requires the sprite's texture to be Read/Write enabled (or Crunch
        // compressed); otherwise Unity throws. Guard so an un-readable texture just
        // loses the alpha test instead of throwing an exception every Play.
        try
        {
            GetComponent<Image>().alphaHitTestMinimumThreshold = 0.5f;
        }
        catch (System.Exception)
        {
            // Texture isn't readable — skip alpha hit testing on this image.
        }
    }
}
