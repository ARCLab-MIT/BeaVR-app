Shader "WebRTC/ExternalTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100
        
        // No culling or depth write for simple UI/Video rendering
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            // ---------------------------------------------------------
            // ANDROID OES SUPPORT
            // ---------------------------------------------------------
            // We only use OES extension on Android. 
            // In Editor, we fallback to standard 2D sampling.
            #if defined(SHADER_API_GLES3) && !defined(UNITY_EDITOR)
                #extension GL_OES_EGL_image_external : require
                #extension GL_OES_EGL_image_external_essl3 : enable
            #endif

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            // Define the texture and transform
            // Note: We don't use sampler2D on Android for the OES texture
            #if defined(SHADER_API_GLES3) && !defined(UNITY_EDITOR)
                uniform samplerExternalOES _MainTex;
            #else
                sampler2D _MainTex;
            #endif
            
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Apply texture scale/offset (Unity tiling)
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // FLIP FIX: Android OES textures often come in upside down compared to Unity UI
                #if defined(SHADER_API_GLES3) && !defined(UNITY_EDITOR)
                    o.uv.y = 1.0 - o.uv.y;
                #endif

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the texture based on platform
                #if defined(SHADER_API_GLES3) && !defined(UNITY_EDITOR)
                    float4 col = tex2D(_MainTex, i.uv); 
                    // Note: In strict GLSL, this might look like texture(_MainTex, i.uv)
                    // but Unity's HLSL compiler often maps tex2D to the correct intrinsic 
                    // if the sampler is defined as samplerExternalOES.
                #else
                    float4 col = tex2D(_MainTex, i.uv);
                #endif

                return col;
            }
            ENDCG
        }
    }
}