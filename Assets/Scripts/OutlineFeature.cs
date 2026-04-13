using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OutlineFeature : ScriptableRendererFeature
{
    class OutlinePass : ScriptableRenderPass
    {
        private Material outlineMaterial;
        private RenderTargetIdentifier source;
        private RenderTargetHandle tempTexture;

        private FilteringSettings filteringSettings;
        private ShaderTagId shaderTagId = new ShaderTagId("UniversalForward");

        public OutlinePass(Material material, LayerMask layerMask)
        {
            outlineMaterial = material;
            filteringSettings = new FilteringSettings(RenderQueueRange.all, layerMask);
        }

        public void Setup(RenderTargetIdentifier src)
        {
            source = src;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            tempTexture.Init("_TemporaryColorTexture");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Outline Pass");

            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            cmd.GetTemporaryRT(tempTexture.id, descriptor);

            // 1. Render highlighted objects into texture
            Blit(cmd, source, tempTexture.Identifier(), outlineMaterial, 0);

            // 2. Output back
            Blit(cmd, tempTexture.Identifier(), source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    public Material outlineMaterial;
    public LayerMask layerMask;

    private OutlinePass pass;

    public override void Create()
    {
        pass = new OutlinePass(outlineMaterial, layerMask);
        pass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        pass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(pass);
    }
}
