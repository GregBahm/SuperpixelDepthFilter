Shader "Unlit/SuperPixelOutputShader"
{
	Properties
	{
		_MaxDepth("Max Depth", Float) = 0
		[Range(0, 1)]
		_DepthThreshold("Depth Threshold", Float) = 0
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

			struct CellDepthData
			{
				int TotalDepth;
				int PixelsInCell;
			};

			StructuredBuffer<CellBasisData> _CellBasisBuffer;

			sampler2D _SourceTexture;
			float _MaxDepth;
			float _DepthThreshold;
			int _SourceImageWidth;
			int _SourceImageHeight;
			int _SuperpixelResolution;
			Buffer<int> _ResultsBuffer;

			Buffer<float2> _DepthMappings;
			Buffer<int> _DepthData;
			StructuredBuffer<CellDepthData> _CellDepthsBuffer;
			
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

			int UvsToDepthImageIndex(float2 uv)
			{
				int x = uv.x * 512;
				int y = uv.y * 424;
				return x + y * 512;
			}

			CellBasisData GetCell(float2 uv)
			{
				int pixelIndex = UvsToSourceImageIndex(uv);
				int cellIndex = _ResultsBuffer[pixelIndex];
				return _CellBasisBuffer[cellIndex];
			}

			float GetPixelDepth(float2 uv)
			{
				int depthIndex = UvsToSourceImageIndex(uv);
				float2 newCoords = _DepthMappings[depthIndex] / float2(512, 424);

				int newDepthIndex = UvsToDepthImageIndex(newCoords);
				int rawDepth = _DepthData[newDepthIndex];
				return (float)rawDepth / _MaxDepth;
			}

			float3 GetMoneyMelon(float2 uv)
			{
				int pixelIndex = UvsToSourceImageIndex(uv);
				int cellIndex = _ResultsBuffer[pixelIndex];
				CellDepthData cellDepth = _CellDepthsBuffer[cellIndex];

				float depth = cellDepth.TotalDepth / cellDepth.PixelsInCell;
				return depth / _MaxDepth;
				if (depth > _DepthThreshold || depth == 0)
				{
					return float3(1, 0, 0);
				}
				return tex2D(_SourceTexture, uv).rgb; 
			}

			float3 Control(float2 uv)
			{
				float3 sourceTex = tex2D(_SourceTexture, uv).rgb;
				float depth = GetPixelDepth(uv);

				if (depth > _DepthThreshold || depth == 0)
				{
					return float3(1, 0, 0);
				}
				return sourceTex;
			}

			float3 GetOutlinesColor(v2f i)
			{
				float3 sourceTex = tex2D(_SourceTexture, i.uv).rgb;
				float2 offsetUvs = i.uv + float2(.002, 0);
				float2 offsetUvsB = i.uv + float2(0, .002);
				float3 cellColor = GetCell(i.uv).Color;
				float3 offsetCellColorA = GetCell(offsetUvs).Color;
				float3 offsetCellColorB = GetCell(offsetUvsB).Color;
				float lengthChangeA = length(offsetCellColorA - cellColor);
				float lengthChangeB = length(offsetCellColorB - cellColor);
				float colorDiff = max(lengthChangeA, lengthChangeB);
				float outlinePower = colorDiff * 1000;
				sourceTex = lerp(sourceTex, float3(1, 0, 0), outlinePower);
				return sourceTex;
			}

			float3 GetImpressionistColor(v2f i)
			{
				return GetCell(i.uv).Color;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				//float3 color = Control(i.uv);
				float3 color = GetMoneyMelon(i.uv);
				//return GetDepth(i.uv) / _Test;
				//return tex2D(_SourceTexture, i.uv);
				//float3 color = GetOutlinesColor(i); 
				//float3 color = GetImpressionistColor(i);
				return float4(color,0);
			}
			ENDCG
		}
	}
}
