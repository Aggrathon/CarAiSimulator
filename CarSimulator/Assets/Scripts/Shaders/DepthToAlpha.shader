Shader "CameraAI/DepthToAlpha"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float2 scrPos : TEXCOORD1;
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata_base v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord;
				o.scrPos = ComputeScreenPos(o.vertex);
				return o;
			}
			
			sampler2D _MainTex;
			sampler2D _CameraDepthNormalsTexture;

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);

				float3 normalValues;
				float dist;
				DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, i.scrPos.xy), dist, normalValues);
				
				dist = clamp(1-dist * 3, 0.0, 1.0);
				float norm = clamp(normalValues.y - 0.4, 0.0, 1.0) *1.8;
				col.a = dist*dist*dist*dist*dist* norm*norm;
				//col.a = (col.a * 2 - 1)*(col.a * 2 - 1)*(col.a * 2 - 1)*0.5 + 0.5;
				return col;
			}
			ENDCG
		}
	}
}
