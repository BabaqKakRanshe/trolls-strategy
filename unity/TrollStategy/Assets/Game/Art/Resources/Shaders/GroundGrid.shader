// The cell grid over the colony's land (GroundGridView). One quad in the map plane, UV 0..1 across the whole grid.
// Faint cell lines with bright crosses where they meet, warm block borders, a soft glow and a slow sheen; lines keep
// their width in pixels and fade out before they turn to moire. _LandMask (one texel per cell, bilinear) hides the
// grid off cleared land; _Pointer lights it up round the cursor, _Emphasis while the player places something.
Shader "TrollStrategy/GroundGrid"
{
    Properties
    {
        [NoScaleOffset] _LandMask ("Land Mask (R: open cell)", 2D) = "white" {}
        _GridSize ("Cells X, Cells Y, Block Cells", Vector) = (40, 40, 5, 0)
        _LineColor ("Line", Color) = (0.91, 0.84, 0.7, 1)
        _BlockColor ("Block Border", Color) = (0.91, 0.76, 0.44, 1)
        _GlowColor ("Glow", Color) = (1, 0.96, 0.82, 1)
        _LineWidth ("Line Width (px)", Range(0.5, 4)) = 1.2
        _BlockWidth ("Block Border Width (px)", Range(0.5, 6)) = 2.2
        _CrossSize ("Cross Arm (cells)", Range(0, 0.5)) = 0.16
        _GlowWidth ("Glow Width (px)", Range(0, 24)) = 7
        _Opacity ("Opacity at Rest", Range(0, 1)) = 0.55
        _ActiveOpacity ("Opacity while Placing", Range(0, 1)) = 1
        _Emphasis ("Emphasis", Range(0, 1)) = 0
        _Pointer ("Pointer (cell x, cell y, radius in cells, strength)", Vector) = (0, 0, 4.5, 0)
        _SheenPeriod ("Sheen Period (s)", Float) = 9
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-50"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off
        Offset -1, -1

        Pass
        {
            Name "GroundGrid"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_LandMask);
            SAMPLER(sampler_LandMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _GridSize;
                half4 _LineColor;
                half4 _BlockColor;
                half4 _GlowColor;
                float _LineWidth;
                float _BlockWidth;
                float _CrossSize;
                float _GlowWidth;
                half _Opacity;
                half _ActiveOpacity;
                half _Emphasis;
                float4 _Pointer;
                float _SheenPeriod;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fog : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // coverage of a line of the given width (px) at a distance (px) from its middle, anti-aliased over 1 px
            float Line(float distancePx, float widthPx)
            {
                return saturate(widthPx * 0.5 + 0.5 - distancePx);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 cell = input.uv * _GridSize.xy;
                float2 perPx = max(fwidth(cell), 1e-5);            // cells per pixel on each axis
                float cellPx = 1.0 / max(perPx.x, perPx.y);        // pixels per cell, the coarser axis

                // cell lines and crosses where they meet: distance to the nearest line, in cells and in pixels
                float2 toLine = abs(frac(cell + 0.5) - 0.5);
                float2 toLinePx = toLine / perPx;
                float minor = max(Line(toLinePx.x, _LineWidth), Line(toLinePx.y, _LineWidth));
                float arm = _CrossSize;
                float cross = max(
                    Line(toLinePx.x, _LineWidth * 1.6) * (1.0 - smoothstep(arm * 0.55, arm, toLine.y)),
                    Line(toLinePx.y, _LineWidth * 1.6) * (1.0 - smoothstep(arm * 0.55, arm, toLine.x)));

                // block borders every _GridSize.z cells
                float blockCells = max(_GridSize.z, 1.0);
                float2 toBlockPx = abs(frac(cell / blockCells + 0.5) - 0.5) * blockCells / perPx;
                float toBlock = min(toBlockPx.x, toBlockPx.y);
                float block = Line(toBlock, _BlockWidth);

                // fine lines leave before they get denser than the pixels; block borders hold out longer
                float minorLod = smoothstep(4.0, 10.0, cellPx);
                float blockLod = smoothstep(3.0, 8.0, cellPx * blockCells);

                float glowWidth = max(_GlowWidth, 1e-3);
                float glow = exp(-toBlock * toBlock / (glowWidth * glowWidth)) * 0.22 * blockLod
                           + exp(-4.0 * min(toLinePx.x, toLinePx.y) * min(toLinePx.x, toLinePx.y) / (glowWidth * glowWidth))
                             * 0.08 * minorLod;

                // round the pointer: brighter lines and a soft fill in the cell under it
                float toPointer = distance(cell, _Pointer.xy);
                float spot = _Pointer.w * (1.0 - smoothstep(_Pointer.z * 0.2, _Pointer.z, toPointer));
                float hovered = all(floor(cell) == floor(_Pointer.xy)) ? 1.0 : 0.0;

                // a slow diagonal band of light that runs over the land
                float phase = dot(cell, float2(0.55, 0.35)) / 36.0 - _Time.y / max(_SheenPeriod, 0.1);
                float sheen = pow(saturate(0.5 + 0.5 * sin(phase * TWO_PI)), 24.0);

                float lines = max(max(minor * 0.32, cross) * minorLod, block * blockLod);
                float light = 1.0 + spot * (1.0 + _Emphasis) + sheen * 0.7;
                float mask = smoothstep(0.45, 0.95, SAMPLE_TEXTURE2D(_LandMask, sampler_LandMask, input.uv).r);
                float opacity = lerp(_Opacity, _ActiveOpacity, _Emphasis);

                float alpha = saturate((lines + glow) * light * opacity);
                alpha = saturate(alpha + hovered * spot * lerp(0.05, 0.14, _Emphasis)) * mask;

                half3 color = lerp(_LineColor.rgb, _BlockColor.rgb, saturate(block * blockLod + glow * 2.0));
                color = lerp(color, _GlowColor.rgb, saturate(spot * 0.6 + sheen * 0.6));
                color = MixFog(color, input.fog);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
