//#define HIGHLIGHT_NEIGHBOURS
//#define SHOW_DEBUG_GIZMOS

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using TGS.Geom;
using Poly2Tri;

namespace TGS {

	public enum RESHAPE_MODE {
		NONE = 0,
		ADD = 1,
		MOVE = 2,
		ERASE = 3,
		MERGE = 4
	}

	[ExecuteInEditMode]
	public partial class TerrainGridSystem : MonoBehaviour {

		// internal fields
		const int MAP_LAYER = 5;
		const int IGNORE_RAYCAST = 2;
		const double MIN_VERTEX_DISTANCE = 0.002; 
		const double SQR_MIN_VERTEX_DIST = MIN_VERTEX_DISTANCE * MIN_VERTEX_DISTANCE;
		const double SQR_MAX_VERTEX_DIST = 10 * SQR_MIN_VERTEX_DIST;
		const float LOD_DISTANCE_THRESHOLD = 1000;
		const float HIGHLIGHT_NEAR_CLIP_SQR = 1225; // 35*35;

		// Custom inspector stuff
		public const int MAX_TERRITORIES = 256;
		public const int MAX_CELLS = 10000;
		public bool isDirty;
		public RESHAPE_MODE reshapeMode = RESHAPE_MODE.ADD;
		public const int MAX_CELLS_FOR_CURVATURE = 500;
		public const int MAX_CELLS_FOR_RELAXATION = 500;

		// Materials and resources
		Material territoriesMat, cellsMat, hudMatTerritoryOverlay, hudMatTerritoryGround, hudMatCellOverlay, hudMatCellGround;
		Material coloredMat, texturizedMat;

		// Cell mesh data
		const string CELLS_LAYER_NAME = "Cells";
		Vector3[][] cellMeshBorders;
		int[][] cellMeshIndices;
		Dictionary<Segment,Region> cellNeighbourHit;
		float meshStep;
		bool recreateCells;

		// Territory mesh data
		const string TERRITORIES_LAYER_NAME = "Territories";
		Vector3[][] territoryMeshBorders;
		Dictionary<Segment,Region> territoryNeighbourHit;
		int[][] territoryMeshIndices;
		List<Segment>territoryFrontiers;

		// Common territory & cell structures
		List<Vector3> frontiersPoints;
		Dictionary<Segment, bool> segmentHit;
		List<TriangulationPoint> steinerPoints;
		Dictionary<TriangulationPoint, int> surfaceMeshHit;

		// Terrain data
		float[,] terrainHeights;
		float[,] terrainRoughnessMap;
		float[,] tempTerrainRoughnessMap;
		int terrainRoughnessMapWidth, terrainRoughnessMapHeight;
		int heightMapWidth, heightMapHeight;
		const int TERRAIN_CHUNK_SIZE = 8;
		float effectiveRoughness; // = _gridRoughness * terrainHeight

		// Placeholders and layers
		GameObject territoryLayer;
		GameObject _surfacesLayer;
		GameObject surfacesLayer { get { if (_surfacesLayer==null) CreateSurfacesLayer(); return _surfacesLayer; } }
		GameObject highlightedObj;
		GameObject cellLayer;

		// Caches
		Dictionary<int, GameObject>surfaces;
		Dictionary<Cell, int>_cellLookup;
		int lastCellLookupCount = -1;
		Dictionary<Color, Material>coloredMatCache;
		Color[] factoryColors;
		bool refreshMesh = false;

		// Z-Figther & LOD
		Vector3 lastLocalPosition, lastCamPos, lastPos;
		float lastGridElevation, lastGridCameraOffset;
		float terrainWidth;
		float terrainHeight;
		float terrainDepth;

		// Interaction
		static TerrainGridSystem _instance;
		public bool mouseIsOver;
		Territory _territoryHighlighted;
		int	_territoryHighlightedIndex = -1;
		Cell _cellHighlighted;
		int _cellHighlightedIndex = -1;
		float highlightFadeStart;
		int _territoryLastClickedIndex = -1, _cellLastClickedIndex = -1;

		// Misc
		int _lastVertexCount = 0;
		public int lastVertexCount { get { return _lastVertexCount; } }

		Dictionary<Cell, int>cellLookup {
			get {
				if (_cellLookup != null && cells.Count == lastCellLookupCount)
					return _cellLookup;
				if (_cellLookup == null) {
					_cellLookup = new Dictionary<Cell,int> ();
				} else {
					_cellLookup.Clear ();
				}
				for (int k=0; k<cells.Count; k++) {
					_cellLookup.Add (cells[k], k);
				}
				lastCellLookupCount = cells.Count;
				return _cellLookup;
			}
		}


		int layerMask { get {  return 1<<MAP_LAYER; } }

		#region Gameloop events

		void OnEnable () {
			if (cells==null || territories == null) {
				Init ();
			}
			if (hudMatTerritoryOverlay != null && hudMatTerritoryOverlay.color != _territoryHighlightColor) {
				hudMatTerritoryOverlay.color = _territoryHighlightColor;
			}
			if (hudMatTerritoryGround != null && hudMatTerritoryGround.color != _territoryHighlightColor) {
				hudMatTerritoryGround.color = _territoryHighlightColor;
			}
			if (hudMatCellOverlay != null && hudMatCellOverlay.color != _cellHighlightColor) {
				hudMatCellOverlay.color = _cellHighlightColor;
			}
			if (hudMatCellGround != null && hudMatCellGround.color != _cellHighlightColor) {
				hudMatCellGround.color = _cellHighlightColor;
			}
			if (territoriesMat != null && territoriesMat.color != _territoryFrontierColor) {
				territoriesMat.color = _territoryFrontierColor;
			}
			if (cellsMat != null && cellsMat.color != _cellBorderColor) {
				cellsMat.color = _cellBorderColor;
			}
			UpdateMaterialDepthOffset();
		}


		#endregion



	#region Initialization

		public void Init () {
			gameObject.layer = MAP_LAYER;

			if (territoriesMat == null) {
				territoriesMat = Instantiate (Resources.Load <Material> ("Materials/Territory"));
				territoriesMat.hideFlags = HideFlags.DontSave;
			}
			if (cellsMat == null) {
				cellsMat = Instantiate (Resources.Load <Material> ("Materials/Cell"));
				cellsMat.hideFlags = HideFlags.DontSave;
			}
			if (hudMatTerritoryOverlay == null) {
				hudMatTerritoryOverlay = Instantiate (Resources.Load <Material> ("Materials/HudTerritoryOverlay"));
				hudMatTerritoryOverlay.hideFlags = HideFlags.DontSave;
			}
			if (hudMatTerritoryGround == null) {
				hudMatTerritoryGround = Instantiate (Resources.Load <Material> ("Materials/HudTerritoryGround"));
				hudMatTerritoryGround.hideFlags = HideFlags.DontSave;
			}
			if (hudMatCellOverlay == null) {
				hudMatCellOverlay = Instantiate (Resources.Load <Material> ("Materials/HudCellOverlay"));
				hudMatCellOverlay.hideFlags = HideFlags.DontSave;
			}
			if (hudMatCellGround == null) {
				hudMatCellGround = Instantiate (Resources.Load <Material> ("Materials/HudCellGround"));
				hudMatCellGround.hideFlags = HideFlags.DontSave;
			}
			if (coloredMat == null) {
				coloredMat = Instantiate (Resources.Load <Material> ("Materials/ColorizedRegion"));
				coloredMat.hideFlags = HideFlags.DontSave;
			}
			if (texturizedMat == null) {
				texturizedMat = Instantiate (Resources.Load <Material> ("Materials/TexturizedRegion"));
				texturizedMat.hideFlags = HideFlags.DontSave;
			}
			coloredMatCache = new Dictionary<Color, Material>();

			UnityEngine.Random.seed = seed; //_numCells * seed;
			if (factoryColors==null || factoryColors.Length<MAX_CELLS) {
				factoryColors = new Color[MAX_CELLS];
				for (int k=0;k<factoryColors.Length;k++) factoryColors[k] = new Color(UnityEngine.Random.Range(0.0f, 0.5f), UnityEngine.Random.Range(0.0f,0.5f), UnityEngine.Random.Range (0.0f,0.5f));
			}

			Redraw ();
		}

		void CreateSurfacesLayer() {
			Transform t = transform.FindChild ("Surfaces");
			if (t != null) {
				DestroyImmediate (t.gameObject);
			}
			_surfacesLayer = new GameObject ("Surfaces");
			_surfacesLayer.transform.SetParent (transform, false);
			_surfacesLayer.transform.localPosition = Vector3.zero; // Vector3.back * 0.01f;
			_surfacesLayer.layer = gameObject.layer;
		}

		void DestroySurfaces() {
			HideTerritoryRegionHighlight();
			HideCellRegionHighlight();
			if (segmentHit!=null) segmentHit.Clear ();
			if (surfaces!=null) surfaces.Clear();
			if (_surfacesLayer!=null) DestroyImmediate(_surfacesLayer);
		}

	#endregion

	#region Map generation
	
		void SetupIrregularGrid() {
			Point[] centers = new Point[_numCells];
			for (int k=0;k<centers.Length;k++) {
				centers[k] = new Point(UnityEngine.Random.Range (-0.49f, 0.49f), UnityEngine.Random.Range (-0.49f, 0.49f));
			}
			
			VoronoiFortune voronoi = new VoronoiFortune ();
			for (int k=0;k<goodGridRelaxation;k++) {
				voronoi.AssignData (centers);
				voronoi.DoVoronoi ();
				if (k <goodGridRelaxation-1) {
					for (int j=0;j<_numCells;j++) {
						Point centroid = voronoi.cells[j].centroid;
						centers[j] = (centers[j] + centroid)/2;
					}
				}
			}

			// Make cell regions: we assume cells have only 1 region but that can change in the future
			float curvature = goodGridCurvature;
			for (int k=0; k<voronoi.cells.Length; k++) {
				VoronoiCell voronoiCell = voronoi.cells[k];
				Cell cell = new Cell(k.ToString(), voronoiCell.center.vector3);
				Region cr = new Region (cell);
				if (curvature>0) {
					cr.polygon = voronoiCell.GetPolygon(3, curvature);
				} else {
					cr.polygon = voronoiCell.GetPolygon(1, 0);
				}
				if (cr.polygon!=null) {
					// Add segments
					for (int i=0;i<voronoiCell.segments.Count;i++) {
						Segment s = voronoiCell.segments[i];
						if (!s.deleted) {
							if (curvature>0) {
								cr.segments.AddRange(s.subdivisions);
							} else {
								cr.segments.Add (s);
							}
						}
					}

					cell.polygon = cr.polygon.Clone();
					cell.region = cr;
					cells.Add (cell);
				}
			}
		}

