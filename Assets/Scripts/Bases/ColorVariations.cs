using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorVariations : MonoBehaviour
{
    private static Dictionary<Material, Material[]> variations = new();
    private const int variationsNum = 8;
    // posun vzoru v object-space; *Scale shaderu => jiná oblast voronoi.
    // ~50 stačí na výrazně jiný vzhled, není tak velký, aby trpěla přesnost sin-hashe.
    private const float offsetRadius = 50f;
    private static readonly int PatternOffsetId = Shader.PropertyToID("_PatternOffset");

    void Start()
    {
        var renderer = GetComponent<Renderer>();
        renderer.sharedMaterial = GetMaterialVariation(renderer.sharedMaterial);
    }

    private Material GetMaterialVariation(Material sharedMaterial)
    {
        if (!variations.TryGetValue(sharedMaterial, out var materials))
        {
            materials = CreateVariations(sharedMaterial);
            variations[sharedMaterial] = materials;
        }
        return materials[Random.Range(0, materials.Length)];
    }

    private Material[] CreateVariations(Material sharedMaterial)
    {
        Material[] materials = new Material[variationsNum];
        for (int i = 0; i < variationsNum; i++)
        {
            var m = new Material(sharedMaterial);

            Color.RGBToHSV(m.color, out var h, out var s, out var v);
            var color = Random.ColorHSV(Lo(h), Hi(h), Lo(s), Hi(s), Lo(v), Hi(v));
            m.color = color;

            // procedurální materiály (žula): posuň vzor, ať je každá varianta jiná
            if (m.HasProperty(PatternOffsetId))
                m.SetVector(PatternOffsetId, Random.insideUnitSphere * offsetRadius);

            materials[i] = m;
        }
        return materials;
    }

    private static float Lo(float a) => Mathf.Clamp01(a - 0.05f);
    private static float Hi(float a) => Mathf.Clamp01(a + 0.05f);
}
