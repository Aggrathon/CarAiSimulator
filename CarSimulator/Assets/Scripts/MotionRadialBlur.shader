Shader "ImageEffect/MotionRadialBlur"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_BlurStrength("Blur Strength", Range(0.0,1.0)) = 0.5
		_BlurWidth("Blur Width", Range(0.0,1.0)) = 0.5
	}

	SubShader{
		ZTest Always Cull Off ZWrite Off
		Fog{ Mode off }

		Pass{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			uniform sampler2D _MainTex;
			uniform half _BlurStrength;
			uniform half _BlurWidth;

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

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}


			half4 frag(v2f i) : COLOR{
				half4 color = tex2D(_MainTex, i.uv);

				//vector to the middle of the screen
				half2 dir = half2(0.5, 0.6 * _ScreenParams.y / _ScreenParams.x) - i.uv;
				half dist = sqrt(dir.x*dir.x + dir.y*dir.y);
				dir = dir / dist;

				float scale = _ScreenParams.w - 0.5;
				half4 blur = (color + color
					+ tex2D(_MainTex, i.uv + dir * (-0.08) * _BlurWidth * scale)
					+ tex2D(_MainTex, i.uv + dir * (-0.06) * _BlurWidth * scale)
					+ tex2D(_MainTex, i.uv + dir * (-0.04) * _BlurWidth * scale)
					+ tex2D(_MainTex, i.uv + dir * (-0.03) * _BlurWidth * scale)
					+ tex2D(_MainTex, i.uv + dir * (-0.02) * _BlurWidth * scale)
					+ tex2D(_MainTex, i.uv + dir * (-0.01) * _BlurWidth * scale)
					+ tex2D(_MainTex, i.uv + dir * (0.01) * _BlurWidth  * scale)
					+ tex2D(_MainTex, i.uv + dir * (0.02) * _BlurWidth  * scale)
					+ tex2D(_MainTex, i.uv + dir * (0.03) * _BlurWidth  * scale)
					+ tex2D(_MainTex, i.uv + dir * (0.04) * _BlurWidth  * scale)
					+ tex2D(_MainTex, i.uv + dir * (0.06) * _BlurWidth  * scale)
					+ tex2D(_MainTex, i.uv + dir * (0.08) * _BlurWidth  * scale)
					) / 14.0;

				half t = dist * _BlurStrength * _ScreenParams.w;
				t = clamp(t, 0.0, 1.0);
				half4 c = lerp(color, blur, t*t);
				return c;
			}

			ENDCG
		}
	}
}