		/// <summary>
		/// Descompose a number in two greater factors.
		/// </summary>
		/// <returns><c>true</c>, if two factors was gotten, <c>false</c> if number is prime.</returns>
		bool GetTwoFactors(int n, out int p, out int q) {
			List<int> factors = new List<int>();
			List<int> primes = new List<int>();
			factors.Add (n);
			while(factors.Count>0) {
				n = factors[0];
				factors.RemoveAt(0);
				bool isPrime = true;
				for (int k=n/2;k>1;k--) {
					if ( n % k == 0) {
						factors.Insert(0, k);
						factors.Insert(1, n/k);
						isPrime = false;
						break;
					}
				}
				if (isPrime) primes.Add (n);
			}
			p = primes[0];
			if (primes.Count == 1) {
				q = 1;
				return false;
			}
			q = primes[1];
			if (primes.Count == 2) {
				return true;
			}
			// reduce until 2 big numbers
			for(;;) {
				int c = primes.Count-1;
				if (c<2) break;
				p = primes[c] * primes[c-1];
				primes.RemoveAt(c);
				primes.RemoveAt(c-1);
				for (c-=2;c>=0;c--) {
					if (primes[c]>p) {
						break;
					}
				}
				primes.Insert (c+1, p);
			}
			p = primes[0];
			q = primes[1];
			return true;
		}

		void SetupBoxGrid(bool strictQuads) {

			// Make cell regions: we assume cells have only 1 region but that can change in the future
			int l = _numCells;
			int qx, qy;
			int q = (int)(Math.Sqrt (l));
			if (strictQuads) {
				qx=qy=q;
			} else {
				qx=l;
				qy=1;
				if (q<1) q=1;
				if ( (int)(q*q) != l) { // not squared
					if (!GetTwoFactors(l, out qx, out qy)) {
						// if number > 10 and it's prime, reduce by one so we can avoid ugly accordian grids
						if (l>10) GetTwoFactors(l-1, out qx, out qy);
					}
				} else {
					qx = qy = q;
				}
			}
			double stepX = (transform.localScale.y / transform.localScale.x) / qx;
			double stepY = (transform.localScale.x / transform.localScale.y) / qy;

			double halfStepX = stepX*0.5;
			double halfStepY = stepY*0.5;

			Segment [,,] sides = new Segment[qx,qy,4]; // 0 = left, 1 = top, 2 = right, 3 = bottom
			int c = -1;
			int subdivisions = goodGridCurvature > 0 ? 3: 1;
			for (int k=0;k<qx;k++) {
				for (int j=0;j<qy;j++) {
					Point center = new Point((double)k/qx-0.5+halfStepX,(double)j/qy-0.5+halfStepY);
					Cell cell = new Cell( (++c).ToString(), new Vector2((float)center.x, (float)center.y));

					Segment left = k>0 ? sides[k-1, j, 2] : new Segment(center.Offset(-halfStepX, -halfStepY), center.Offset(-halfStepX, halfStepY), true);
					sides[k, j, 0] = left;

					Segment top = new Segment(center.Offset(-halfStepX, halfStepY), center.Offset(halfStepX, halfStepY), j==qy-1);
					sides[k, j, 1] = top;

					Segment right = new Segment(center.Offset(halfStepX, halfStepY), center.Offset(halfStepX, -halfStepY), k==qx-1);
					sides[k, j, 2] = right;

					Segment bottom = j>0 ? sides[k, j-1, 1] : new Segment(center.Offset(halfStepX, -halfStepY), center.Offset(-halfStepX, -halfStepY), true);
					sides[k, j, 3] = bottom;

					Region cr = new Region (cell);
					if (subdivisions>1) {
						cr.segments.AddRange (top.Subdivide(subdivisions, _gridCurvature));
						cr.segments.AddRange (right.Subdivide(subdivisions, _gridCurvature));
						cr.segments.AddRange (bottom.Subdivide(subdivisions, _gridCurvature));
						cr.segments.AddRange (left.Subdivide(subdivisions, _gridCurvature));
					} else {
						cr.segments.Add (top);
						cr.segments.Add (right);
						cr.segments.Add (bottom);
						cr.segments.Add (left);
					}
					Connector connector = new Connector();
					connector.AddRange(cr.segments);
					cr.polygon = connector.ToPolygon(); // FromLargestLineStrip();
					if (cr.polygon!=null) {
						cell.region = cr;
						cells.Add (cell);
					}
				}
			}
		}

	
		void SetupHexagonalGrid() {
			
			// Make cell regions: we assume cells have only 1 region but that can change in the future
			int l = _numCells;
			int qx, qy;
			int q = (int)(Math.Sqrt (l));
			q = q * 12 / 13;
			if (q<1) q= 1;
			qx=qy=q;
			int qx2 = qx * 4 / 3;

			double stepX = (transform.localScale.y / transform.localScale.x) / qx;
			double stepY = (transform.localScale.x / transform.localScale.y) / qy;

			double halfStepX = stepX*0.5;
			double halfStepY = stepY*0.5;

			Segment [,,] sides = new Segment[qx2,qy,6]; // 0 = left-up, 1 = top, 2 = right-up, 3 = right-down, 4 = down, 5 = left-down
			int c = -1;
			int subdivisions = goodGridCurvature > 0 ? 3: 1;
			for (int j=0;j<qy;j++) {
				for (int k=0;k<qx2;k++) {
					Point center = new Point((double)k/qx-0.5+halfStepX,(double)j/qy-0.5+halfStepY);
					center.x -= k *  halfStepX/2;
					Cell cell = new Cell( (++c).ToString(), new Vector2((float)center.x, (float)center.y));

					double offsetY = (k % 2==0) ? 0: -halfStepY;

					Segment leftUp =  (k>0 && offsetY<0) ? sides[k-1, j, 3]: new Segment(center.Offset(-halfStepX, offsetY), center.Offset(-halfStepX/2, halfStepY + offsetY), k==0 || (j==qy-1 && offsetY==0));
					sides[k, j, 0] = leftUp;

					Segment top = new Segment(center.Offset(-halfStepX/2, halfStepY + offsetY), center.Offset(halfStepX/2, halfStepY + offsetY), j==qy-1);
					sides[k, j, 1] = top;

					Segment rightUp = new Segment(center.Offset(halfStepX/2, halfStepY + offsetY), center.Offset(halfStepX, offsetY), k==qx2-1 || (j==qy-1 && offsetY==0));
					sides[k, j, 2] = rightUp;

					Segment rightDown = (j > 0 && k<qx2-1 && offsetY<0) ? sides[k+1,j-1,0]: new Segment(center.Offset(halfStepX, offsetY), center.Offset(halfStepX/2, -halfStepY + offsetY), (j==0 && offsetY<0)|| k==qx2-1);
					sides[k, j, 3] = rightDown;

					Segment bottom = j>0 ? sides[k, j-1, 1] : new Segment(center.Offset(halfStepX/2, -halfStepY + offsetY), center.Offset(-halfStepX/2, -halfStepY +offsetY), true);
					sides[k, j, 4] = bottom;

					Segment leftDown;
					if (offsetY<0 && j>0) {
						leftDown = sides[k-1, j-1, 2];
					} else if (offsetY==0 && k>0) {
						leftDown = sides[k-1, j, 2];
					} else {
						leftDown = new Segment(center.Offset(-halfStepX/2, -halfStepY+offsetY), center.Offset(-halfStepX, offsetY), true);
					}
					sides[k, j, 5] = leftDown;

					if (j==0) {
//						leftDown.CropBottom();
//						bottom.CropBottom();
//						rightDown.CropBottom();
					}
					if (k==qx2-1) {
						top.CropRight();
						rightUp.CropRight();
						rightDown.CropRight();
						bottom.CropRight();
					}

					Region cr = new Region (cell);
					if (subdivisions>1) {
						if (!top.deleted) cr.segments.AddRange (top.Subdivide(subdivisions, _gridCurvature));
						if (!rightUp.deleted) cr.segments.AddRange (rightUp.Subdivide(subdivisions, _gridCurvature));
						if (!rightDown.deleted) cr.segments.AddRange (rightDown.Subdivide(subdivisions, _gridCurvature));
						if (!bottom.deleted) cr.segments.AddRange (bottom.Subdivide(subdivisions, _gridCurvature));
						if (!leftDown.deleted) cr.segments.AddRange (leftDown.Subdivide(subdivisions, _gridCurvature));
						if (!leftUp.deleted) cr.segments.AddRange (leftUp.Subdivide(subdivisions, _gridCurvature));
					} else {
						if (!top.deleted) cr.segments.Add (top);
						if (!rightUp.deleted) cr.segments.Add (rightUp);
						if (!rightDown.deleted) cr.segments.Add (rightDown);
						if (!bottom.deleted) cr.segments.Add (bottom);
						if (!leftDown.deleted) cr.segments.Add (leftDown);
						if (!leftUp.deleted) cr.segments.Add (leftUp);
					}
					Connector connector = new Connector();
					connector.AddRange(cr.segments);
					cr.polygon = connector.ToPolygon(); // FromLargestLineStrip();
					if (cr.polygon!=null) {
						cell.region = cr;
						cells.Add (cell);
					}
				}
			}
		}



		void CreateCells () {

			UnityEngine.Random.seed = seed; 

			_numCells = Mathf.Clamp(_numCells, Mathf.Max (_numTerritories, 2), MAX_CELLS);
			if (cells==null) {
				cells = new List<Cell> (_numCells);
			} else {
				cells.Clear();
			}
			lastCellLookupCount = -1;

			switch(_gridTopology) {
			case GRID_TOPOLOGY.Box: SetupBoxGrid(true); break;
			case GRID_TOPOLOGY.Rectangular: SetupBoxGrid(false); break;
			case GRID_TOPOLOGY.Hexagonal: SetupHexagonalGrid(); break;
			default: SetupIrregularGrid(); break; // case GRID_TOPOLOGY.Irregular:
			}

			CellsFindNeighbours();

			// Update cells polygon
			for (int k=0;k<cells.Count;k++) {
				CellUpdateBounds(cells[k]);
			}
		}

		void CellUpdateBounds(Cell cell) {
			cell.polygon = cell.region.polygon;
			List<Vector3> points = cell.polygon.contours[0].GetVector3Points();
			cell.region.points = points;
			// Update bounding rect
			float minx, miny, maxx, maxy;
			minx = miny = float.MaxValue;
			maxx = maxy = float.MinValue;
			for (int p=0;p<points.Count;p++) {
				Vector3 point = points[p];
				if (point.x<minx) minx = point.x;
				if (point.x>maxx) maxx = point.x;
				if (point.y<miny) miny = point.y;
				if (point.y>maxy) maxy = point.y;
			}
			cell.region.rect2D = new Rect(minx, miny, maxx-minx, maxy-miny);
		}


