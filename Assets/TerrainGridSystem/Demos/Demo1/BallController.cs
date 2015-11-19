using UnityEngine;
using System.Collections;

namespace TGS {
	public class BallController : MonoBehaviour {

		TerrainGridSystem tgs;

		// Use this for initialization
		void Start () {
			tgs = TerrainGridSystem.instance;
		}
	
		// Update is called once per frame
		void Update () {
			if (transform.position.y < -10)
				Destroy (gameObject);


			if (tgs.cellHighlightedIndex>=0) {
				Vector3 position = tgs.GetCellPosition(tgs.cellHighlightedIndex);
				Vector3 direction = (position - transform.position).normalized;
				GetComponent<Rigidbody>().AddForce( direction * 100);


			}
		}
	}

}