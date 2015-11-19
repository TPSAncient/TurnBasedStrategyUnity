Shader "Terrain Grid System/Unlit Single Color Territory Thick Line" {
 
Properties {
    _Color ("Color", Color) = (1,1,1,1)
    _Offset ("Depth Offset", float) = -0.01  
    _NearClip ("Near Clip", Range(0, 1000.0)) = 25.0
    _FallOff ("FallOff", Range(1, 1000.0)) = 50.0
    _Width ("Width Offset", Range(0.0001, 0.005)) = 0.0005
}
 
SubShader {
    Tags {
       "Queue"="Geometry+3"
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
			float4 pos : POSITION;	
			float4 rpos : TEXCOORD0;	
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
    
   // SECOND STROKE ***********
 
    Pass {
    	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag				

		float _Offset;
		fixed4 _Color;
		float _NearClip;
		float _FallOff;
		float _Width;

		//Data structure communication from Unity to the vertex shader
		//Defines what inputs the vertex shader accepts
		struct AppData {
			float4 vertex : POSITION;
		};

		//Data structure for communication from vertex shader to fragment shader
		//Defines what inputs the fragment shader accepts
		struct VertexToFragment {
			float4 pos : POSITION;	
			float4 rpos: TEXCOORD0;	
		};
		
		//Vertex shader
		VertexToFragment vert(AppData v) {
			VertexToFragment o;							
			
			float4x4 projectionMatrix = UNITY_MATRIX_P;
			float d = projectionMatrix[1][1];
 			float distanceFromCameraToVertex = mul( UNITY_MATRIX_MV, v.vertex ).z;
 			//The check here is for wether the camera is orthographic or perspective
 			float frustumHeight = projectionMatrix[3][3] == 1 ? 2/d : 2.0*-distanceFromCameraToVertex*(1/d);
 			float metersPerPixel = frustumHeight/_ScreenParams.y;
 			
 			float4 vertex = v.vertex;
 			vertex.x += metersPerPixel*_Width;
			o.pos = mul(UNITY_MATRIX_MVP, vertex);
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
    
      // THIRD STROKE ***********
 
    Pass {
    	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag				

		float _Offset;
		fixed4 _Color;
		float _NearClip;
		float _FallOff;
		float _Width;

		//Data structure communication from Unity to the vertex shader
		//Defines what inputs the vertex shader accepts
		struct AppData {
			float4 vertex : POSITION;
		};

		//Data structure for communication from vertex shader to fragment shader
		//Defines what inputs the fragment shader accepts
		struct VertexToFragment {
			fixed4 pos : POSITION;	
			fixed4 rpos: TEXCOORD0;	
		};
		
		
		//Vertex shader
		VertexToFragment vert(AppData v) {
			VertexToFragment o;							
			
			float4x4 projectionMatrix = UNITY_MATRIX_P;
			float d = projectionMatrix[1][1];
 			float distanceFromCameraToVertex = mul( UNITY_MATRIX_MV, v.vertex ).z;
 			//The check here is for wether the camera is orthographic or perspective
 			float frustumHeight = projectionMatrix[3][3] == 1 ? 2/d : 2.0*-distanceFromCameraToVertex*(1/d);
 			float metersPerPixel = frustumHeight/_ScreenParams.y;
 			
 			float4 vertex = v.vertex;
 			vertex.y += metersPerPixel*_Width;
			o.pos = mul(UNITY_MATRIX_MVP, vertex);
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
    
       
      // FOURTH STROKE ***********
 
    Pass {
    	CGPROGRAM
		#pragma vertex vert	
		#pragma fragment frag				

		float _Offset;
		fixed4 _Color;
		float _NearClip;
		float _FallOff;
		float _Width;

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
			
			float4x4 projectionMatrix = UNITY_MATRIX_P;
			float d = projectionMatrix[1][1];
 			float distanceFromCameraToVertex = mul( UNITY_MATRIX_MV, v.vertex ).z;
 			//The check here is for wether the camera is orthographic or perspective
 			float frustumHeight = projectionMatrix[3][3] == 1 ? 2/d : 2.0*-distanceFromCameraToVertex*(1/d);
 			float metersPerPixel = frustumHeight/_ScreenParams.y;
 			
 			float4 vertex = v.vertex;
 			vertex += metersPerPixel*_Width;
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