		/// <summary>
		/// Must be called after changing one cell geometry.
		/// </summary>
		void UpdateCellGeometry(Cell cell, TGS.Geom.Polygon poly) {
			// Copy new polygon definition
			cell.region.polygon = poly;
			cell.polygon = cell.region.polygon.Clone();
			// Update segments list
			cell.region.segments.Clear();
			List<Segment>segmentCache = new List<Segment>(cellNeighbourHit.Keys);
			for (int k=0;k<poly.contours[0].points.Count;k++) {
				Segment s = poly.contours[0].GetSegment(k);
				bool found = false;
				// Search this segment in the segment cache
				for (int j=0;j<segmentCache.Count;j++) {
					Segment o = segmentCache[j];
					if ((Point.EqualsBoth(o.start, s.start) && Point.EqualsBoth(o.end, s.end)) || (Point.EqualsBoth(o.end, s.start) && Point.EqualsBoth(o.start, s.end))) {
						cell.region.segments.Add (o);
						((Cell)cellNeighbourHit[o].entity).territoryIndex = cell.territoryIndex; // updates the territory index of this segment in the cache 
						found = true;
						break;
					}
				}
				if (!found) cell.region.segments.Add (s);
			}
			// Refresh neighbours
			CellsUpdateNeighbours();
			// Refresh rect2D
			CellUpdateBounds(cell);
			// Refresh territories
			FindTerritoryFrontiers();
			UpdateTerritoryBoundaries();
		}

		void CellsUpdateNeighbours() {
			for (int k=0; k<cells.Count; k++) {
				cells[k].region.neighbours.Clear();
			}
			CellsFindNeighbours();
		}


		void CellsFindNeighbours () {
			
			if (cellNeighbourHit == null) {
				cellNeighbourHit = new Dictionary<Segment, Region> (50000);
			} else {
				cellNeighbourHit.Clear ();
			}
			for (int k=0; k<cells.Count; k++) {
				Cell cell = cells [k];
				Region region = cell.region;
				int numSegments = region.segments.Count;
				for (int i = 0; i<numSegments; i++) {
					Segment seg = region.segments[i];
					if (cellNeighbourHit.ContainsKey (seg)) {
						Region neighbour = cellNeighbourHit [seg];
						if (neighbour != region) {
							if (!region.neighbours.Contains (neighbour)) {
								region.neighbours.Add (neighbour);
								neighbour.neighbours.Add (region);
							}
						}
					} else {
						cellNeighbourHit.Add (seg, region);
					}
				}
			}
		}


		void FindTerritoryFrontiers () {

			if (territories==null) return;

			if (territoryFrontiers == null) {
				territoryFrontiers = new List<Segment>(cellNeighbourHit.Count);
			} else {
				territoryFrontiers.Clear ();
			}
			if (territoryNeighbourHit == null) {
				territoryNeighbourHit = new Dictionary<Segment, Region> (50000);
			} else {
				territoryNeighbourHit.Clear ();
			}
			Connector[] connectors = new Connector[territories.Count];
			for (int k=0;k<territories.Count;k++)
				connectors[k] = new Connector();

			for (int k=0; k<cells.Count; k++) {
				Cell cell = cells [k];
				Region region = cell.region;
				int numSegments = region.segments.Count;
				for (int i = 0; i<numSegments; i++) {
					Segment seg = region.segments[i];
					if (seg.border) {
						territoryFrontiers.Add (seg);
						int territory1 = cell.territoryIndex;
						connectors[territory1].Add (seg);
						continue;
					}
					if (territoryNeighbourHit.ContainsKey (seg)) {
						Region neighbour = territoryNeighbourHit [seg];
						Cell neighbourCell = (Cell)neighbour.entity;
						if (neighbourCell.territoryIndex!=cell.territoryIndex) {
							territoryFrontiers.Add (seg);
							int territory1 = cell.territoryIndex;
							connectors[territory1].Add (seg);
							int territory2 = neighbourCell.territoryIndex;
							connectors[territory2].Add (seg);
						}
					} else {
						territoryNeighbourHit.Add (seg, region);
					}
				}
			}

			for (int k=0;k<territories.Count;k++)
				territories[k].polygon = connectors[k].ToPolygonFromLargestLineStrip();
		}


		void AddSegmentToMesh(Vector3 p0, Vector3 p1) {
			float h0 = _terrain.SampleHeight(transform.TransformPoint(p0));
			float h1 = _terrain.SampleHeight(transform.TransformPoint(p1));
			if (_gridNormalOffset>0) {
				Vector3 invNormal = transform.InverseTransformVector(_terrain.terrainData.GetInterpolatedNormal(p0.x+0.5f,p0.y+0.5f));
				p0 += invNormal * _gridNormalOffset;
				invNormal = transform.InverseTransformVector(_terrain.terrainData.GetInterpolatedNormal(p1.x+0.5f,p1.y+0.5f));
				p1 += invNormal * _gridNormalOffset;
			}
			p0.z -= h0;
			p1.z -= h1;
			frontiersPoints.Add (p0);
			frontiersPoints.Add (p1);
		}

		/// <summary>
		/// Subdivides the segment in smaller segments
		/// </summary>
		/// <returns><c>true</c>, if segment was drawn, <c>false</c> otherwise.</returns>
		void SurfaceSegmentForMesh(Vector3 p0, Vector3 p1) {

			// trace the line until roughness is exceeded
			float dist = (float)Math.Sqrt ( (p1.x-p0.x)*(p1.x-p0.x) + (p1.y-p0.y)*(p1.y-p0.y));
			Vector3 direction = p1-p0;

			int numSteps = Mathf.FloorToInt( meshStep * dist);
			Vector3 t0 = p0;
			float h0 = _terrain.SampleHeight(transform.TransformPoint(t0));
			if (_gridNormalOffset>0) {
				Vector3 invNormal = transform.InverseTransformVector(_terrain.terrainData.GetInterpolatedNormal(t0.x+0.5f,t0.y+0.5f));
				t0 += invNormal * _gridNormalOffset;
			}
			t0.z -= h0;
			Vector3 ta = t0;
			float h1, ha = h0;
			for (int i=1;i<numSteps;i++) {
				Vector3 t1 = p0 + direction * i / numSteps;
				h1 = _terrain.SampleHeight(transform.TransformPoint(t1));
				if ( h0 < h1 || h0-h1 > effectiveRoughness) {
					frontiersPoints.Add (t0);
					if (t0!=ta) {
						if (_gridNormalOffset>0) {
							Vector3 invNormal = transform.InverseTransformVector(_terrain.terrainData.GetInterpolatedNormal(ta.x+0.5f,ta.y+0.5f));
							ta += invNormal * _gridNormalOffset;
						}
						ta.z -= ha;
						frontiersPoints.Add (ta);
						frontiersPoints.Add (ta);
					}
					if (_gridNormalOffset>0) {
						Vector3 invNormal = transform.InverseTransformVector(_terrain.terrainData.GetInterpolatedNormal(t1.x+0.5f,t1.y+0.5f));
						t1 += invNormal * _gridNormalOffset;
					}
					t1.z -= h1;
					frontiersPoints.Add (t1);
					t0 = t1;
					h0 = h1;
				}
				ta = t1;
				ha = h1;
			}
			// Add last point
			h1 = _terrain.SampleHeight(transform.TransformPoint(p1));
			if (_gridNormalOffset>0) {
				Vector3 invNormal = transform.InverseTransformVector(_terrain.terrainData.GetInterpolatedNormal(p1.x+0.5f,p1.y+0.5f));
				p1 += invNormal * _gridNormalOffset;
			}
			p1.z -= h1;
			frontiersPoints.Add (t0);
			frontiersPoints.Add (p1);
		}

		void GenerateCellsMesh () {
			
			if (segmentHit == null) {
				segmentHit = new Dictionary<Segment, bool> (50000);
			} else {
				segmentHit.Clear ();
			}

			if (frontiersPoints == null) {
				frontiersPoints = new List<Vector3> (100000);
			} else {
				frontiersPoints.Clear ();
			}

			if (_terrain==null) {
				for (int k=0; k<cells.Count; k++) {
					Cell cell = cells [k];
					Region region = cell.region;
					int numSegments = region.segments.Count;
					for (int i = 0; i<numSegments; i++) {
						Segment s = region.segments[i];
						if (!segmentHit.ContainsKey(s)) {
							segmentHit.Add (s, true);
							frontiersPoints.Add (s.startToVector3);
							frontiersPoints.Add (s.endToVector3);
						}
					}
				}
			} else {
				meshStep = (2.0f - _gridRoughness) / (float)MIN_VERTEX_DISTANCE;
				for (int k=0; k<cells.Count; k++) {
					Cell cell = cells [k];
					Region region = cell.region;
					int numSegments = region.segments.Count;
					for (int i = 0; i<numSegments; i++) {
						Segment s = region.segments[i];
						if (!segmentHit.ContainsKey(s)) {
							segmentHit.Add (s, true);
							SurfaceSegmentForMesh(s.start.vector3, s.end.vector3);
						}
					}
				}
			}

			int meshGroups = (frontiersPoints.Count / 65000) + 1;
			int meshIndex = -1;
			if (cellMeshIndices==null || cellMeshIndices.GetUpperBound(0)!=meshGroups-1) {
				cellMeshIndices = new int[meshGroups][];
				cellMeshBorders = new Vector3[meshGroups][];
			}
			for (int k=0; k<frontiersPoints.Count; k+=65000) {
				int max = Mathf.Min (frontiersPoints.Count - k, 65000); 
				++meshIndex;
				if (cellMeshBorders[meshIndex]==null || cellMeshBorders[0].GetUpperBound(0)!=max-1) {
					cellMeshBorders [meshIndex] = new Vector3[max];
					cellMeshIndices [meshIndex] = new int[max];
				}
				for (int j=0; j<max; j++) {
					cellMeshBorders [meshIndex] [j] = frontiersPoints [j+k];
					cellMeshIndices [meshIndex] [j] = j;
				}
			}
		}

