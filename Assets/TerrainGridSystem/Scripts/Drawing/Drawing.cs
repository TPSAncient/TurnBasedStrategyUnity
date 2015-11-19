using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace TGS {
	public static class Drawing {
		static Dictionary<Vector3, int>hit;

		public static Vector3 Vector3one = Vector3.one;
		static List<Vector3> newPoints;

		public static GameObject CreateSurface (string name, Vector3[] surfPoints, Material material) {
			
			GameObject hexa = new GameObject (name, typeof(MeshRenderer), typeof(MeshFilter));
			hexa.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
			
			int pointCount = surfPoints.Length;

			if (newPoints == null) {
				newPoints = new List<Vector3> (pointCount);
			} else{
				newPoints.Clear();
			}
			int[] triNew = new int[pointCount];
			int newPointsCount = -1;
			if (hit == null)
				hit = new Dictionary<Vector3, int> (65000);
			else
				hit.Clear ();

			for (int k=0; k<pointCount; k++) {
				Vector3 p = surfPoints [k];
				if (hit.ContainsKey (p)) {
					triNew [k] = hit [p];
				} else {
					newPoints.Add (p);
					hit.Add (p, ++newPointsCount);
					triNew [k] = newPointsCount;
					if (newPointsCount>=64997) break;
				}
			}
			Mesh mesh = new Mesh ();
			mesh.hideFlags = HideFlags.DontSave;
			Vector3[] newPoints2 = newPoints.ToArray ();
			mesh.vertices = newPoints2;
			mesh.triangles = triNew;
			mesh.RecalculateNormals ();
			mesh.RecalculateBounds ();
			mesh.Optimize ();
			
			MeshFilter meshFilter = hexa.GetComponent<MeshFilter> ();
			meshFilter.mesh = mesh;
			
			hexa.GetComponent<Renderer> ().sharedMaterial = material;
			return hexa;
			
		}

		
		public static GameObject CreateSurface (string name, Vector3[] surfPoints, int[] indices, Material material) {
			
			GameObject hexa = new GameObject (name, typeof(MeshRenderer), typeof(MeshFilter));
			hexa.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
			
			Mesh mesh = new Mesh ();
			mesh.hideFlags = HideFlags.DontSave;
			mesh.vertices = surfPoints;
			mesh.triangles = indices;
			mesh.RecalculateNormals ();
			mesh.RecalculateBounds ();
			mesh.Optimize ();
			
			MeshFilter meshFilter = hexa.GetComponent<MeshFilter> ();
			meshFilter.mesh = mesh;
			
			hexa.GetComponent<Renderer> ().sharedMaterial = material;
			return hexa;
			
		}
	}


}



