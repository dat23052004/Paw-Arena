Shader "PawArena/Cauldron/Water Particle"
{
    Properties { _Ring("Bubble ring", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Ring;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.color=input.color;
                output.uv=input.uv;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2-1;
                half radius=length(p);
                half disc=1-smoothstep(.65,1,radius);
                half ring=smoothstep(.55,.78,radius)*(1-smoothstep(.82,1,radius));
                half highlight=pow(saturate(1-length(p-float2(-.3,.35))*2.5),3);
                return half4(input.color.rgb+highlight*.5,input.color.a*lerp(disc,ring+highlight*.35,_Ring));
            }
            ENDHLSL
        }
    }
}
