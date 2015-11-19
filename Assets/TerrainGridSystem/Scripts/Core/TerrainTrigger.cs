using UnityEngine;
using System.Collections;

namespace TGS {
	public class TerrainTrigger : MonoBehaviour {

		// Use this for initialization
		TerrainGridSystem ths;

		void Start () {
			if (GetComponent<TerrainCollider> () == null) {
				gameObject.AddComponent<TerrainCollider> ();
			}
			ths = transform.GetComponentInChildren<TerrainGridSystem>();
			if (ths==null) {
				Debug.LogError("Missing Terrain Highlight System reference in Terrain Trigger script.");
				DestroyImmediate (this);
			}
		}

		void OnMouseEnter () {
			ths.mouseIsOver = true;
		}
		
		void OnMouseExit () {
			// Make sure it's outside of grid
			Vector3 mousePos = Input.mousePosition;
			Ray ray = Camera.main.ScreenPointToRay (mousePos);
			RaycastHit[] hits = Physics.RaycastAll (Camera.main.transform.position, ray.direction, 5000);
			if (hits.Length > 0) {
				for (int k=0; k<hits.Length; k++) {
					if (hits [k].collider.gameObject == this.ths.terrain.gameObject) 
						return; 
				}
			}
			ths.mouseIsOver = false;
		}

	}

}