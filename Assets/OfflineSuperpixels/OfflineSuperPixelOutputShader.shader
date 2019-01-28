Shader "Unlit/OfflineSuperPixelOutputShader"
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
			StructuredBuffer<CellBasisData> _CellBasisBuffer;

			sampler2D _SourceTexture;
			sampler2D _DigitalTexture;
			sampler2D _AlphaTexture;
			float _MaxDepth;
			float _DepthThreshold;
			float _Outlines;
			float _CellAlpha; 
			float _RawAlpha;
			float _Impressionist;
			int _SourceImageWidth;
			int _SourceImageHeight; 
			int _SuperpixelResolution;
			Buffer<int> _ResultsBuffer;

			Buffer<float2> _DepthMappings;
			Buffer<int> _DepthData;
			StructuredBuffer<CellAlphaData> _CellAlphasBuffer;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			float2 IndexToUvs(uint index) // Test code to delete
			{
				int y = (index / _SuperpixelResolution) % _SuperpixelResolution;
				int x = index % _SuperpixelResolution;
				float retX = (float)x / _SuperpixelResolution;
				float retY = (float)y / _SuperpixelResolution;
				return float2(retX, retY);
			}

			int UvsToSourceImageIndex(float2 uv) 
			{
				int x = uv.x * _SourceImageWidth;
				int y = uv.y * _SourceImageHeight;
				return x + y * _SourceImageWidth;
			}
			CellBasisData GetCell(float2 uv)
			{
				int pixelIndex = UvsToSourceImageIndex(uv);
				int cellIndex = _ResultsBuffer[pixelIndex];
				return _CellBasisBuffer[cellIndex];
			}

			float GetAlpha(float2 uv)
			{
				int pixelIndex = UvsToSourceImageIndex(uv);
				int cellIndex = _ResultsBuffer[pixelIndex];
				CellAlphaData cellDepth = _CellAlphasBuffer[cellIndex];
				
				float alpha = (float)cellDepth.TotalAlpha / cellDepth.PixelCount;
				return alpha / 255;
			}

			float GetOutlines(float3 sourceTex, v2f i)
			{
				float2 offsetUvs = i.uv + float2(.002, 0);
				float2 offsetUvsB = i.uv + float2(0, .002);
				float3 cellColor = GetCell(i.uv).Color;
				float3 offsetCellColorA = GetCell(offsetUvs).Color;
				float3 offsetCellColorB = GetCell(offsetUvsB).Color;
				float lengthChangeA = length(offsetCellColorA - cellColor);
				float lengthChangeB = length(offsetCellColorB - cellColor);
				float colorDiff = max(lengthChangeA, lengthChangeB);
				return colorDiff * 1000;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float3 sourceTex = tex2D(_SourceTexture, i.uv).rgb;
				float3 digitalTex = tex2D(_DigitalTexture, i.uv).rgb;
				float3 alphaTex = tex2D(_AlphaTexture, i.uv).rgb;
				float3 impressionistTex = GetCell(i.uv).Color;
				float cellAlpha = GetAlpha(i.uv);
				float outlines = GetOutlines(sourceTex, i); 
				
				//return float4(lerp(digitalTex, sourceTex, alphaTex.x), 1);
				
				float3 ret = lerp(digitalTex, sourceTex, cellAlpha);
				ret = lerp(ret, cellAlpha, _CellAlpha);
				ret = lerp(ret, alphaTex, _RawAlpha);
				ret = lerp(ret, impressionistTex, _Impressionist);
				ret += saturate(outlines) * _Outlines * float3(0, 1, 1);
				
				return float4(ret,0);
			}
			ENDCG
		}
	}
}
