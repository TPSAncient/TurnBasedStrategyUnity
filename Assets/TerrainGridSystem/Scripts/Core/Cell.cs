using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TGS.Geom;

namespace TGS {

	public class Cell: IAdmin {
		public string name { get; set; }
		public int territoryIndex = -1;
		public Region region { get; set; }
		public Polygon polygon { get; set; }
		public Vector2 center;

		public Cell (string name) {
			this.name = name;
		}

		public Cell(string name, Vector2 center): this(name) {
			this.center = center;
		}

	}
}

