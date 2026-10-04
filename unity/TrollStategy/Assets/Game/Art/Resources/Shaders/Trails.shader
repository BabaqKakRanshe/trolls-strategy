// Trails on the colony's lawn (TrailView). One quad in the map plane just over the lawn, UV 0..1 across the grid.
// _Wear holds one texel per cell (R: wear 0..1 of the most a cell can take). Every cell round the pixel adds a soft
// round blot of its wear; where the blots of a trodden line add up past a stage's mark the stage shows, so a line
// of cells reads as one smooth stripe that widens as it is trodden: lighter trodden grass round it, an earth path
// inside, and on a road a light beaten middle. Value noise frays the edges.
// The quad multiplies what is already drawn (2 x src x dst), so the lit lawn keeps its light, shade and shadows and
// the tints only shift its colour; 0.5 leaves the lawn as it is.
Shader "TrollStrategy/Trails"
{
    Properties
    {
        [NoScaleOffset] _Wear ("Wear (R: wear of the cell)", 2D) = "black" {}
        _GridSize ("Cells X, Cells Y", Vector) = (40, 40, 0, 0)
        _Stages ("Trampled, Path, Road (wear 0..1)", Vector) = (0.1, 0.4, 0.8, 0)
        // multipliers in the frame's colour space, set by TrailView; vectors, so nothing converts them as colours
        _TrampledTint ("Trampled Grass Tint", Vector) = (0.62, 0.52, 0.8, 1)
        _PathTint ("Path Tint", Vector) = (0.93, 0.31, 1, 1)
        _RoadTint ("Road Tint", Vector) = (1, 0.48, 1, 1)
        _Falloff ("Blot Falloff (1 / 2 sigma^2, per cell^2)", Range(0.5, 8)) = 2
        _HalfWidth ("Half Width at a Stage's Mark (cells)", Range(0.05, 0.6)) = 0.3
        _TrampledStrength ("Trampled Strength", Range(0, 1)) = 0.55
        _EdgeNoise ("Edge Noise", Range(0, 0.4)) = 0.18
        _Speckle ("Path Speckle", Range(0, 0.3)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-60"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend DstColor SrcColor
        ZWrite Off
        ZTest LEqual
        Cull Off
        Offset -1, -1

        Pass
        {
            Name "Trails"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Wear);

            CBUFFER_START(UnityPerMaterial)
                float4 _GridSize;
                float4 _Stages;
                float4 _TrampledTint;
                float4 _PathTint;
                float4 _RoadTint;
                float _Falloff;
                float _HalfWidth;
                half _TrampledStrength;
                half _EdgeNoise;
                half _Speckle;
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

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // smooth value noise, 0..1
            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                int2 size = (int2)_GridSize.xy;
                float2 cell = input.uv * _GridSize.xy;
                int2 home = (int2)floor(cell);

                // blots of the 3x3 cells round the pixel; their sum draws the stripe
                float field = 0.0;
                [unroll] for (int dy = -1; dy <= 1; dy++)
                {
                    [unroll] for (int dx = -1; dx <= 1; dx++)
                    {
                        int2 c = clamp(home + int2(dx, dy), int2(0, 0), size - 1);
                        float inside = all(home + int2(dx, dy) == c) ? 1.0 : 0.0;
                        float wear = LOAD_TEXTURE2D(_Wear, c).r * inside;
                        float2 d = cell - (float2(c) + 0.5);
                        float r2 = dot(d, d);
                        field += wear * exp(-_Falloff * r2);
                    }
                }

                // a straight line of cells at wear w adds up to w * lineSum in its middle; trodden grass and the
                // path show where the sum passes the value a line at their mark has _HalfWidth cells off its middle,
                // the road's beaten middle only where a line at the road's mark peaks
                float lineSum = sqrt(PI / _Falloff);
                float atEdge = lineSum * exp(-_Falloff * _HalfWidth * _HalfWidth);
                float fray = 1.0 + (Noise(cell * 2.3) * 0.65 + Noise(cell * 6.1) * 0.35 - 0.5) * 2.0 * _EdgeNoise;
                field *= fray;

                float trampled = smoothstep(atEdge * _Stages.x, atEdge * _Stages.x * 1.8, field);
                float path = smoothstep(atEdge * _Stages.y * 0.9, atEdge * _Stages.y * 1.1, field);
                float road = smoothstep(lineSum * _Stages.z * 0.9, lineSum * _Stages.z, field) * path;

                half3 tint = half3(0.5, 0.5, 0.5);
                tint = lerp(tint, _TrampledTint.rgb, trampled * _TrampledStrength);
                tint = lerp(tint, _PathTint.rgb, path);
                tint = lerp(tint, _RoadTint.rgb, road);
                // grit in the earth: darker specks on the path, fewer on the beaten road
                float speck = smoothstep(0.72, 0.9, Noise(cell * 11.0)) * _Speckle;
                tint *= 1.0 - speck * path * (1.0 - road * 0.6);

                tint = MixFogColor(tint, half3(0.5, 0.5, 0.5), input.fog);
                return half4(tint, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