		void CreateTerritories() {

			_numTerritories = Mathf.Clamp(_numTerritories, 1, MAX_CELLS);

			if (!_colorizeTerritories && !_showTerritories && _highlightMode != HIGHLIGHT_MODE.Territories) {
				if (territories!=null) territories.Clear();
				if (territoryLayer!=null) DestroyImmediate(territoryLayer);
				return;
			}

			// Freedom for the cells!...
			CheckCells();
			for (int k=0;k<cells.Count;k++) {
				cells[k].territoryIndex = -1;
			}

			UnityEngine.Random.seed = seed;

			// ... em, not. Start creating countries and assigning one cell
			if (territories==null) {
				territories = new List<Territory>(_numTerritories);
			} else {
				territories.Clear();
			}
			for (int c=0;c<_numTerritories;c++) {
				Territory territory = new Territory(c.ToString());
				territory.fillColor = factoryColors[c];
				int territoryIndex = territories.Count;
				int p = UnityEngine.Random.Range (0, cells.Count);
				int z=0;
				while (cells[p].territoryIndex!=-1 && z++<=cells.Count) {
					p++;
					if (p>=cells.Count) p=0;
				}
				if (z>cells.Count) break; // no more territories can be found - this should not happen
				Cell prov = cells[p];
				prov.territoryIndex = territoryIndex;
				territory.capitalCenter = prov.center;
				territory.cells.Add (prov);
//				territory.polygon = prov.polygon.Clone();
				territories.Add (territory);
			}

			// Continue conquering cells
//			PolygonClipper[] pc = new PolygonClipper[_numTerritories];
			int[] territoryCellIndex = new int[territories.Count];


			// Iterate one cell per country (this is not efficient but ensures balanced distribution)
			bool remainingCells = true;
			while (remainingCells) {
				remainingCells = false;
				for (int k=0; k<territories.Count; k++) {
					Territory territory = territories [k];
					for (int p=territoryCellIndex[k]; p<territory.cells.Count; p++) {
						Region cellRegion = territory.cells[p].region;
						for (int n=0; n<cellRegion.neighbours.Count; n++) {
							Region otherRegion = cellRegion.neighbours [n];
							Cell otherProv = (Cell)otherRegion.entity;
							if (otherProv.territoryIndex == -1) {
								otherProv.territoryIndex = k;
								territory.cells.Add (otherProv);
								remainingCells = true;
								p = territory.cells.Count;
								break;
							}
						}
						if (p<territory.cells.Count) // no free neighbours left for this cell
							territoryCellIndex[k]++;
					}
				}
			}
			FindTerritoryFrontiers();
			UpdateTerritoryBoundaries();

		}

		void UpdateTerritoryBoundaries() {
			if (territories==null) return;

			// Update territory region
			for (int k=0; k<territories.Count; k++) {
				Territory territory = territories [k];
				if (territory.polygon==null) continue;
				Region territoryRegion = new Region(territory);
				territoryRegion.points = territory.polygon.contours[0].GetVector3Points();
				List<Point> points = territory.polygon.contours[0].points;
				for (int j=0;j<points.Count;j++) {
					Point p0 = points[j];
					Point p1;
					if (j == points.Count-1) {
						p1 = points[0];
					} else {
						p1 = points[j+1];
					}
					territoryRegion.segments.Add (new Segment(p0, p1));
				}
				territory.region = territoryRegion;
				
				// Update bounding rect
				float minx, miny, maxx, maxy;
				minx = miny = float.MaxValue;
				maxx = maxy = float.MinValue;
				for (int p=0;p<territoryRegion.points.Count;p++) {
					Vector3 point = territoryRegion.points[p];
					if (point.x<minx) minx = point.x;
					if (point.x>maxx) maxx = point.x;
					if (point.y<miny) miny = point.y;
					if (point.y>maxy) maxy = point.y;
				}
				territoryRegion.rect2D = new Rect(minx, miny, maxx-minx, maxy-miny);
			}
		}

		void GenerateTerritoriesMesh () {
			if (territories==null) return;

			if (segmentHit == null) {
				segmentHit = new Dictionary<Segment, bool> (5000);
			} else {
				segmentHit.Clear ();
			}
			if (frontiersPoints == null) {
				frontiersPoints = new List<Vector3> (10000);
			} else {
				frontiersPoints.Clear ();
			}

			if (_terrain==null) {
				for (int k=0; k<territoryFrontiers.Count; k++) {
					Segment s = territoryFrontiers[k];
					frontiersPoints.Add (s.startToVector3);
					frontiersPoints.Add (s.endToVector3);
				}
			} else {
				meshStep = (2.0f - _gridRoughness) / (float)MIN_VERTEX_DISTANCE;
				for (int k=0; k<territoryFrontiers.Count; k++) {
					Segment s = territoryFrontiers[k];
					SurfaceSegmentForMesh(s.start.vector3, s.end.vector3);
				}

			}

			int meshGroups = (frontiersPoints.Count / 65000) + 1;
			int meshIndex = -1;
			if (territoryMeshIndices==null || territoryMeshIndices.GetUpperBound(0)!=meshGroups-1) {
				territoryMeshIndices = new int[meshGroups][];
				territoryMeshBorders = new Vector3[meshGroups][];
			}
			for (int k=0; k<frontiersPoints.Count; k+=65000) {
				int max = Mathf.Min (frontiersPoints.Count - k, 65000); 
				++meshIndex;
				if (territoryMeshBorders[meshIndex]==null || territoryMeshBorders[meshIndex].GetUpperBound(0)!=max-1) {
					territoryMeshBorders [meshIndex] = new Vector3[max];
					territoryMeshIndices [meshIndex] = new int[max];
				}
				for (int j=0; j<max; j++) {
					territoryMeshBorders [meshIndex] [j] = frontiersPoints [j+k];
					territoryMeshIndices [meshIndex] [j] = j;
				}
			}
		}



		void FitToTerrain() {
			if (_terrain==null || Camera.main==null) return;

			// Fit to terrain
			Vector3 terrainSize = _terrain.terrainData.size;
			terrainWidth = terrainSize.x;
			terrainHeight = terrainSize.y;
			terrainDepth = terrainSize.z;
            transform.localRotation = Quaternion.Euler(90,0,0);
			transform.localScale = new Vector3( terrainWidth, terrainDepth, 1);
			effectiveRoughness = _gridRoughness * terrainHeight;

			Vector3 camPos = Camera.main.transform.position;
			bool refresh = camPos != lastCamPos || transform.position != lastPos || _gridElevation != lastGridElevation || _gridCameraOffset != lastGridCameraOffset;
			if (refresh) {
				Vector3 localPosition = new Vector3(terrainWidth * 0.5f, 0.01f +_gridElevation, terrainDepth * 0.5f);
				if (_gridCameraOffset>0) {
					localPosition += (camPos-transform.position).normalized * (camPos - transform.position).sqrMagnitude * _gridCameraOffset * 0.001f;
				} 
				transform.localPosition = localPosition;
				lastPos = transform.position;
				lastCamPos = camPos;
				lastGridElevation = _gridElevation;
				lastGridCameraOffset = _gridCameraOffset;
			}
		}

		void UpdateTerrainReference(Terrain terrain) {

			_terrain = terrain;
			MeshRenderer quad = GetComponent<MeshRenderer>();

			if (_terrain==null) {
				if (!quad.enabled)
					quad.enabled = true;
				if (transform.parent!=null) {
					transform.SetParent(null);
					transform.localScale = new Vector3(100,100,1);
					transform.localRotation = Quaternion.Euler(0,0,0);
				}
				MeshCollider mc = GetComponent<MeshCollider>();
				if (mc==null) gameObject.AddComponent<MeshCollider>();
			} else {
				transform.SetParent(_terrain.transform, false);
				if (quad.enabled) {
					quad.enabled = false;
				}
				if (_terrain.GetComponent<TerrainTrigger>()==null) {
					_terrain.gameObject.AddComponent<TerrainTrigger>();
				}
				MeshCollider mc = GetComponent<MeshCollider>();
				if (mc!=null) DestroyImmediate(mc);
				lastCamPos = Camera.main.transform.position - Vector3.up; // just to force update on first frame
				FitToTerrain ();
				lastCamPos = Camera.main.transform.position - Vector3.up; // just to force update on first update as well
				if (CalculateTerrainRoughness ()) {
					refreshMesh = true;
					// Clear geometry
					if (cellLayer != null) {
						DestroyImmediate (cellLayer);
					}
					if (territoryLayer != null) {
						DestroyImmediate (territoryLayer);
					}
				}

			}
		}

		/// <summary>
		/// Calculates the terrain roughness.
		/// </summary>
		/// <returns><c>true</c>, if terrain roughness has changed, <c>false</c> otherwise.</returns>
		bool CalculateTerrainRoughness() {
			heightMapWidth = _terrain.terrainData.heightmapWidth;
			heightMapHeight= _terrain.terrainData.heightmapHeight;
			terrainHeights = _terrain.terrainData.GetHeights(0,0,heightMapWidth,heightMapHeight);
			terrainRoughnessMapWidth = heightMapWidth / TERRAIN_CHUNK_SIZE;
			terrainRoughnessMapHeight = heightMapHeight / TERRAIN_CHUNK_SIZE;
			if (terrainRoughnessMap==null) {
				terrainRoughnessMap = new float[terrainRoughnessMapHeight, terrainRoughnessMapWidth];
				tempTerrainRoughnessMap = new float[terrainRoughnessMapHeight, terrainRoughnessMapWidth];
			} else {
				for (int l=0;l<terrainRoughnessMapHeight;l++) {
					for (int c=0;c<terrainRoughnessMapWidth;c++) {
						terrainRoughnessMap[l,c] = 0;
						tempTerrainRoughnessMap[l,c] = 0;
					}
				}
			}

#if SHOW_DEBUG_GIZMOS
			if (GameObject.Find ("ParentDot")!=null) DestroyImmediate(GameObject.Find ("ParentDot"));
			GameObject parentDot = new GameObject("ParentDot");
			parentDot.hideFlags = HideFlags.DontSave;
			parentDot.transform.position = Vector3.zero;
#endif

			float maxStep = (float)TERRAIN_CHUNK_SIZE/heightMapWidth;
			float minStep = 1.0f/heightMapWidth;
			for (int y=0, l=0;l<terrainRoughnessMapHeight;y+=TERRAIN_CHUNK_SIZE,l++) {
				for (int x=0,c=0;c<terrainRoughnessMapWidth;x+=TERRAIN_CHUNK_SIZE,c++) {
					int j0 = y == 0 ? 1: y;
					int j1 = y+TERRAIN_CHUNK_SIZE;
					int k0 = x == 0 ? 1: x;
					int k1 = x+TERRAIN_CHUNK_SIZE;
					float maxDiff = 0;
					for (int j=j0;j<j1;j++) {
						for (int k=k0;k<k1;k++) {
							float diff = terrainHeights[j,k] - terrainHeights[j,k-1];
							if (diff>maxDiff || -diff>maxDiff) maxDiff = Mathf.Abs (diff);
							diff = terrainHeights[j,k] - terrainHeights[j+1,k-1];
							if (diff>maxDiff || -diff>maxDiff) maxDiff = Mathf.Abs (diff);
							diff = terrainHeights[j,k] - terrainHeights[j+1,k];
							if (diff>maxDiff || -diff>maxDiff) maxDiff = Mathf.Abs (diff);
							diff = terrainHeights[j,k] - terrainHeights[j+1,k+1];
							if (diff>maxDiff || -diff>maxDiff) maxDiff = Mathf.Abs (diff);
							diff = terrainHeights[j,k] - terrainHeights[j,k+1];
							if (diff>maxDiff || -diff>maxDiff) maxDiff = Mathf.Abs (diff);
							diff = terrainHeights[j,k] - terrainHeights[j-1,k+1];
							if (diff>maxDiff || -diff>maxDiff) maxDiff = Mathf.Abs (diff);
							diff = terrainHeights[j,k] - terrainHeights[j-1,k];
							if (diff>maxDiff || -diff>maxDiff) maxDiff = Mathf.Abs (diff);
							diff = terrainHeights[j,k] - terrainHeights[j-1,k-1];
							if (diff>maxDiff || -diff>maxDiff) maxDiff = Mathf.Abs (diff);
						}
					}
					maxDiff /= (_gridRoughness * 5.0f);
					maxDiff = Mathf.Lerp (minStep, maxStep, (1.0f - maxDiff) / (1.0f + maxDiff));
					tempTerrainRoughnessMap[l,c] = maxDiff; 
				}
			}

			// collapse chunks with low gradient
			float flatThreshold = maxStep * (1.0f - _gridRoughness*0.1f);
			for (int j=0;j<terrainRoughnessMapHeight;j++) {
				for (int k=0;k<terrainRoughnessMapWidth-1;k++) {
					if (tempTerrainRoughnessMap[j,k]>=flatThreshold) {
						int i = k+1;
						while (i<terrainRoughnessMapWidth && tempTerrainRoughnessMap[j,i]>=flatThreshold) i++;
						while (k<i && k<terrainRoughnessMapWidth) tempTerrainRoughnessMap[j,k] = maxStep * (i-k++);
					}
				}
			}

			// spread min step
			for (int l=0;l<terrainRoughnessMapHeight;l++) {
				for (int c=0;c<terrainRoughnessMapWidth;c++) {
					minStep = tempTerrainRoughnessMap[l,c];
					if (l>0) {
						if (tempTerrainRoughnessMap[l-1,c]<minStep) minStep = tempTerrainRoughnessMap[l-1,c];
						if (c>0) if (tempTerrainRoughnessMap[l-1,c-1]<minStep) minStep = tempTerrainRoughnessMap[l-1,c-1];
						if (c<terrainRoughnessMapWidth-1) if (tempTerrainRoughnessMap[l-1,c+1]<minStep) minStep = tempTerrainRoughnessMap[l-1,c+1];
					}
					if (c>0 && tempTerrainRoughnessMap[l,c-1]<minStep) minStep =  tempTerrainRoughnessMap[l,c-1];
					if (c<terrainRoughnessMapWidth-1 && tempTerrainRoughnessMap[l,c+1]<minStep) minStep =  tempTerrainRoughnessMap[l,c+1];
					if (l<terrainRoughnessMapHeight-1) {
						if (tempTerrainRoughnessMap[l+1,c]<minStep) minStep = tempTerrainRoughnessMap[l+1,c];
						if (c>0) if (tempTerrainRoughnessMap[l+1,c-1]<minStep) minStep = tempTerrainRoughnessMap[l+1,c-1];
						if (c<terrainRoughnessMapWidth-1) if (tempTerrainRoughnessMap[l+1,c+1]<minStep) minStep = tempTerrainRoughnessMap[l+1,c+1];
					}
					terrainRoughnessMap[l,c] = minStep;
				}
			}


#if SHOW_DEBUG_GIZMOS
			for (int l=0;l<terrainRoughnessMapHeight-1;l++) {
				for (int c=0;c<terrainRoughnessMapWidth-1;c++) {
					if (terrainRoughnessMap[l,c]<0.005f) {
						GameObject marker = Instantiate(Resources.Load<GameObject>("Prefabs/Dot"));
						marker.transform.SetParent(parentDot.transform, false);
						marker.hideFlags = HideFlags.DontSave;
						marker.transform.localPosition = new Vector3(500 * ((float)c / 64 + 0.5f/64) , 1, 500* ((float)l / 64 +  0.5f/64));
						marker.transform.localScale = Vector3.one * 350/512.0f;
					}
				}
			}
#endif

			return true;
		}


