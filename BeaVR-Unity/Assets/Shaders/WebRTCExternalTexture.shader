Shader "WebRTC/ExternalTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    // ---------------------------------------------------------
    // SubShader 1: Android / Quest (OES Hardware Decoding)
    // ---------------------------------------------------------
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        // Only run this SubShader on GLES3 (Android/Quest)
        Pass
        {
            GLSLPROGRAM
            #pragma only_renderers gles3

            // OES Extensions must be at the very top
            #extension GL_OES_EGL_image_external : require
            #extension GL_OES_EGL_image_external_essl3 : enable

            // Unity standard includes for GLSL
            #include "UnityCG.glslinc"

            #ifdef VERTEX
            
            // Raw GLSL Vertex Inputs
            varying vec2 textureCoordinate;

            void main()
            {
                // gl_MultiTexCoord0 is the standard generic attribute for UVs in Unity GLSL
                textureCoordinate = gl_MultiTexCoord0.xy;
                
                // Flip Y for Android OES if needed (often required for WebRTC)
                textureCoordinate.y = 1.0 - textureCoordinate.y;

                // Standard vertex position transformation
                gl_Position = gl_ModelViewProjectionMatrix * gl_Vertex;
            }
            
            #endif

            #ifdef FRAGMENT
            
            // The OES Sampler (Key Fix)
            uniform samplerExternalOES _MainTex;
            varying vec2 textureCoordinate;

            void main()
            {
                // Must use generic texture2D or texture() depending on version, 
                // but with samplerExternalOES defined, the driver handles it.
                gl_FragColor = texture2D(_MainTex, textureCoordinate);
            }
            
            #endif

            ENDGLSL
        }
    }

    // ---------------------------------------------------------
    // SubShader 2: Editor / PC Fallback (Software Decoding)
    // ---------------------------------------------------------
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Standard 2D sampling for Editor
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}