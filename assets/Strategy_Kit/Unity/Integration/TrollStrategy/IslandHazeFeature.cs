using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TrollStrategy.Presentation.Island
{
    /// <summary>
    /// Renderer feature of the colony renderer that draws <see cref="IslandHaze"/> (Hidden/TrollStrategy/IslandHaze):
    /// one full-screen pass over the camera colour before post-processing, reading the depth texture. It runs only for
    /// cameras with post-processing whose volumes turn the haze on, so the battle arena and the building showcase
    /// never get it.
    /// </summary>
    public sealed class IslandHazeFeature : ScriptableRendererFeature
    {
        public const string ShaderResource = "Shaders/IslandHaze";

        private Material _material;
        private HazePass _pass;

        public override void Create()
        {
            _pass = new HazePass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;
            if (!renderingData.cameraData.postProcessEnabled) return;
            var haze = VolumeManager.instance.stack.GetComponent<IslandHaze>();
            if (haze == null || !haze.IsActive()) return;
            if (_material == null)
            {
                var shader = Resources.Load<Shader>(ShaderResource);
                if (shader == null) return;
                _material = CoreUtils.CreateEngineMaterial(shader);
            }
            _pass.Setup(_material, haze);
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            _pass.requiresIntermediateTexture = true;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private sealed class HazePass : ScriptableRenderPass
        {
            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly int FogColorId = Shader.PropertyToID("_HazeFogColor");
            private static readonly int FogHeightsId = Shader.PropertyToID("_HazeFogHeights");
            private static readonly int EdgeColorId = Shader.PropertyToID("_HazeEdgeColor");
            private static readonly int EdgeParamsId = Shader.PropertyToID("_HazeEdgeParams");

            private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
            private Material _material;
            private Vector4 _fogColor, _fogHeights, _edgeColor, _edgeParams;

            public HazePass()
            {
                profilingSampler = new ProfilingSampler("Island Haze");
            }

            public void Setup(Material material, IslandHaze haze)
            {
                _material = material;
                // the shader mixes in linear space; the parameters hold colours as picked (sRGB), like the background
                Color fog = haze.fogColor.value.linear;
                _fogColor = new Vector4(fog.r, fog.g, fog.b, 1f);
                float start = haze.fogStart.value;
                float full = Mathf.Min(haze.fogFull.value, start - .01f);
                _fogHeights = new Vector4(start, 1f / (start - full), haze.fogOpacity.value, 0f);
                Color edge = haze.edgeColor.value.linear;
                _edgeColor = new Vector4(edge.r, edge.g, edge.b, 1f);
                Vector2 range = haze.edgeRange.value;
                _edgeParams = new Vector4(range.x, 1f / Mathf.Max(.01f, range.y - range.x), haze.edgeIntensity.value,
                    haze.edgeTop.value);
            }

            private sealed class PassData
            {
                public Material material;
                public MaterialPropertyBlock block;
                public TextureHandle source;
                public Vector4 fogColor, fogHeights, edgeColor, edgeParams;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                TextureHandle source = resources.activeColorTexture;
                var description = renderGraph.GetTextureDesc(source);
                description.name = "_IslandHazeColor";
                description.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(description);

                using (var builder =
                    renderGraph.AddRasterRenderPass<PassData>(passName, out var data, profilingSampler))
                {
                    data.material = _material;
                    data.block = _block;
                    data.source = source;
                    data.fogColor = _fogColor;
                    data.fogHeights = _fogHeights;
                    data.edgeColor = _edgeColor;
                    data.edgeParams = _edgeParams;
                    builder.UseTexture(source, AccessFlags.Read);
                    if (resources.cameraDepthTexture.IsValid())
                        builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                    {
                        RTHandle input = pass.source;
                        pass.block.Clear();
                        pass.block.SetTexture(BlitTextureId, input);
                        pass.block.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                        pass.block.SetVector(FogColorId, pass.fogColor);
                        pass.block.SetVector(FogHeightsId, pass.fogHeights);
                        pass.block.SetVector(EdgeColorId, pass.edgeColor);
                        pass.block.SetVector(EdgeParamsId, pass.edgeParams);
                        context.cmd.DrawProcedural(Matrix4x4.identity, pass.material, 0, MeshTopology.Triangles, 3, 1,
                            pass.block);
                    });
                }
                // the hazed frame is the camera colour from here on: post-processing reads it
                resources.cameraColor = destination;
            }
        }
    }
}
