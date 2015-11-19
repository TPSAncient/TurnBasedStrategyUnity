Shader "Terrain Grid System/Unlit Highlight" {
Properties {
    _Color ("Tint Color", Color) = (1,1,1,0.5)
    _Intensity ("Intensity", Range(0.0, 2.0)) = 1.0
    _Offset ("Depth Offset", Int) = -1    
}
SubShader {
    Tags {
        "Queue"="Geometry+230"
        "IgnoreProjector"="True"
        "RenderType"="Transparent"
    }
    
			//Set up blending and other operations
			Offset [_Offset], [_Offset]
			Cull Off			// Must be off to draw all highlight rectangles
			Lighting Off		//On | Off - Lighting will not be calculated or applied
			ZWrite Off			//On | Off - Z coordinates from pixel positions will be written to the Z/Depth buffer
			ZTest Always		//Less | Greater | LEqual | GEqual | Equal | NotEqual | Always - Pixels will only be allowed to continue through the rendering pipeline if the Z coordinate of their position is LEqual the existing Z coordinate in the Z/Depth buffer
			Fog { Mode Off }
			Blend SrcAlpha OneMinusSrcAlpha //  OneMinusSrcAlpha		//SrcFactor DstFactor (also:, SrcFactorA DstFactorA) = One | Zero | SrcColor | SrcAlpha | DstColor | DstAlpha | OneMinusSrcColor | OneMinusSrcAlpha | OneMinusDstColor | OneMinusDstAlpha - Blending between shader output and the backbuffer will use blend mode 'Solid'
								//Blend SrcAlpha OneMinusSrcAlpha     = Alpha blending
								//Blend One One                       = Additive
								//Blend OneMinusDstColor One          = Soft Additive
								//Blend DstColor Zero                 = Multiplicative
								//Blend DstColor SrcColor             = 2x Multiplicative
		Pass {
			CGPROGRAM						//Start a program in the CG language
			#pragma target 2.0				//Run this shader on at least Shader Model 2.0 hardware (e.g. Direct3D 9)
			#pragma fragment frag			//The fragment shader is named 'frag'
			#pragma vertex vert				//The vertex shader is named 'vert'
			#include "UnityCG.cginc"		//Include Unity's predefined inputs and macros

			//Unity variables to be made accessible to Vertex and/or Fragment shader
			fixed4 _Color;								//Receive input from the _Color property
			float _Intensity;

			//Data structure communication from Unity to the vertex shader
			//Defines what inputs the vertex shader accepts
			struct AppData {
				float4 vertex : POSITION;					//Receive vertex position
			};

			//Data structure for communication from vertex shader to fragment shader
			//Defines what inputs the fragment shader accepts
			struct VertexToFragment {
				float4 pos : POSITION;						//Send fragment position to fragment shader
			};

			//Vertex shader
			VertexToFragment vert(AppData v) {
				VertexToFragment o;							//Create a data structure to pass to fragment shader
				o.pos = mul(UNITY_MATRIX_MVP,v.vertex);		//Include influence of Modelview + Projection matrices
				return o;									//Transmit data to the fragment shader
			}

			//Fragment shader
			fixed4 frag(VertexToFragment i) : COLOR {
				return fixed4(_Color) * _Intensity;						//Output RGBA color
			}

			ENDCG							//End of CG program

		}
	}	
}
