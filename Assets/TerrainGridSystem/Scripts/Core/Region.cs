using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TGS.Geom;

namespace TGS {
	public class Region {

		public Polygon polygon;
		public List<Vector3> points { get; set; }
		public List<Segment> segments;
		public List<Region> neighbours;
		public IAdmin entity;
		public Rect rect2D;

		public Material customMaterial { get; set; }
		
		public Vector2 customTextureScale, customTextureOffset;
		public float customTextureRotation;


		public Region(IAdmin entity) {
			this.entity = entity;
			neighbours = new List<Region>(12);
			segments = new List<Segment>(50);
		}

		
		public Region Clone() {
			Region c = new Region(entity);
			c.customMaterial = this.customMaterial;
			c.customTextureScale = this.customTextureScale;
			c.customTextureOffset = this.customTextureOffset;
			c.customTextureRotation = this.customTextureRotation;
			c.points = new List<Vector3>(points);
			c.polygon = polygon.Clone();
			c.segments = new List<Segment>(segments);
			return c;
		}

	}
}

