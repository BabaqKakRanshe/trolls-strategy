// The island's air (IslandHazeFeature, settings in the IslandHaze volume component), one full-screen pass before
// URP's post-processing. Height fog: every surface below the lawn fades into the sky colour by its world height, so
// the rock pillars, the clouds under empty slots and the satellites' roots dissolve downwards whatever the zoom. Edge
// haze: the frame turns milky towards its corners, most at the bottom, a light vignette in place of URP's dark one. The
// camera background is left as it is: it already is the sky colour.
Shader "Hidden/TrollStrategy/IslandHaze"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }

        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "IslandHaze"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _HazeFogColor;     // rgb: linear colour
            float4 _HazeFogHeights;   // x: world height where the fog starts, y: 1 / (start - full), z: opacity
            float4 _HazeEdgeColor;    // rgb: linear colour
            float4 _HazeEdgeParams;   // x: radius where the edge haze starts, y: 1 / (full - start), z: intensity,
                                      // w: share of it left at the top edge of the frame

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);

            #if UNITY_REVERSED_Z
                float depth = SampleSceneDepth(uv);
                bool background = depth <= 0.0;
            #else
                float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, SampleSceneDepth(uv));
                bool background = depth >= 1.0;
            #endif
                if (!background)
                {
                    float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                    float fog = saturate((_HazeFogHeights.x - world.y) * _HazeFogHeights.y);
                    fog = fog * fog * (3.0 - 2.0 * fog) * _HazeFogHeights.z;
                    color.rgb = lerp(color.rgb, _HazeFogColor.rgb, fog);
                }

                // 0 in the middle of the frame, 1 in its corners (an ellipse with the frame's proportions)
                float radius = length(uv - 0.5) * 1.41421356;
                float edge = saturate((radius - _HazeEdgeParams.x) * _HazeEdgeParams.y);
                edge = edge * edge * (3.0 - 2.0 * edge) * _HazeEdgeParams.z;
                // heavier at the bottom of the frame, where the island sinks into the air, than over the sky on top.
                // Which way uv.y runs on screen depends on the platform: the view ray through the lower point of the
                // middle column dips further down
                float upper = ComputeWorldSpacePosition(float2(0.5, 0.25), UNITY_NEAR_CLIP_VALUE, UNITY_MATRIX_I_VP).y;
                float lower = ComputeWorldSpacePosition(float2(0.5, 0.75), UNITY_NEAR_CLIP_VALUE, UNITY_MATRIX_I_VP).y;
                float down = upper >= lower ? uv.y : 1.0 - uv.y;    // 0 at the top edge, 1 at the bottom
                edge *= lerp(_HazeEdgeParams.w, 1.0, down);
                color.rgb = lerp(color.rgb, _HazeEdgeColor.rgb, edge);
                return color;
            }
            ENDHLSL
        }
    }
}
