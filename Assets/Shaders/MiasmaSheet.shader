Shader "Custom/MiasmaSheet"
{
    Properties
    {
        _Color ("Miasma Color", Color) = (0.5, 0, 0.7, 0.9)
        _ClearedMask ("Cleared Tiles Mask", 2D) = "black" {}
        _WorldToUV ("World To UV", Vector) = (1, 1, 0, 0)  // scaleX, scaleZ, offsetX, offsetZ
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
        }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

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
                float3 worldPos : TEXCOORD1;
            };

            fixed4 _Color;
            sampler2D _ClearedMask;
            float4 _ClearedMask_ST;
            float4 _WorldToUV;  // x=scaleX, y=scaleZ, z=offsetX, w=offsetZ

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                // Calculate world position for texture sampling
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Convert world position to texture UV coordinates
                // _WorldToUV: (scaleX, scaleZ, offsetX, offsetZ)
                float2 texUV;
                texUV.x = i.worldPos.x * _WorldToUV.x + _WorldToUV.z;
                texUV.y = i.worldPos.z * _WorldToUV.y + _WorldToUV.w;
                
                // Sample the cleared mask texture
                // White (1) = cleared (no fog), Black (0) = fog present
                fixed mask = tex2D(_ClearedMask, texUV).r;
                
                // Invert: mask 1 = no fog (alpha 0), mask 0 = fog (alpha 1)
                fixed alpha = 1.0 - mask;
                
                // Apply miasma color with calculated alpha
                fixed4 col = _Color;
                col.a *= alpha;
                
                return col;
            }
            ENDCG
        }
    }
}
