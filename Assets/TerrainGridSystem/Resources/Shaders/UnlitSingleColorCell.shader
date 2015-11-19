Shader "Terrain Grid System/Unlit Single Color Cell Thin Line" {
 
Properties {
    _Color ("Color", Color) = (1,1,1,1)
    _Offset ("Depth Offset", float) = -0.01  
    _NearClip ("Near Clip", Range(0, 1000.0)) = 25.0
    _FallOff ("FallOff", Range(1, 1000.0)) = 50.0
}
 
SubShader {
    Tags {
      "Queue"="Geometry+2"
      "RenderType"="Opaque"
  	}
    Blend SrcAlpha OneMinusSrcAlpha
  	ZWrite Off
    Pass {
    	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag				

		float _Offset;
		fixed4 _Color;
		float _NearClip;
		float _FallOff;

		//Data structure communication from Unity to the vertex shader
		//Defines what inputs the vertex shader accepts
		struct AppData {
			float4 vertex : POSITION;
		};

		//Data structure for communication from vertex shader to fragment shader
		//Defines what inputs the fragment shader accepts
		struct VertexToFragment {
			fixed4 pos : POSITION;	
			fixed4 rpos : TEXCOORD0;	
		};
		
		//Vertex shader
		VertexToFragment vert(AppData v) {
			VertexToFragment o;							
			o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
			o.pos.z+=_Offset;
			o.rpos= o.pos;
			return o;									
		}
		
		fixed4 frag(VertexToFragment i) : COLOR {
			fixed4 color = _Color;
			color[3] = saturate((i.rpos.z-_NearClip)/_FallOff);
			return fixed4(color);						//Output RGBA color
		}
			
		ENDCG
    }
    
}
}