		void UpdateMaterialDepthOffset () {
			if (territories != null) {
				for (int c=0; c<territories.Count; c++) {
					int cacheIndex = GetCacheIndexForTerritoryRegion (c);
					if (surfaces.ContainsKey (cacheIndex)) {
						GameObject surf = surfaces [cacheIndex];
						if (surf != null) {
							surf.GetComponent<Renderer> ().sharedMaterial.SetInt ("_Offset", _gridDepthOffset);
						}
					}
				}
			}
			if (cells != null) {
				for (int c=0; c<cells.Count; c++) {
					int cacheIndex = GetCacheIndexForCellRegion (c);
					if (surfaces.ContainsKey (cacheIndex)) {
						GameObject surf = surfaces [cacheIndex];
						if (surf != null) {
							surf.GetComponent<Renderer> ().sharedMaterial.SetInt ("_Offset", _gridDepthOffset);
						}
					}
				}
			}
			cellsMat.SetFloat ("_Offset", _gridDepthOffset/10000.0f);
			territoriesMat.SetFloat ("_Offset", _gridDepthOffset/10000.0f);
			hudMatCellOverlay.SetInt ("_Offset", _gridDepthOffset);
			hudMatCellGround.SetInt ("_Offset", _gridDepthOffset-1);
			hudMatTerritoryOverlay.SetInt ("_Offset", _gridDepthOffset);
			hudMatTerritoryGround.SetInt ("_Offset", _gridDepthOffset-1);
		}


	#endregion
	
		#region Drawing stuff

		int GetCacheIndexForTerritoryRegion (int territoryIndex) {
			return territoryIndex; // * 1000 + regionIndex;
		}

		Material hudMatTerritory { get {
				return _overlayMode == OVERLAY_MODE.Overlay ? hudMatTerritoryOverlay: hudMatTerritoryGround;
			}
		}

		Material hudMatCell { get {
				return _overlayMode == OVERLAY_MODE.Overlay ? hudMatCellOverlay: hudMatCellGround;
			}
		}

		Material GetColoredTexturedMaterial(Color color, Texture2D texture) {
			if (texture==null && coloredMatCache.ContainsKey(color)) {
				return coloredMatCache[color];
			} else {
				Material customMat;
				if (texture!=null) {
					customMat = Instantiate(texturizedMat);
					customMat.name = texturizedMat.name;
					customMat.mainTexture = texture;
				} else {
					customMat = Instantiate (coloredMat);
					customMat.name = coloredMat.name;
					coloredMatCache[color] = customMat;
				}
				customMat.color = color;
				customMat.hideFlags = HideFlags.DontSave;
				return customMat;
			}
		}

		void ApplyMaterialToSurface(GameObject obj, Material sharedMaterial) {
			if (obj!=null) {
				Renderer r = obj.GetComponent<Renderer>();
				if (r!=null) 
					r.sharedMaterial = sharedMaterial;
			}
		}


		void DrawColorizedTerritories() {
			if (territories==null) return;
			for (int k=0;k<territories.Count;k++) {
				Color fillColor = territories[k].fillColor;
				fillColor.a *= colorizedTerritoriesAlpha;
				ToggleTerritoryRegionSurface(k, true, fillColor);
			}
		}

		public void GenerateMap() {
			recreateCells = true;
			Redraw();
		}


		public void Redraw() {

			if (!gameObject.activeInHierarchy) return;

			// Initialize surface cache
			if (surfaces != null) {
				List<GameObject> cached = new List<GameObject> (surfaces.Values);
				for (int k=0; k<cached.Count; k++)
					if (cached [k] != null)
						DestroyImmediate (cached [k]);
			}
			surfaces = new Dictionary<int, GameObject> ();
			DestroySurfaces();

			refreshMesh = true;
			UpdateTerrainReference(_terrain);

			_lastVertexCount = 0;
			if (_showCells) {
				CheckCells ();
				DrawCellBorders();
			}
			if (_showTerritories) {
				CheckTerritories();
				DrawTerritoryBorders();
			}
			if (_colorizeTerritories) {
				CheckTerritories();
				DrawColorizedTerritories();
			}
			recreateCells = false;
			refreshMesh = false; // mesh creation finished at this point
		}


		void CheckCells() {
			if (!_showCells && !_showTerritories && !_colorizeTerritories && _highlightMode == HIGHLIGHT_MODE.None) return;
			if (cells==null || recreateCells) {
				CreateCells();
				refreshMesh = true;
			}
			if (refreshMesh)
				GenerateCellsMesh();

		}

		void DrawCellBorders () {

			CheckCells();

			if (cellLayer != null) {
				DestroyImmediate (cellLayer);
			} else {
				Transform t = transform.FindChild(CELLS_LAYER_NAME);
				if (t!=null) DestroyImmediate(t.gameObject);
			}
			if (cells.Count==0) return;

			cellLayer = new GameObject (CELLS_LAYER_NAME);
			cellLayer.hideFlags = HideFlags.DontSave;
			cellLayer.transform.SetParent (transform, false);
			cellLayer.transform.localPosition = Vector3.back * 0.001f;
		
			for (int k=0; k<cellMeshBorders.Length; k++) {
				GameObject flayer = new GameObject ("flayer");
				flayer.hideFlags = HideFlags.DontSave;
				flayer.transform.SetParent (cellLayer.transform, false);
				flayer.transform.localPosition = Vector3.zero;
				flayer.transform.localRotation = Quaternion.Euler (Vector3.zero);
			
				Mesh mesh = new Mesh ();
				mesh.vertices = cellMeshBorders [k];
				mesh.SetIndices (cellMeshIndices [k], MeshTopology.Lines, 0);

				mesh.RecalculateBounds ();
				mesh.hideFlags = HideFlags.DontSave;
			
				MeshFilter mf = flayer.AddComponent<MeshFilter> ();
				mf.sharedMesh = mesh;
				_lastVertexCount += mesh.vertexCount;
			
				MeshRenderer mr = flayer.AddComponent<MeshRenderer> ();
				mr.receiveShadows = false;
				mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
				mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				mr.useLightProbes = false;
				mr.sharedMaterial = cellsMat;
			}

			cellLayer.SetActive(_showCells);

			// Draw mesh markers ( DEBUG)
//			int cc = 0;
//			for (int k=0;k<cellMeshBorders.Length;k++) {
//				Vector3[] vec = cellMeshBorders[k];
//				for (int j=0;j<vec.Length;j++) {
//					if (++cc>10000) return;
//					GameObject dot = Instantiate(dotPrefab);
//					dot.hideFlags = HideFlags.DontSave;
//					dot.transform.SetParent(cellLayer.transform, false);
//					dot.transform.localPosition = vec[j];
//					dot.transform.localScale = Vector3.one * 0.001f;
//				}
//			}
		}
	
		void CheckTerritories() {
			if (!_showTerritories && !_colorizeTerritories && _highlightMode != HIGHLIGHT_MODE.Territories) return;
			if (territories==null || recreateCells) {
				CreateTerritories();
				refreshMesh = true;
			}
			if (refreshMesh) 
				GenerateTerritoriesMesh();

		}

