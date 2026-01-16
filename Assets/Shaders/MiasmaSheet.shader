Shader "Custom/MiasmaSheet"
{
    Properties
    {
        _Color ("Miasma Color", Color) = (0.5, 0, 0.7, 0.9)
        _ClearedTex ("Cleared Tiles Texture", 2D) = "white" {}
        _TileSize ("Tile Size", Float) = 0.25
        _SheetOrigin ("Sheet Origin", Vector) = (0, 0, 0, 0)
        _SheetSize ("Sheet Size", Vector) = (10, 10, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
                float3 worldPos : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            fixed4 _Color;
            sampler2D _ClearedTex;
            float4 _ClearedTex_ST;
            float _TileSize;
            float4 _SheetOrigin;
            float2 _SheetSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                // Calculate world position
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldPos = worldPos;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Use UV coordinates (0-1) to sample texture directly
                // UV goes from 0,0 (bottom-left) to 1,1 (top-right)
                float2 texCoord = i.uv;
                
                // Sample cleared texture (1 = cleared, 0 = has miasma)
                float cleared = tex2D(_ClearedTex, texCoord).r;
                
                // If cleared, make transparent. Otherwise show miasma color.
                fixed4 col = _Color;
                col.a *= (1.0 - cleared);
                
                return col;
            }
            ENDCG
        }
    }
}
