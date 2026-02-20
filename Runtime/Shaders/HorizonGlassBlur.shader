Shader "Horizon/UI/Glass Blur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,0.2)
        _BlurSize ("Blur Size", Range(0, 20)) = 8.0
        
        [Header(Stencil)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp] 
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        GrabPass { "_BackgroundTexture" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest
            
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 grabPos  : TEXCOORD1;
                float4 worldPosition : TEXCOORD2;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            sampler2D _BackgroundTexture;
            half4 _BackgroundTexture_TexelSize;
            float _BlurSize;
            float4 _ClipRect;

            static const float GOLDEN_ANGLE = 2.39996323;
            static const int ITERATIONS = 12;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                OUT.grabPos = ComputeGrabScreenPos(OUT.vertex);
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 screenUV = IN.grabPos.xy / IN.grabPos.w;

                float2 texelSize = _BackgroundTexture_TexelSize.xy;
                
                half4 accumColor = 0;
                
                float2x2 rot = float2x2(cos(GOLDEN_ANGLE), sin(GOLDEN_ANGLE), -sin(GOLDEN_ANGLE), cos(GOLDEN_ANGLE));

                float2 direction = float2(_BlurSize, 0.0);
                
                UNITY_UNROLL
                for (int i = 0; i < ITERATIONS; i++)
                {
                    float r = sqrt((float)i) / sqrt((float)ITERATIONS);
                    
                    direction = mul(rot, direction);
                    
                    float2 offset = direction * r * texelSize;
                    
                    accumColor += tex2D(_BackgroundTexture, screenUV + offset);
                }
                
                accumColor /= (float)ITERATIONS;

                half4 finalColor = lerp(accumColor, IN.color, IN.color.a);
                
                half4 mainTex = tex2D(_MainTex, IN.texcoord);
                finalColor.a = mainTex.a; 
                
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                
                return finalColor;
            }
            ENDCG
        }
    }
}