		void DrawTerritoryBorders () {

			if (territoryLayer != null) {
				DestroyImmediate (territoryLayer);
			} else {
				Transform t = transform.FindChild(TERRITORIES_LAYER_NAME);
				if (t!=null) DestroyImmediate(t.gameObject);
			}
			if (territories.Count==0) return;

			territoryLayer = new GameObject (TERRITORIES_LAYER_NAME);
			territoryLayer.hideFlags = HideFlags.DontSave;
			territoryLayer.transform.SetParent (transform, false);
			territoryLayer.transform.localPosition = Vector3.back * 0.001f;
			
			for (int k=0; k<territoryMeshBorders.Length; k++) {
				GameObject flayer = new GameObject ("flayer");
				flayer.hideFlags = HideFlags.DontSave;
				flayer.transform.SetParent (territoryLayer.transform, false);
				flayer.transform.localPosition = Vector3.back * 0.001f; // Vector3.zero;
				flayer.transform.localRotation = Quaternion.Euler (Vector3.zero);
				
				Mesh mesh = new Mesh ();
				mesh.vertices = territoryMeshBorders [k];
				mesh.SetIndices (territoryMeshIndices [k], MeshTopology.Lines, 0);

				mesh.RecalculateBounds ();
				mesh.hideFlags = HideFlags.DontSave;
				
				MeshFilter mf = flayer.AddComponent<MeshFilter> ();
				mf.sharedMesh = mesh;
				_lastVertexCount += mesh.vertexCount;

				MeshRenderer mr = flayer.AddComponent<MeshRenderer> ();
				mr.receiveShadows = false;
				mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
				mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				mr.useLightProbes = false;
				mr.sharedMaterial = territoriesMat;
			}
			territoryLayer.SetActive(_showTerritories);

		}

		#endregion


		#region Internal API

		public string GetMapData () {
		
			return "";
		
		
		
		}

		#endregion


	#region Highlighting
	
		void OnMouseEnter () {
			mouseIsOver = true;
		}
		
		void OnMouseExit () {
			// Make sure it's outside of grid
			Vector3 mousePos = Input.mousePosition;
			Ray ray = Camera.main.ScreenPointToRay (mousePos);
			RaycastHit[] hits = Physics.RaycastAll (Camera.main.transform.position, ray.direction, 5000);
			if (hits.Length > 0) {
				for (int k=0; k<hits.Length; k++) {
					if (hits [k].collider.gameObject == gameObject) 
						return; 
				}
			}
			mouseIsOver = false;
		}


		bool GetLocalHitFromMousePos (out Vector3 localPoint) {
			
			localPoint = Vector3.zero;
			if (!mouseIsOver) return false;
			
			Vector3 mousePos = Input.mousePosition;
			Ray ray = Camera.main.ScreenPointToRay (mousePos);
			RaycastHit[] hits = Physics.RaycastAll (Camera.main.transform.position, ray.direction, 5000);
			if (hits.Length > 0) {
				if (_terrain!=null) {
					for (int k=0; k<hits.Length; k++) {
						if (hits [k].collider.gameObject == _terrain.gameObject) {
							if ( (hits[k].point - Camera.main.transform.position).sqrMagnitude>HIGHLIGHT_NEAR_CLIP_SQR) {
								localPoint = _terrain.transform.InverseTransformPoint (hits [k].point);
								float w = _terrain.terrainData.size.x;
								float d = _terrain.terrainData.size.z;
								localPoint.x = localPoint.x / w - 0.5f;
								localPoint.y = localPoint.z / d - 0.5f;
								return true;
							}
						}
					}
				} else {
					for (int k=0; k<hits.Length; k++) {
						if (hits [k].collider.gameObject == gameObject) {
							localPoint = transform.InverseTransformPoint (hits [k].point);
//							float w = _terrain.terrainData.size.x;
//							float d = _terrain.terrainData.size.z;
//							localPoint.x = localPoint.x / w - 0.5f;
//							localPoint.y = localPoint.z / d - 0.5f;
							return true;
						}
					}
				}
			}
			return false;
		}
		
		void CheckMousePos () {
			if (_highlightMode == HIGHLIGHT_MODE.None || !Application.isPlaying)
				return;
			
			Vector3 localPoint;
			bool goodHit = GetLocalHitFromMousePos (out localPoint);
			if (!goodHit) {
				HideTerritoryRegionHighlight ();
				return;
			}

			// verify if last highlighted territory remains active
			bool sameTerritoryHighlight = false;
			if (_territoryHighlightedIndex >= 0) {
				if (ContainsPoint (localPoint.x, localPoint.y, _territoryHighlighted.region.points)) { 
					sameTerritoryHighlight = true;
				}
			}
			int newTerritoryHighlightedIndex = -1;

			if (!sameTerritoryHighlight) {
				// mouse if over the grid - verify if hitPos is inside any territory polygon
				if (territories != null) {
					for (int c=0; c<territories.Count; c++) {
							if (ContainsPoint (localPoint.x, localPoint.y, territories [c].region.points)) { 
								newTerritoryHighlightedIndex = c;
								c = territories.Count; // Exits for
								break;
							}
						}	
					}
			}

			// verify if last highlited cell remains active
			bool sameCellHighlight = false;
			if (_cellHighlightedIndex >= 0) {
				if (ContainsPoint (localPoint.x, localPoint.y, _cellHighlighted.region.points)) { 
					sameCellHighlight = true;
				}
			}
			int newCellHighlightedIndex = -1;

			if (!sameCellHighlight) {
				if (_highlightMode == HIGHLIGHT_MODE.Cells) {
					if (_territoryHighlightedIndex >= 0) {
						for (int p=0; p<_territoryHighlighted.cells.Count; p++) {
							Cell cell = _territoryHighlighted.cells [p];
								if (ContainsPoint (localPoint.x, localPoint.y, cell.region.points)) {
									newCellHighlightedIndex = GetCellIndex (_cellHighlighted);
									p = _territoryHighlighted.cells.Count; // exits for
									break;
								}
						}
					} else {
						for (int p=0; p<cells.Count; p++) {
							Cell cell = cells [p];
								if (ContainsPoint (localPoint.x, localPoint.y, cell.region.points)) {
									newCellHighlightedIndex = p;
									p = cells.Count;  // exits for
									break;
								}
							}
						}
					}
			}

			switch (_highlightMode) {
			case HIGHLIGHT_MODE.Territories:
				if (!sameTerritoryHighlight) {
					if (newTerritoryHighlightedIndex >= 0) {
						HighlightTerritoryRegion (newTerritoryHighlightedIndex, false);
					} else {
						HideTerritoryRegionHighlight ();
					}
				}
				break;
			case HIGHLIGHT_MODE.Cells:
				if (!sameCellHighlight) {
					if (newCellHighlightedIndex >= 0) {
						HighlightCellRegion (newCellHighlightedIndex, false);
					} else {
						HideCellRegionHighlight ();
					}
				}
				break;
			}

			// record last clicked cell/territory
			if (Input.GetMouseButtonDown(0)) {
				_cellLastClickedIndex = _cellHighlightedIndex;
				_territoryLastClickedIndex = _territoryHighlightedIndex;
			}

		}
	

		void UpdateHighlightFade() {
			if (_highlightFadeAmount==0) return;

			if (highlightedObj!=null) {
				float newAlpha = 1.0f - Mathf.PingPong(Time.time - highlightFadeStart, _highlightFadeAmount);
				Material mat = highlightedObj.GetComponent<Renderer>().sharedMaterial;
				Color color = mat.color;
				Color newColor = new Color(color.r, color.g, color.b, newAlpha);
				mat.color = newColor;
			}

		}


	#endregion
	
	
	#region Geometric functions
	
		bool ContainsPoint (float x, float y, List<Vector3> poly) { 
			int numPoints = poly.Count;
			int j = numPoints - 1; 
			bool inside = false; 
			for (int i = 0; i < numPoints; j = i++) { 
				if (((poly [i].y <= y && y < poly [j].y) || (poly [j].y <= y && y < poly [i].y)) && 
					(x < (poly [j].x - poly [i].x) * (y - poly [i].y) / (poly [j].y - poly [i].y) + poly [i].x))  
					inside = !inside; 
			} 
			return inside; 
		}
	
	
	#endregion

		
		
		#region Territory highlighting
		
		void HideTerritoryRegionHighlight () {
			HideCellRegionHighlight ();
			if (_territoryHighlighted == null)
				return;
			if (highlightedObj != null) {
				if (_territoryHighlighted != null && _territoryHighlighted.region.customMaterial != null) {
					ApplyMaterialToSurface (highlightedObj, _territoryHighlighted.region.customMaterial);
					if (!_colorizeTerritories) highlightedObj.SetActive (false);
				} else {
					highlightedObj.SetActive (false);
				}
				highlightedObj = null;
			}
			_territoryHighlighted = null;
			_territoryHighlightedIndex = -1;
		}

		/// <summary>
		/// Highlights the territory region specified. Returns the generated highlight surface gameObject.
		/// Internally used by the Map UI and the Editor component, but you can use it as well to temporarily mark a territory region.
		/// </summary>
		/// <param name="refreshGeometry">Pass true only if you're sure you want to force refresh the geometry of the highlight (for instance, if the frontiers data has changed). If you're unsure, pass false.</param>
		public GameObject HighlightTerritoryRegion (int territoryIndex, bool refreshGeometry) {
			if (highlightedObj!=null) HideTerritoryRegionHighlight();
			if (territoryIndex<0 || territoryIndex>=territories.Count) return null;
			int cacheIndex = GetCacheIndexForTerritoryRegion (territoryIndex); 
			bool existsInCache = surfaces.ContainsKey (cacheIndex);
			if (refreshGeometry && existsInCache) {
				GameObject obj = surfaces [cacheIndex];
				surfaces.Remove(cacheIndex);
				DestroyImmediate(obj);
				existsInCache = false;
			}
			if (existsInCache) {
				highlightedObj = surfaces [cacheIndex];
				if (highlightedObj==null) {
					surfaces.Remove(cacheIndex);
				} else {
					if (!highlightedObj.activeSelf)
						highlightedObj.SetActive (true);
					Renderer rr = highlightedObj.GetComponent<Renderer> ();
					if (rr.sharedMaterial!=hudMatTerritory)
						rr.sharedMaterial = hudMatTerritory;
				}
			} else {
				highlightedObj = GenerateTerritoryRegionSurface (territoryIndex, hudMatTerritory, Vector2.one, Vector2.zero, 0);
			}

			_territoryHighlightedIndex = territoryIndex;
			_territoryHighlighted = territories[territoryIndex];
			return highlightedObj;
		}
		
		GameObject GenerateTerritoryRegionSurface (int territoryIndex, Material material) {
			return GenerateTerritoryRegionSurface(territoryIndex, material, Vector2.one, Vector2.zero, 0);
		}
		
