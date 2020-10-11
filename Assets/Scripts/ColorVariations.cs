using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorVariations : MonoBehaviour
{
    static readonly int colorPropertyId = Shader.PropertyToID("_BaseColor");
    static MaterialPropertyBlock sharedPropertyBlock;


    void Start()
    {
        var renderer = GetComponent<MeshRenderer>();
        Color.RGBToHSV(renderer.material.color, out var h, out var s, out var v);
        var color = Random.ColorHSV(Lo(h), Hi(h), Lo(s), Hi(s), Lo(v), Hi(v));

        if (sharedPropertyBlock == null)
        {
            sharedPropertyBlock = new MaterialPropertyBlock();
        }
        sharedPropertyBlock.SetColor(colorPropertyId, color);
        renderer.SetPropertyBlock(sharedPropertyBlock);
    }

    private static float Lo(float a) => Mathf.Clamp01(a - 0.05f);
    private static float Hi(float a) => Mathf.Clamp01(a + 0.05f);
}
