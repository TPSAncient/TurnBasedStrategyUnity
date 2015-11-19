using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TGS.Geom;

namespace TGS {

	public class Territory: IAdmin {

		public string name { get; set; }
		public Region region { get; set; }
		public Polygon polygon { get; set; }
		public Vector2 capitalCenter;
		public List<Cell> cells;
		public Color fillColor = Color.gray;

		public Territory (string name) {
			this.name = name;
			this.cells =  new List<Cell>();
		}

	}

}