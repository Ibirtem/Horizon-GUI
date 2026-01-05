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
                float2 texcoord  : TEXCOORD0;
                float4 grabPos   : TEXCOORD1;
                float4 worldPosition : TEXCOORD2;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            sampler2D _BackgroundTexture;
            float4 _BackgroundTexture_TexelSize;
            float _BlurSize;
            float4 _ClipRect;

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
                half4 blurredCol = float4(0,0,0,0);
                float2 offset = _BackgroundTexture_TexelSize.xy * _BlurSize;
                
                // 3x3 Box/Gaussian Hybrid Kernel
                blurredCol += tex2Dproj(_BackgroundTexture, IN.grabPos) * 0.22;

                blurredCol += tex2Dproj(_BackgroundTexture, IN.grabPos + float4(offset.x, 0, 0, 0)) * 0.13;
                blurredCol += tex2Dproj(_BackgroundTexture, IN.grabPos + float4(-offset.x, 0, 0, 0)) * 0.13;
                blurredCol += tex2Dproj(_BackgroundTexture, IN.grabPos + float4(0, offset.y, 0, 0)) * 0.13;
                blurredCol += tex2Dproj(_BackgroundTexture, IN.grabPos + float4(0, -offset.y, 0, 0)) * 0.13;

                blurredCol += tex2Dproj(_BackgroundTexture, IN.grabPos + float4(offset.x, offset.y, 0, 0)) * 0.065;
                blurredCol += tex2Dproj(_BackgroundTexture, IN.grabPos + float4(-offset.x, -offset.y, 0, 0)) * 0.065;
                blurredCol += tex2Dproj(_BackgroundTexture, IN.grabPos + float4(offset.x, -offset.y, 0, 0)) * 0.065;
                blurredCol += tex2Dproj(_BackgroundTexture, IN.grabPos + float4(-offset.x, offset.y, 0, 0)) * 0.065;

                half4 sharpCol = tex2Dproj(_BackgroundTexture, IN.grabPos);

                float depthFactor = smoothstep(0.0, 1.0, IN.grabPos.w);

                float mixFactor = lerp(0.4, 1.0, depthFactor);

                half4 bgResult = lerp(sharpCol, blurredCol, mixFactor);

                half4 mainTex = tex2D(_MainTex, IN.texcoord);
                half4 finalColor = lerp(bgResult, IN.color, IN.color.a);
                
                finalColor.a = mainTex.a; 
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                
                return finalColor;
            }
            ENDCG
        }
    }
}