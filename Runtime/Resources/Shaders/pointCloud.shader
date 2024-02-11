Shader "Immersal/Point Cloud"
{
	Properties
	{

	}

		SubShader
	{
		Cull Off
		Tags{ "RenderType" = "Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			#include "UnityCG.cginc"

			float _PointSize;
			fixed _PerspectiveEnabled;
			fixed4 _PointColor;

			struct Vertex
			{
				float3 vertex : POSITION;
			};

			struct VertexOut
			{
				float psize : PSIZE;
				float4 center : TEXCOORD0;
				half size : TEXCOORD1;
				UNITY_FOG_COORDS(0)
			};

			VertexOut vert(Vertex vertex, out float4 outpos : SV_POSITION)
			{
				VertexOut o;
				outpos = UnityObjectToClipPos(vertex.vertex);

                o.psize = lerp(_PointSize, _PointSize / outpos.w * _ScreenParams.y, step(0.5, _PerspectiveEnabled));
				o.size = o.psize;

				o.center = ComputeScreenPos(outpos);
				UNITY_TRANSFER_FOG(o, o.position);
				return o;
			}
            
			fixed4 frag(VertexOut i, UNITY_VPOS_TYPE vpos : VPOS) : SV_Target
			{
				fixed4 c = _PointColor;
				float4 center = i.center;
				center.xy /= center.w;
				center.xy *= _ScreenParams.xy;
				float d = distance(vpos.xy, center.xy);

				if (d > i.size * 0.5) {
                    discard;
				}

				UNITY_APPLY_FOG(input.fogCoord, c);
				return c;
			}
			ENDCG
		}
	}
}