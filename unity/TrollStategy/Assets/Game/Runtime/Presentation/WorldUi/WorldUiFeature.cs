using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TrollStrategy.Presentation.WorldUi
{
    /// <summary>
    /// Renderer feature of the colony renderer that draws the world labels (<see cref="WorldPanel"/>, on
    /// <see cref="WorldPanel.Layer"/>) after post-processing, over the finished frame. The renderer's own opaque and
    /// transparent passes leave that layer out.
    /// <para>World labels write no depth: in the transparent pass, depth of field took the depth behind a label and
    /// blurred a price that stands over the far clouds. Here no effect touches them, so they also keep the colours of
    /// the theme as the screen HUD does. They draw without a depth test, over the models, as screen text would; owners
    /// place them clear of the models anyway.</para>
    /// </summary>
    public sealed class WorldUiFeature : ScriptableRendererFeature
    {
        public const RenderPassEvent Event = RenderPassEvent.AfterRenderingPostProcessing;

        private WorldUiPass _pass;

        public override void Create()
        {
            _pass = new WorldUiPass { renderPassEvent = Event };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;
            renderer.EnqueuePass(_pass);
        }

        private sealed class WorldUiPass : ScriptableRenderPass
        {
            private static readonly List<ShaderTagId> Tags = new()
            {
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly")
            };

            private FilteringSettings _filtering = new(RenderQueueRange.transparent, 1 << WorldPanel.Layer);

            public WorldUiPass()
            {
                profilingSampler = new ProfilingSampler("World Labels");
            }

            private sealed class PassData
            {
                public RendererListHandle list;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lights = frameData.Get<UniversalLightData>();
                var drawing = RenderingUtils.CreateDrawingSettings(Tags, rendering, cameraData, lights,
                    SortingCriteria.CommonTransparent);
                var list = renderGraph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, _filtering));

                using var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var data, profilingSampler);
                data.list = list;
                builder.UseRendererList(list);
                // colour only: after post-processing the frame is resolved, the multisampled depth no longer matches it
                builder.SetRenderAttachment(resources.activeColorTexture, 0);
                builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                    context.cmd.DrawRendererList(pass.list));
            }
        }
    }
}
