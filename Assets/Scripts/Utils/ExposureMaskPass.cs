using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
public class ExposureMaskPass : CustomPass
{
    public RenderTexture OutputRenderTexture;
    public Material Material;
    public LayerMask outlineLayer;

    protected override bool executeInSceneView => true;
    protected override void Execute(CustomPassContext ctx)
    {
        CoreUtils.SetRenderTarget(ctx.cmd, OutputRenderTexture, ClearFlag.None);
        CustomPassUtils.DrawRenderers(ctx, outlineLayer, overrideMaterial: Material);

        //if (OutputRenderTexture != null)
        //{
        //    var scale = RTHandles.rtHandleProperties.rtHandleScale;
        //    ctx.cmd.Blit(ctx.customColorBuffer.Value, OutputRenderTexture, new Vector2(scale.x, scale.y), Vector2.zero, 0, 0);
        //    // Notify Unity that the color buffer content changed.
        //    OutputRenderTexture.IncrementUpdateCount();
        //}
        OutputRenderTexture.IncrementUpdateCount();
    }
    protected override void Cleanup()
    {
        base.Cleanup();
    }
}