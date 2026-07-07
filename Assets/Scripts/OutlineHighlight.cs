using UnityEngine;
using System.Collections.Generic;

public class OutlineHighlight : MonoBehaviour
{
    [SerializeField] private Color outlineColor = Color.yellow;
    [SerializeField] private float outlineWidth = 0.02f;
    [SerializeField] private float glowIntensity = 1.5f;

    private Renderer rend;
    private Material outlineMaterial;
    private Material originalMaterial;
    private List<Material> originalMaterials = new List<Material>();

    private void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null && rend.materials.Length > 0)
        {
            originalMaterial = rend.materials[0];
        }
    }

    public void EnableOutline()
    {
        if (rend == null) return;

        foreach (var mat in rend.materials)
        {
            originalMaterials.Add(new Material(mat));
        }

        outlineMaterial = new Material(Shader.Find("Standard"));
        outlineMaterial.color = new Color(outlineColor.r, outlineColor.g, outlineColor.b, 0.3f);
        outlineMaterial.SetFloat("_Glossiness", 0.8f);
        outlineMaterial.SetFloat("_Metallic", 0.5f);

        Material[] mats = new Material[rend.materials.Length + 1];
        for (int i = 0; i < rend.materials.Length; i++)
        {
            mats[i] = rend.materials[i];
        }
        mats[mats.Length - 1] = outlineMaterial;
        rend.materials = mats;
    }

    public void DisableOutline()
    {
        if (rend == null || originalMaterials.Count == 0) return;

        Material[] mats = new Material[originalMaterials.Count];
        for (int i = 0; i < originalMaterials.Count; i++)
        {
            mats[i] = originalMaterials[i];
        }
        rend.materials = mats;
        originalMaterials.Clear();
    }
}