		GameObject GenerateTerritoryRegionSurface (int territoryIndex, Material material, Vector2 textureScale, Vector2 textureOffset, float textureRotation) {
			if (territoryIndex<0 || territoryIndex>=territories.Count) return null;
			Region region = territories [territoryIndex].region;

			// Calculate region's surface points
			int numSegments = region.segments.Count;
			Connector connector = new Connector();
			if (_terrain==null) {
				connector.AddRange(region.segments);
			} else {
				for (int i = 0; i<numSegments; i++) {
					Segment s = region.segments[i];
					SurfaceSegmentForSurface(s, connector);
				}
			}
			Geom.Polygon surfacedPolygon = connector.ToPolygonFromLargestLineStrip();
			List<Point> surfacedPoints = surfacedPolygon.contours[0].points;
			
			List<PolygonPoint> ppoints = new List<PolygonPoint>(surfacedPoints.Count);
			for (int k=0;k<surfacedPoints.Count;k++) {
				double x = surfacedPoints[k].x+2;
				double y = surfacedPoints[k].y+2;
				if (!IsTooNearPolygon(x, y, ppoints)) {
					float h = _terrain!=null ? _terrain.SampleHeight(transform.TransformPoint((float)x-2, (float)y-2,0)): 0;
					ppoints.Add (new PolygonPoint(x, y, h));
				}
			}
			Poly2Tri.Polygon poly = new Poly2Tri.Polygon(ppoints);
			
			if (_terrain!=null) {

				if (steinerPoints==null) {
					steinerPoints = new List<TriangulationPoint>(6000);
				} else {
					steinerPoints.Clear();
				}

				float stepX = 1.0f / heightMapWidth;
				float smallStep = 1.0f / heightMapWidth;
				float y = region.rect2D.yMin + smallStep;
				float ymax = region.rect2D.yMax - smallStep;
				float[] acumY = new float[terrainRoughnessMapWidth];
				while(y<ymax) {
					int j = (int)((y + 0.5f) * terrainRoughnessMapHeight); // * heightMapHeight)) / TERRAIN_CHUNK_SIZE;
					if (j>=terrainRoughnessMapHeight) j=terrainRoughnessMapHeight-1;
					float sy = y + 2;
					float xin = GetFirstPointInRow(sy, ppoints) + smallStep;
					float xout = GetLastPointInRow(sy, ppoints) - smallStep;
					int k0 = -1;
					for (float x = xin; x<xout; x+=stepX) {
						int k = (int)((x + 0.5f) * terrainRoughnessMapWidth); //)) / TERRAIN_CHUNK_SIZE;
						if (k>=terrainRoughnessMapWidth) k=terrainRoughnessMapWidth-1;
						if (k0!=k) {
							k0=k;
							stepX = terrainRoughnessMap[j,k];
							if (acumY[k] >= stepX) acumY[k] = 0;
							acumY[k] += smallStep;
						}
						if (acumY[k] >= stepX) {
							// Gather precision height
							float h = _terrain.SampleHeight (transform.TransformPoint(x,y,0));
							float htl = _terrain.SampleHeight (transform.TransformPoint (x-smallStep, y+smallStep, 0));
							if (htl>h) h = htl;
							float htr = _terrain.SampleHeight (transform.TransformPoint (x+smallStep, y+smallStep, 0));
							if (htr>h) h = htr;
							float hbr = _terrain.SampleHeight (transform.TransformPoint (x+smallStep, y-smallStep, 0));
							if (hbr>h) h = hbr;
							float hbl = _terrain.SampleHeight (transform.TransformPoint (x-smallStep, y-smallStep, 0));
							if (hbl>h) h = hbl;
							steinerPoints.Add (new PolygonPoint (x+2, sy, h));		
						}
					}
					y += smallStep;
					if (steinerPoints.Count>80000) {
						break;
					}
				}
				poly.AddSteinerPoints(steinerPoints);
			}

			P2T.Triangulate(poly);
			
			Vector3[] revisedSurfPoints = new Vector3[poly.Triangles.Count*3];
			
			if (_gridNormalOffset>0) {
				for (int k=0;k<poly.Triangles.Count;k++) {
					DelaunayTriangle dt = poly.Triangles[k];
					float x = dt.Points[0].Xf-2;
					float y = dt.Points[0].Yf-2;
					float z = -dt.Points[0].Zf;
					Vector3 nd = transform.InverseTransformVector(_terrain.terrainData.GetInterpolatedNormal(x+0.5f,y+0.5f)) * _gridNormalOffset;
					revisedSurfPoints[k*3].x = x + nd.x;
					revisedSurfPoints[k*3].y = y + nd.y;
					revisedSurfPoints[k*3].z = z + nd.z;
					
					x = dt.Points[2].Xf-2;
					y = dt.Points[2].Yf-2;
					z = -dt.Points[2].Zf;
					nd = transform.InverseTransformVector(_terrain.terrainData.GetInterpolatedNormal(x+0.5f,y+0.5f)) * _gridNormalOffset;
					revisedSurfPoints[k*3+1].x = x + nd.x;
					revisedSurfPoints[k*3+1].y = y + nd.y;
					revisedSurfPoints[k*3+1].z = z + nd.z;
					
					x = dt.Points[1].Xf-2;
					y = dt.Points[1].Yf-2;
					z = -dt.Points[1].Zf;
					nd = transform.InverseTransformVector(_terrain.terrainData.GetInterpolatedNormal(x+0.5f,y+0.5f)) * _gridNormalOffset;
					revisedSurfPoints[k*3+2].x = x + nd.x;
					revisedSurfPoints[k*3+2].y = y + nd.y;
					revisedSurfPoints[k*3+2].z = z + nd.z;
				}
			} else {
				for (int k=0;k<poly.Triangles.Count;k++) {
					DelaunayTriangle dt = poly.Triangles[k];
					revisedSurfPoints[k*3].x = dt.Points[0].Xf-2;
					revisedSurfPoints[k*3].y = dt.Points[0].Yf-2;
					revisedSurfPoints[k*3].z = -dt.Points[0].Zf;
					revisedSurfPoints[k*3+1].x = dt.Points[2].Xf-2;
					revisedSurfPoints[k*3+1].y = dt.Points[2].Yf-2;
					revisedSurfPoints[k*3+1].z = -dt.Points[2].Zf;
					revisedSurfPoints[k*3+2].x = dt.Points[1].Xf-2;
					revisedSurfPoints[k*3+2].y = dt.Points[1].Yf-2;
					revisedSurfPoints[k*3+2].z = -dt.Points[1].Zf;
				}
			}
			int cacheIndex = GetCacheIndexForTerritoryRegion (territoryIndex); 
			string cacheIndexSTR = cacheIndex.ToString();
			// Deletes potential residual surface
			Transform t = surfacesLayer.transform.FindChild(cacheIndexSTR);
			if (t!=null) DestroyImmediate(t.gameObject);
			GameObject surf = Drawing.CreateSurface (cacheIndexSTR, revisedSurfPoints, material);									
			surf.transform.SetParent (surfacesLayer.transform, false);
			surf.transform.localPosition = Vector3.zero;
			surf.layer = gameObject.layer;
			if (surfaces.ContainsKey(cacheIndex)) surfaces.Remove(cacheIndex);
			surfaces.Add (cacheIndex, surf);

			return surf;
		}

		void UpdateColorizedTerritoriesAlpha () {
			if (territories==null) return;
			for (int c=0;c<territories.Count;c++) {
				Territory territory = territories[c];
					int cacheIndex = GetCacheIndexForTerritoryRegion(c);
					if (surfaces.ContainsKey(cacheIndex)) {
						GameObject surf = surfaces[cacheIndex];
						if (surf!=null) {
							Color newColor = surf.GetComponent<Renderer>().sharedMaterial.color;
							newColor.a = territory.fillColor.a * _colorizedTerritoriesAlpha;
							surf.GetComponent<Renderer>().sharedMaterial.color = newColor;
						}
					}
			}
		}

		#endregion


		#region Cell highlighting
		
		int GetCacheIndexForCellRegion (int cellIndex) {
			return 1000000 + cellIndex; // * 1000 + regionIndex;
		}
		
		/// <summary>
		/// Highlights the cell region specified. Returns the generated highlight surface gameObject.
		/// Internally used by the Map UI and the Editor component, but you can use it as well to temporarily mark a territory region.
		/// </summary>
		/// <param name="refreshGeometry">Pass true only if you're sure you want to force refresh the geometry of the highlight (for instance, if the frontiers data has changed). If you're unsure, pass false.</param>
		void HighlightCellRegion (int cellIndex, bool refreshGeometry) {
#if HIGHLIGHT_NEIGHBOURS
			DestroySurfaces();
#endif
			if (highlightedObj!=null) HideCellRegionHighlight();
			if (cellIndex<0 || cellIndex>=cells.Count) return;
			
			int cacheIndex = GetCacheIndexForCellRegion (cellIndex); 
			bool existsInCache = surfaces.ContainsKey (cacheIndex);
			if (refreshGeometry && existsInCache) {
				GameObject obj = surfaces [cacheIndex];
				surfaces.Remove(cacheIndex);
				DestroyImmediate(obj);
				existsInCache = false;
			}
			if (existsInCache) {
				highlightedObj = surfaces [cacheIndex];
				if (highlightedObj!=null) {
					highlightedObj.SetActive (true);
					highlightedObj.GetComponent<Renderer> ().sharedMaterial = hudMatCell;
				} else {
					surfaces.Remove(cacheIndex);
				}
			} else {
				highlightedObj = GenerateCellRegionSurface (cellIndex, hudMatCell);
			}
			_cellHighlighted = cells[cellIndex];
			_cellHighlightedIndex = cellIndex;
			highlightFadeStart = Time.time;

#if HIGHLIGHT_NEIGHBOURS
			for (int k=0;k<cellRegionHighlighted.neighbours.Count;k++) {
				int  ni = GetCellIndex((Cell)cellRegionHighlighted.neighbours[k].entity);
				GenerateCellRegionSurface(ni, 0, hudMatTerritory);
			}
#endif

		}

		void HideCellRegionHighlight () {
			if (cellHighlighted == null)
				return;
			if (highlightedObj != null) {
				if (cellHighlighted.region.customMaterial!=null) {
					ApplyMaterialToSurface (highlightedObj, cellHighlighted.region.customMaterial);
				} else {
					highlightedObj.SetActive (false);
				}
				highlightedObj = null;
			}
			_cellHighlighted = null;
			_cellHighlightedIndex = -1;
		}

		void SurfaceSegmentForSurface(Segment s, Connector connector) {

			// trace the line until roughness is exceeded
			double dist = s.magnitude; // (float)Math.Sqrt ( (p1.x-p0.x)*(p1.x-p0.x) + (p1.y-p0.y)*(p1.y-p0.y));
			Point direction = s.end - s.start;
			
			int numSteps = (int)(dist / MIN_VERTEX_DISTANCE);
			Point t0 = s.start;
			float h0 = _terrain.SampleHeight(transform.TransformPoint(t0.vector3));
			Point ta = t0;
			float h1;
			for (int i=1;i<numSteps;i++) {
				Point t1 = s.start + direction * i / numSteps;
				h1 = _terrain.SampleHeight(transform.TransformPoint(t1.vector3));
				if (h0 < h1 || h0-h1 > effectiveRoughness) { //-effectiveRoughness) {
					if (t0!=ta) {
						Segment s0 = new Segment(t0, ta, s.border);
						connector.Add (s0);
						Segment s1 = new Segment(ta, t1, s.border);
						connector.Add (s1);
					} else {
						Segment s0 = new Segment(t0, t1, s.border);
						connector.Add (s0);
					}
					t0 = t1;
					h0 = h1;
				}
				ta = t1;
			}
			// Add last point
			Segment finalSeg = new Segment(t0, s.end, s.border);
			connector.Add (finalSeg);

		}


