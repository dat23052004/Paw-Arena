Shader "PawArena/Cauldron/Water"
{
    Properties
    {
        _ShallowColor("Shallow magenta", Color) = (1,.025,1,1)
        _DeepColor("Deep violet-magenta", Color) = (.88,.008,.98,1)
        _FoamColor("Foam tint", Color) = (.95,.55,1,1)
        _Radius("Surface radius", Float) = 18
        _WaveHeight("Wave height", Range(0,.25)) = .11
        _Refraction("Refraction", Range(0,.06)) = .009
        _Absorption("Depth absorption", Range(.05,3)) = .32
        _Smoothness("Smoothness", Range(0,1)) = .9
        _NormalStrength("Ripple normal strength", Range(.1,6)) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            Blend One Zero
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor;
                float _Radius, _WaveHeight, _Refraction, _Absorption, _Smoothness, _NormalStrength;
            CBUFFER_END
            float4 _Ripples[8];
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 localXZ : TEXCOORD1;
                half fog : TEXCOORD2;
            };
            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx)*.1031);
                q += dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }
            // Quintic value noise with an analytic gradient: no straight repeating sine bands.
            float3 NoiseGradient(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 u = f*f*f*(f*(f*6-15)+10);
                float2 du = 30*f*f*(f*(f-2)+1);
                float a = Hash(cell);
                float b = Hash(cell+float2(1,0));
                float c = Hash(cell+float2(0,1));
                float d = Hash(cell+1);
                float k = a-b-c+d;
                return float3(a+(b-a)*u.x+(c-a)*u.y+k*u.x*u.y,
                    ((b-a)+k*u.y)*du.x,((c-a)+k*u.x)*du.y);
            }
            // x = height, yz = surface slope. Separate drifting scales keep the liquid irregular.
            float3 WaterField(float2 p)
            {
                float t = _Time.y;
                float2x2 rotation = float2x2(.8,-.6,.6,.8);
                float3 broad = NoiseGradient(p*.29+float2(t*.035,-t*.025));
                float3 detail = NoiseGradient(mul(rotation,p)*.73+float2(-t*.055,t*.04)+13.7);
                float height = (broad.x*.7+detail.x*.3-.5)*_WaveHeight;
                float2 slope = (broad.yz*.29*.7+mul(transpose(rotation),detail.yz)*.73*.3)*_WaveHeight;
                [unroll] for (int i=0;i<8;i++)
                {
                    float age = t-_Ripples[i].z;
                    if (_Ripples[i].w > 0 && age >= 0 && age < 5)
                    {
                        float2 delta = p-_Ripples[i].xy;
                        float distance = max(length(delta),.001);
                        float front = distance-age*2.8;
                        float envelope = exp(-front*front*1.1-age*.7)*_Ripples[i].w*.16;
                        height += sin(front*7)*envelope;
                        slope += delta/distance*envelope*(7*cos(front*7)-2.2*front*sin(front*7));
                    }
                }
                float edge = 1-smoothstep(_Radius*.94,_Radius,length(p));
                return float3(height,slope)*edge;
            }
            float EyeDepth(float rawDepth)
            {
                return unity_OrthoParams.w > .5 ? LinearDepthToEyeDepth(rawDepth) : LinearEyeDepth(rawDepth,_ZBufferParams);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.localXZ = input.positionOS.xz;
                input.positionOS.y += WaterField(input.positionOS.xz).x;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.localXZ;
                float3 field = WaterField(p);
                float3 micro = NoiseGradient(p*1.1+float2(_Time.y*.07,_Time.y*.035)+41.3);
                float2 slope = (field.yz+micro.yz*.01)*_NormalStrength;
                half3 normal = normalize(TransformObjectToWorldNormal(float3(-slope.x,1,-slope.y)));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float eye = -TransformWorldToView(input.positionWS).z;
                float sceneEye = EyeDepth(SampleSceneDepth(uv));
                float thickness = sceneEye-eye;
                // A missing/foreground depth sample must not turn the entire pool into contact foam.
                float validDepth = step(.001,thickness)*step(sceneEye,_ProjectionParams.z*.999);
                float depth = lerp(8,min(max(thickness,0),20),validDepth);
                float2 normalVS = mul((float3x3)GetWorldToViewMatrix(),normal).xy;
                float2 distorted = saturate(uv + normalVS*_Refraction*saturate(depth));
                float distortedEye = EyeDepth(SampleSceneDepth(distorted));
                // Do not bend foreground characters over the surface.
                if (distortedEye <= eye) { distorted = uv; }
                half3 background = SampleSceneColor(distorted);
                half absorption = 1-exp(-depth*_Absorption);
                half3 tint = lerp(_ShallowColor.rgb,_DeepColor.rgb,absorption);
                // Keep the reference's saturated potion color; refraction only reveals nearby submerged objects.
                half transmission = (1-absorption)*.35*validDepth;
                half3 color = lerp(tint,background*lerp(half3(1,1,1),tint,.35),transmission);
                color *= .96+NoiseGradient(p*.16+float2(_Time.y*.015,0)).x*.08;
                half fresnel = .025+.65*pow(1-saturate(dot(normal,view)),5);
                half3 reflected = reflect(-view,normal);
                half3 environment = GlossyEnvironmentReflection(reflected,1-_Smoothness,1);
                // A probe is preferred. The no-skybox preview still needs a non-uniform sky reflection,
                // otherwise even correct animated normals read as a completely flat color.
                float2 skyUV = reflected.xz/max(reflected.y,.25);
                float clouds = NoiseGradient(skyUV*2.3+float2(.8,2.1)).x;
                float cloudDetail = NoiseGradient(skyUV*4.7+float2(6.2,1.3)).x;
                float skyPatch = smoothstep(.15,.9,clouds*.8+cloudDetail*.2);
                half3 fallbackSky = lerp(half3(.24,.32,.5),half3(1.8,1.65,2),skyPatch);
                environment = max(environment,fallbackSky);
                color = lerp(color,environment,fresnel);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 halfway = normalize(view+light.direction);
                half nDotH = saturate(dot(normal,halfway));
                half specular = pow(nDotH,lerp(80,320,_Smoothness))*.5+pow(nDotH,24)*.12;
                color += light.color*specular*light.shadowAttenuation;
                float shore = smoothstep(_Radius-.18,_Radius,length(p));
                float contact = (1-smoothstep(.025,.14,depth))*validDepth;
                float foamNoise = NoiseGradient(p*1.1+float2(_Time.y*.04,0)).x;
                float foam = max(shore,contact*.4)*smoothstep(.32,.8,foamNoise);
                color = lerp(color,_FoamColor.rgb,saturate(foam*.3));
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
    }
}
