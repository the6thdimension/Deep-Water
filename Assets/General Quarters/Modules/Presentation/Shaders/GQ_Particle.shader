// Deep Water — General Quarters: particle shader for HDRP (slice 6 presentation).
//
// Unlit, vertex-coloured, textured billboards for Shuriken particle systems: smoke, flame, flashes, tracers and spray.
// HDRP ships no particle shader for the built-in Particle System (the built-in "Particles/*" shaders draw as flat quads
// under HDRP), so this is a small hand-written one.
//   - Colours are display-relative: 1 is white on screen at any exposure, values above 1 bloom. A smoke colour of 0.7
//     reads as light grey in sun or haze alike, which suits an effect pass over a physically exposed scene.
//   - HDRP's fog (atmospheric scattering) is applied, so distant smoke fades into the haze like everything else.
//   - Soft particles: fade where a billboard meets geometry or the sea, so smoke has no hard line at the water.
//   - _SrcBlend/_DstBlend pick alpha blending (5, 10) or additive (5, 1; 1, 1 for flame).
Shader "DeepWater/General Quarters/Particle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint (HDR, display-relative)", Color) = (1, 1, 1, 1)
        _Brightness ("Brightness", Float) = 1
        _SoftDistance ("Soft particle distance (m)", Float) = 2
        _FogWeight ("Fog weight", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 10
    }

    HLSLINCLUDE
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/AtmosphericScattering/AtmosphericScattering.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint;
                float _Brightness;
                float _SoftDistance;
                float _FogWeight;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionRWS : TEXCOORD1;   // camera-relative world position (HDRP renders camera-relative)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 rws = TransformObjectToWorld(input.positionOS);
                o.positionRWS = rws;
                o.positionCS = TransformWorldToHClip(rws);
                o.color = input.color;
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 c = tex * input.color * _Tint;
                c.rgb *= _Brightness;

                // Soft particles: fade out as the billboard approaches what is already drawn behind it.
                float sceneDepth = LinearEyeDepth(LoadCameraDepth(uint2(input.positionCS.xy)), _ZBufferParams);
                float particleDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                c.a *= saturate((sceneDepth - particleDepth) / max(_SoftDistance, 1e-3));

                // HDRP fog, so smoke at range sits in the same haze as the ships.
                PositionInputs posInput = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, input.positionCS.z, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
                posInput.positionWS = input.positionRWS;
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionRWS);
                float3 fogColor, fogOpacity;
                EvaluateAtmosphericScattering(posInput, V, fogColor, fogOpacity);
                fogOpacity *= _FogWeight;
                bool additive = _DstBlend == 1;
                // Alpha-blended: blend toward the fog colour. Additive: fade the added light out with distance instead.
                c.rgb = additive ? c.rgb * (1 - fogOpacity) : lerp(c.rgb, fogColor, fogOpacity);

                c.a = saturate(c.a);
                if (additive) c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
