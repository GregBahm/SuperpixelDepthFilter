Shader "Unlit/OfflineAltOutputShader"
{
	Properties
	{
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

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

			struct CellBasisData
			{
				float3 Color;
				float2 Pos;
			};

			struct CellAlphaData
			{
				uint TotalAlpha;
				uint PixelCount;
			};
			
			sampler2D _SourceTexture;
			sampler2D _DigitalTexture;
			sampler2D _AlphaTexture;
			
			float _SourceImageWidth;
			float _SourceImageHeight;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			
			#define SearchRadius 5
			
			float3 GetAltAlpha(float alpha, float2 uv)
			{
				float3 whiteTotal;
				float whiteSum;
				float3 blackTotal;
				float blackSum;
			
				float xIncrement = (float)1 / _SourceImageWidth;
				float yIncrement = (float)1 / _SourceImageHeight;
				for(int x = -SearchRadius; x < SearchRadius; x++)
				{					
					for(int y = -SearchRadius; y < SearchRadius; y++)
					{
						float2 newUvs = uv + float2(xIncrement * x, yIncrement * y);
						float3 texSample = tex2D(_SourceTexture, newUvs).rgb;
						float alphaTex = tex2D(_AlphaTexture, newUvs).x;
						
						whiteTotal += texSample * alphaTex;
						whiteSum += alphaTex;
						blackTotal += texSample * (1 - alphaTex);
						blackSum += (1 - alphaTex);
					}
				}
				
				float3 whiteAverage = whiteTotal / whiteSum;
				float3 blackAverage = blackTotal / blackSum;
				float dist = length(whiteAverage - blackAverage);
				return whiteAverage;
				alpha -= .5f;
				alpha *= dist * 10;
				alpha += .5f;
				return alpha;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				float4 sourceTex = tex2D(_SourceTexture, i.uv);
				float4 digitalTex = tex2D(_DigitalTexture, i.uv);
				float alphaTex = tex2D(_AlphaTexture, i.uv).x;
				return float4(GetAltAlpha(alphaTex, i.uv), 1);
				//float altAlpha = GetAltAlpha(alphaTex, i.uv); 
				//return altAlpha;
				//return lerp(digitalTex, sourceTex, altAlpha);
			}
			ENDCG
		}
	}
}