		float GetFirstPointInRow(float y, List<PolygonPoint>points) {
			int max = points.Count-1;
			float minx=1000;
			for (int k=0;k<=max;k++) {
				PolygonPoint p0 = points[k];
				PolygonPoint p1;
				if (k==max) {
					p1 = points[0];
				} else {
					p1 = points[k+1];
				}
				// if line crosses the horizontal line
				if (p0.Y>=y && p1.Y<=y || p0.Y<=y && p1.Y>=y) {
					float x;
					if (p1.Xf==p0.Xf) {
						x = p0.Xf;
					} else {
						float a = (p1.Xf - p0.Xf)/(p1.Yf - p0.Yf);
						x = p0.Xf + a * (y - p0.Yf);
					}
					if (x<minx) minx=x;
				}
			}
			return minx-2;
		}

		float GetLastPointInRow(float y, List<PolygonPoint>points) {
			int max = points.Count-1;
			float maxx=-1000;
			for (int k=0;k<=max;k++) {
				PolygonPoint p0 = points[k];
				PolygonPoint p1;
				if (k==max) {
					p1 = points[0];
				} else {
					p1 = points[k+1];
				}
				// if line crosses the horizontal line
				if (p0.Yf>=y && p1.Yf<=y || p0.Yf<=y && p1.Yf>=y) {
					float x;
					if (p1.X==p0.Xf) {
						x = p0.Xf;
					} else {
						float a = (p1.Xf - p0.Xf)/(p1.Yf - p0.Yf);
						x = p0.Xf + a * (y - p0.Yf);
					}
					if (x>maxx) maxx=x;
				}
			}
			return maxx-2;
		}

		bool IsTooNearPolygon(double x, double y, List<PolygonPoint> points) {
			for (int j=0;j<points.Count;j++) {
				PolygonPoint p1 = points[j];
				if ((x-p1.X)*(x-p1.X)+(y-p1.Y)*(y-p1.Y) < SQR_MIN_VERTEX_DIST) {
					return true;
				}
			}
			return false;
		}

		GameObject GenerateCellRegionSurface (int cellIndex, Material material) {
			if (cellIndex<0 || cellIndex>=cells.Count) return null;
			Region region = cells [cellIndex].region;

			// Calculate region's surface points
			int numSegments = region.segments.Count;
			Connector connector = new Connector();
			if (_terrain==null) {
				connector.AddRange(region.segments);
			} else {
				for (int i = 0; i<numSegments; i++) {
					Segment s = region.segments[i];
					SurfaceSegmentForSurface(s, connector);
				}
			}
			Geom.Polygon surfacedPolygon = connector.ToPolygonFromLargestLineStrip();
			List<Point> surfacedPoints = surfacedPolygon.contours[0].points;

			List<PolygonPoint> ppoints = new List<PolygonPoint>(surfacedPoints.Count);
			for (int k=0;k<surfacedPoints.Count;k++) {
				double x = surfacedPoints[k].x+2;
				double y = surfacedPoints[k].y+2;
				if (!IsTooNearPolygon(x, y, ppoints)) {
					float h = _terrain!=null ? _terrain.SampleHeight(transform.TransformPoint((float)x-2, (float)y-2,0)): 0;
					ppoints.Add (new PolygonPoint(x, y, h));
				}
			}
			Poly2Tri.Polygon poly = new Poly2Tri.Polygon(ppoints);

			if (_terrain!=null) {
				if (steinerPoints==null) {
					steinerPoints = new List<TriangulationPoint>(6000);
				} else {
					steinerPoints.Clear();
				}
				
				float stepX = 1.0f / heightMapWidth;
				float smallStep = 1.0f / heightMapWidth;
				float y = region.rect2D.yMin + smallStep;
				float ymax = region.rect2D.yMax - smallStep;
				float[] acumY = new float[terrainRoughnessMapWidth];
				while(y<ymax) {
					int j = (int)((y + 0.5f) * terrainRoughnessMapHeight); // * heightMapHeight)) / TERRAIN_CHUNK_SIZE;
					if (j>=0) {
					if (j>=terrainRoughnessMapHeight) j=terrainRoughnessMapHeight-1;
					float sy = y + 2;
					float xin = GetFirstPointInRow(sy, ppoints) + smallStep;
					float xout = GetLastPointInRow(sy, ppoints) - smallStep;
					int k0 = -1;
					for (float x = xin; x<xout; x+=stepX) {
						int k = (int)((x + 0.5f) * terrainRoughnessMapWidth); //)) / TERRAIN_CHUNK_SIZE;
						if (k>=terrainRoughnessMapWidth) k=terrainRoughnessMapWidth-1;
						if (k0!=k) {
							k0=k;
							stepX = terrainRoughnessMap[j,k];
							if (acumY[k] >= stepX) acumY[k] = 0;
							acumY[k] += smallStep;
						}
						if (acumY[k] >= stepX) {
							// Gather precision height
							float h = _terrain.SampleHeight (transform.TransformPoint(x,y,0));
							float htl = _terrain.SampleHeight (transform.TransformPoint (x-smallStep, y+smallStep, 0));
							if (htl>h) h = htl;
							float htr = _terrain.SampleHeight (transform.TransformPoint (x+smallStep, y+smallStep, 0));
							if (htr>h) h = htr;
							float hbr = _terrain.SampleHeight (transform.TransformPoint (x+smallStep, y-smallStep, 0));
							if (hbr>h) h = hbr;
							float hbl = _terrain.SampleHeight (transform.TransformPoint (x-smallStep, y-smallStep, 0));
							if (hbl>h) h = hbl;
							steinerPoints.Add (new PolygonPoint (x+2, sy, h));		
						}
					}
					}
					y += smallStep;
					if (steinerPoints.Count>80000) {
						break;
					}
				}
				poly.AddSteinerPoints(steinerPoints);
			}

			P2T.Triangulate(poly);

			// Calculate & optimize mesh data
			int pointCount = poly.Triangles.Count*3;
			List<Vector3> meshPoints = new List<Vector3> (pointCount);
			int[] triNew = new int[pointCount];
			if (surfaceMeshHit == null)
				surfaceMeshHit = new Dictionary<TriangulationPoint, int> (2000);
			else
				surfaceMeshHit.Clear ();

			int triNewIndex =-1;
			int newPointsCount = -1;

			if (_gridNormalOffset>0) {
				for (int k=0;k<poly.Triangles.Count;k++) {
					DelaunayTriangle dt = poly.Triangles[k];
					TriangulationPoint p = dt.Points [0];
					if (surfaceMeshHit.ContainsKey (p)) {
						triNew [++triNewIndex] = surfaceMeshHit [p];
					} else {
						Vector3 np = new Vector3(p.Xf-2, p.Yf-2, -p.Zf);
						np += transform.InverseTransformVector(_terrain.terrainData.GetInterpolatedNormal(np.x+0.5f,np.y+0.5f)) * _gridNormalOffset;
						meshPoints.Add (np);
						surfaceMeshHit.Add (p, ++newPointsCount);
						triNew [++triNewIndex] = newPointsCount;
					}
					p = dt.Points [2];
					if (surfaceMeshHit.ContainsKey (p)) {
						triNew [++triNewIndex] = surfaceMeshHit [p];
					} else {
						Vector3 np = new Vector3(p.Xf-2, p.Yf-2, -p.Zf);
						np += transform.InverseTransformVector(_terrain.terrainData.GetInterpolatedNormal(np.x+0.5f,np.y+0.5f)) * _gridNormalOffset;
						meshPoints.Add (np);
						surfaceMeshHit.Add (p, ++newPointsCount);
						triNew [++triNewIndex] = newPointsCount;
					}
					p = dt.Points [1];
					if (surfaceMeshHit.ContainsKey (p)) {
						triNew [++triNewIndex] = surfaceMeshHit [p];
					} else {
						Vector3 np = new Vector3(p.Xf-2, p.Yf-2, -p.Zf);
						np += transform.InverseTransformVector(_terrain.terrainData.GetInterpolatedNormal(np.x+0.5f,np.y+0.5f)) * _gridNormalOffset;
						meshPoints.Add (np);
						surfaceMeshHit.Add (p, ++newPointsCount);
						triNew [++triNewIndex] = newPointsCount;
					}
				}
			} else {
				for (int k=0;k<poly.Triangles.Count;k++) {
					DelaunayTriangle dt = poly.Triangles[k];
					TriangulationPoint p = dt.Points [0];
					if (surfaceMeshHit.ContainsKey (p)) {
						triNew [++triNewIndex] = surfaceMeshHit [p];
					} else {
						Vector3 np = new Vector3(p.Xf-2, p.Yf-2, -p.Zf);
						meshPoints.Add (np);
						surfaceMeshHit.Add (p, ++newPointsCount);
						triNew [++triNewIndex] = newPointsCount;
					}
					p = dt.Points [2];
					if (surfaceMeshHit.ContainsKey (p)) {
						triNew [++triNewIndex] = surfaceMeshHit [p];
					} else {
						Vector3 np = new Vector3(p.Xf-2, p.Yf-2, -p.Zf);
						meshPoints.Add (np);
						surfaceMeshHit.Add (p, ++newPointsCount);
						triNew [++triNewIndex] = newPointsCount;
					}
					p = dt.Points [1];
					if (surfaceMeshHit.ContainsKey (p)) {
						triNew [++triNewIndex] = surfaceMeshHit [p];
					} else {
						Vector3 np = new Vector3(p.Xf-2, p.Yf-2, -p.Zf);
						meshPoints.Add (np);
						surfaceMeshHit.Add (p, ++newPointsCount);
						triNew [++triNewIndex] = newPointsCount;
					}
				}
			}

			int cacheIndex = GetCacheIndexForCellRegion (cellIndex); 
			string cacheIndexSTR = cacheIndex.ToString();
			// Deletes potential residual surface
			Transform t = surfacesLayer.transform.FindChild(cacheIndexSTR);
			if (t!=null) DestroyImmediate(t.gameObject);
			GameObject surf = Drawing.CreateSurface (cacheIndexSTR, meshPoints.ToArray(), triNew, material);									
			_lastVertexCount += surf.GetComponent<MeshFilter>().sharedMesh.vertexCount;
			surf.transform.SetParent (surfacesLayer.transform, false);
			surf.transform.localPosition = Vector3.zero;
			surf.layer = gameObject.layer;
			if (surfaces.ContainsKey(cacheIndex)) surfaces.Remove(cacheIndex);
			surfaces.Add (cacheIndex, surf);
			return surf;
		}
		
		#endregion
		


	}
}