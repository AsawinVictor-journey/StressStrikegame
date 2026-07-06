using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class ComicPanelAspectSync : MonoBehaviour
{
    static readonly int AspectId = Shader.PropertyToID("_Aspect");

    RawImage rawImage;
    int lastWidth;
    int lastHeight;

    void OnEnable()
    {
        rawImage = GetComponent<RawImage>();
        Sync(force: true);
    }

    void Update()
    {
        Sync(force: false);
    }

    // Writes straight to the shared material rather than an instanced copy —
    // this compositor material only ever has this one RawImage as a consumer.
    void Sync(bool force)
    {
        var material = rawImage.material;
        if (material == null)
            return;

        int width = Screen.width;
        int height = Screen.height;
        if (!force && width == lastWidth && height == lastHeight)
            return;

        lastWidth = width;
        lastHeight = height;
        if (height > 0)
            material.SetFloat(AspectId, (float)width / height);
    }
}
