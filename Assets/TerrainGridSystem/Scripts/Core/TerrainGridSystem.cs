using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using TGS.Geom;

namespace TGS {
	
	public enum HIGHLIGHT_MODE {
		None = 0,
		Territories = 1,
		Cells = 2
	}

	public enum OVERLAY_MODE {
		Overlay = 0,
		Ground = 1
	}

	public enum GRID_TOPOLOGY {
		Irregular = 0,
		Box = 1,
		Rectangular = 2,
		Hexagonal = 3
	}


	[Serializable]
	public partial class TerrainGridSystem : MonoBehaviour {

		[NonSerialized]
		public List<Territory> territories;

		/// <summary>
		/// Complete array of states and cells and the territory name they belong to.
		/// </summary>
		[NonSerialized]
		public List<Cell> cells;


		[SerializeField]
		Terrain _terrain;
		public Terrain terrain {
			get {
				return _terrain;
			}
			set {
				if (_terrain!=value)  {
					_terrain = value;
					isDirty = true;
					Redraw ();
				}
			}
		}

		public Vector3 terrainCenter {
			get {
				return _terrain.transform.position + new Vector3(terrainWidth * 0.5f, 0, terrainDepth * 0.5f);
			}
		}

		[SerializeField]
		GRID_TOPOLOGY _gridTopology = GRID_TOPOLOGY.Irregular;
		public GRID_TOPOLOGY gridTopology { 
			get { return _gridTopology; } 
			set { if (_gridTopology!=value) {
					_gridTopology = value;
					GenerateMap();
					isDirty = true;
				}
			}
		}

		[SerializeField]
		int _seed = 1;
		public int seed { 
			get { return _seed; } 
			set { if (_seed!=value) {
					_seed = value;
					GenerateMap();
					isDirty = true;
				}
			}
		}

		[SerializeField]
		int _gridRelaxation = 1;
		public int gridRelaxation { 
			get { return _gridRelaxation; } 
			set { if (_gridRelaxation!=value) {
					_gridRelaxation = value;
					GenerateMap();
					isDirty = true;
				}
			}
		}

		float goodGridRelaxation { get {
				if (_numCells>=MAX_CELLS_FOR_RELAXATION) {
					return 1;
				} else {
					return _gridRelaxation;
				}
			}
		}


		[SerializeField]
		int _numCells = 3;
		public int numCells { 
			get { return _numCells; } 
			set { if (_numCells!=value) {
					_numCells = Mathf.Clamp(value, 1, MAX_CELLS);
					GenerateMap();
					isDirty = true;
				}
			}
		}

		[SerializeField]
		int _numTerritories = 3;
		public int numTerritories { 
			get { return _numTerritories; } 
			set { if (_numTerritories!=value) {
					_numTerritories = Mathf.Clamp(value, 1, MAX_TERRITORIES);
					GenerateMap();
					isDirty = true;
				}
			}
		}

		[SerializeField]
		float _gridCurvature = 0.0f;
		public float gridCurvature { 
			get { return _gridCurvature; } 
			set { if (_gridCurvature!=value) {
					_gridCurvature = value;
					GenerateMap();
					isDirty = true;
				}
			}
		}

		float goodGridCurvature { get {
				if (_numCells>=MAX_CELLS_FOR_CURVATURE) {
					return 0;
				} else {
					return _gridCurvature;
				}
			}
		}

		[SerializeField]
		HIGHLIGHT_MODE _highlightMode = HIGHLIGHT_MODE.Cells;
		
		public HIGHLIGHT_MODE highlightMode {
			get {
				return _highlightMode;
			}
			set {
				if (_highlightMode != value) {
					_highlightMode = value;
					isDirty = true;
					HideCellRegionHighlight();
					HideTerritoryRegionHighlight();
					CheckCells();
					CheckTerritories();
				}
			}
		}

		[SerializeField]
		float _highlightFadeAmount = 0.5f;

		public float highlightFadeAmount {
			get {
				return _highlightFadeAmount;
			}
			set {
				if (_highlightFadeAmount!=value) {
					_highlightFadeAmount = value;
					isDirty = true;
				}
			}
		}

		[SerializeField]
		OVERLAY_MODE _overlayMode = OVERLAY_MODE.Overlay;
		
		public OVERLAY_MODE overlayMode {
			get {
				return _overlayMode;
			}
			set {
				if (_overlayMode != value) {
					_overlayMode = value;
					isDirty = true;
				}
			}
		}

		[SerializeField]
		bool _showCells = true;
		/// <summary>
		/// Toggle cells frontiers visibility.
		/// </summary>
		public bool showCells { 
			get {
				return _showCells; 
			}
			set {
				if (value != _showCells) {
					_showCells = value;
					isDirty = true;
					if (cellLayer != null) {
						cellLayer.SetActive (_showCells);
					} else if (_showCells) {
						Redraw ();
					}
				}
			}
		}

		[SerializeField]
		bool
			_showTerritories = false;
		
		/// <summary>
		/// Toggle frontiers visibility.
		/// </summary>
		public bool showTerritories { 
			get {
				return _showTerritories; 
			}
			set {
				if (value != _showTerritories) {
					_showTerritories = value;
					isDirty = true;
					if (!_showTerritories && territoryLayer != null) {
						territoryLayer.SetActive (false);
					} else {
						Redraw ();
					}
				}
			}
		}

		[SerializeField]
		bool _colorizeTerritories = false;
		/// <summary>
		/// Toggle colorize countries.
		/// </summary>
		public bool colorizeTerritories { 
			get {
				return _colorizeTerritories; 
			}
			set {
				if (value != _colorizeTerritories) {
					_colorizeTerritories = value;
					isDirty = true;
					if (!_colorizeTerritories && surfacesLayer!=null) {
						DestroySurfaces();
					} else {
						Redraw();
					}
				}
			}
		}

		[SerializeField]
		float _colorizedTerritoriesAlpha = 0.7f;
		public float colorizedTerritoriesAlpha { 
			get { return _colorizedTerritoriesAlpha; } 
			set { if (_colorizedTerritoriesAlpha!=value) {
					_colorizedTerritoriesAlpha = value;
					isDirty = true;
					UpdateColorizedTerritoriesAlpha();
				}
			}
		}


		/// <summary>
		/// Fill color to use when the mouse hovers a territory's region.
		/// </summary>
		[SerializeField]
		Color
			_territoryHighlightColor = new Color (1, 0, 0, 0.7f);
		
		public Color territoryHighlightColor {
			get {
				return _territoryHighlightColor;
			}
			set {
				if (value != _territoryHighlightColor) {
					_territoryHighlightColor = value;
					isDirty = true;
					if (hudMatTerritoryOverlay != null && _territoryHighlightColor != hudMatTerritoryOverlay.color) {
						hudMatTerritoryOverlay.color = _territoryHighlightColor;
					}
					if (hudMatTerritoryGround != null && _territoryHighlightColor != hudMatTerritoryGround.color) {
						hudMatTerritoryGround.color = _territoryHighlightColor;
					}
				}
			}
		}

		
		/// <summary>
		/// Territories border color
		/// </summary>
		[SerializeField]
		Color
			_territoryFrontierColor = new Color (0, 1, 0, 1.0f);
		
		public Color territoryFrontiersColor {
			get {
				if (territoriesMat != null) {
					return territoriesMat.color;
				} else {
					return _territoryFrontierColor;
				}
			}
			set {
				if (value != _territoryFrontierColor) {
					_territoryFrontierColor = value;
					isDirty = true;
					if (territoriesMat != null && _territoryFrontierColor != territoriesMat.color) {
						territoriesMat.color = _territoryFrontierColor;
					}
				}
			}
		}


		public float territoryFrontiersAlpha {
			get {
				return _territoryFrontierColor.a;
			}
			set {
				if (_territoryFrontierColor.a!=value) {
					_territoryFrontierColor = new Color(_territoryFrontierColor.r, _territoryFrontierColor.g, _territoryFrontierColor.b, value);
				}
			}
		}


		/// <summary>
		/// Cells border color
		/// </summary>
		[SerializeField]
		Color
			_cellBorderColor = new Color (0, 1, 0, 1.0f);
		
		public Color cellBorderColor {
			get {
				if (cellsMat != null) {
					return cellsMat.color;
				} else {
					return _cellBorderColor;
				}
			}
			set {
				if (value != _cellBorderColor) {
					_cellBorderColor = value;
					isDirty = true;
					if (cellsMat != null && _cellBorderColor != cellsMat.color) {
						cellsMat.color = _cellBorderColor;
					}
				}
			}
		}

		public float cellBorderAlpha {
			get {
				return _cellBorderColor.a;
			}
			set {
				if (_cellBorderColor.a!=value) {
					cellBorderColor = new Color(_cellBorderColor.r, _cellBorderColor.g, _cellBorderColor.b, Mathf.Clamp01(value));
				}
			}
		}


		/// <summary>
		/// Fill color to use when the mouse hovers a cell's region.
		/// </summary>
		[SerializeField]
		Color
			_cellHighlightColor = new Color (1, 0, 0, 0.7f);
		
		public Color cellHighlightColor {
			get {
				return _cellHighlightColor;
			}
			set {
				if (value != _cellHighlightColor) {
					_cellHighlightColor = value;
					isDirty = true;
					if (hudMatCellOverlay != null && _cellHighlightColor != hudMatCellOverlay.color) {
						hudMatCellOverlay.color = _cellHighlightColor;
					}
					if (hudMatCellGround != null && _cellHighlightColor != hudMatCellGround.color) {
						hudMatCellGround.color = _cellHighlightColor;
					}
				}
			}
		}

		
		[SerializeField]
		float _gridElevation = 0;
		public float gridElevation { 
			get { return _gridElevation; } 
			set { if (_gridElevation!=value) {
					_gridElevation = value;
					isDirty = true;
					FitToTerrain();
				}
			}
		}

		[SerializeField]
		float _gridCameraOffset = 0;
		public float gridCameraOffset { 
			get { return _gridCameraOffset; } 
			set { if (_gridCameraOffset!=value) {
					_gridCameraOffset = value;
					isDirty = true;
					FitToTerrain();
				}
			}
		}

		
		[SerializeField]
		float _gridNormalOffset = 0;
		public float gridNormalOffset { 
			get { return _gridNormalOffset; } 
			set { if (_gridNormalOffset!=value) {
					_gridNormalOffset = value;
					isDirty = true;
					Redraw ();
				}
			}
		}

		
		[SerializeField]
		int _gridDepthOffset = -1;
		public int gridDepthOffset { 
			get { return _gridDepthOffset; } 
			set { if (_gridDepthOffset!=value) {
					_gridDepthOffset = value;
					UpdateMaterialDepthOffset ();
					isDirty = true;
				}
			}
		}
		
		[SerializeField]
		float _gridRoughness = 0.01f;
		public float gridRoughness { 
			get { return _gridRoughness; } 
			set { if (_gridRoughness!=value) {
					_gridRoughness = value;
					isDirty = true;
					Redraw ();
				}
			}
		}



		#region State variables

		public static TerrainGridSystem instance { get {
				if (_instance==null) {
					GameObject o = GameObject.Find ("TerrainGridSystem");
					if (o!=null) {
						_instance = o.GetComponentInChildren<TerrainGridSystem>();
					} else {
						Debug.LogWarning("TerrainGridSystem gameobject not found in the scene!");
					}
				}
				return _instance;
			}
		}

		/// <summary>
		/// Returns Territory under mouse position or null if none.
		/// </summary>
		public Territory territoryHighlighted { get { return _territoryHighlighted; } }
		
		/// <summary>
		/// Returns currently highlighted territory index in the countries list.
		/// </summary>
		public int territoryHighlightedIndex { get { return _territoryHighlightedIndex; } }

		/// <summary>
		/// Returns Territory index which has been clicked
		/// </summary>
		public int territoryLastClickedIndex { get { return _territoryLastClickedIndex; } }

		/// <summary>
		/// Returns Cell under mouse position or null if none.
		/// </summary>
		public Cell cellHighlighted { get { return _cellHighlighted; } }
		
		/// <summary>
		/// Returns current highlighted cell index.
		/// </summary>
		public int cellHighlightedIndex { get { return _cellHighlightedIndex; } }

		/// <summary>
		/// Returns Cell index which has been clicked
		/// </summary>
		public int cellLastClickedIndex { get { return _cellLastClickedIndex; } }


		#endregion


		#region Gameloop events

		void Update() {

			CheckMousePos (); 		// Verify if mouse enter a territory boundary - we only check if mouse is inside the sphere of world
			FitToTerrain();  		// Verify if there're changes in container and adjust the grid mesh accordingly
			UpdateHighlightFade(); 	// Fades current selection
		}


		#endregion



		#region Public API

		
		/// <summary>
		/// Returns the_numCellsrovince in the cells array by its reference.
		/// </summary>
		public int GetCellIndex (Cell cell) {
			//			string searchToken = cell.territoryIndex + "|" + cell.name;
			if (cellLookup.ContainsKey(cell)) 
				return _cellLookup[cell];
			else
				return -1;
		}


		/// <summary>
		/// Colorize the specified cell and region with specified color.
		/// </summary>
		public void ToggleCellRegionSurface (int cellIndex, bool visible, Color color, bool refreshGeometry) {
			if (cellIndex<0 || cellIndex>=cells.Count) return;
			if (!visible) {
				HideCellRegionSurface (cellIndex);
				return;
			}

			int cacheIndex = GetCacheIndexForCellRegion (cellIndex); 
			bool existsInCache = surfaces.ContainsKey (cacheIndex);
			if (existsInCache && surfaces[cacheIndex]==null) {
				surfaces.Remove(cacheIndex);
				existsInCache = false;
			}

			if (refreshGeometry && existsInCache) {
				GameObject obj = surfaces [cacheIndex];
				surfaces.Remove(cacheIndex);
				DestroyImmediate(obj);
				existsInCache = false;
			}
			Material coloredMat = GetColoredTexturedMaterial(color, null);
			Region region = cells[cellIndex].region;
			bool isHighlighted = cellHighlightedIndex == cellIndex;
			GameObject surf;
			if (existsInCache) {
				surf = surfaces [cacheIndex];
				surf.SetActive (true);
				Material surfMaterial = surf.GetComponent<Renderer>().sharedMaterial;
				if (surfMaterial.color!=color && !isHighlighted) {
					region.customMaterial = coloredMat;
					ApplyMaterialToSurface(surf, coloredMat);
				}
			} else {
				surf = GenerateCellRegionSurface (cellIndex, coloredMat);
				region.customMaterial = coloredMat;
			}
			// If it was highlighted, highlight it again
			if (region.customMaterial!=null && isHighlighted && region.customMaterial.color!=hudMatCell.color) {
				Material clonedMat = Instantiate(region.customMaterial);
				clonedMat.name = region.customMaterial.name;
				clonedMat.color = hudMatCell.color;
				surf.GetComponent<Renderer>().sharedMaterial = clonedMat;
				highlightedObj = surf;
			}
		}


		/// <summary>
		/// Uncolorize/hide specified cell by index in the cells collection.
		/// </summary>
		public void HideCellRegionSurface (int cellIndex) {
			if (_cellHighlightedIndex != cellIndex) {
				int cacheIndex = GetCacheIndexForCellRegion (cellIndex);
				if (surfaces.ContainsKey (cacheIndex)) {
					if (surfaces[cacheIndex] == null) {
						surfaces.Remove(cacheIndex);
					} else {
						surfaces [cacheIndex].SetActive (false);
					}
				}
			}
			cells [cellIndex].region.customMaterial = null;
		}


		
		/// <summary>
		/// Uncolorize/hide specified territory by index in the territories collection.
		/// </summary>
		public void HideTerritoryRegionSurface (int territoryIndex) {
			if (_territoryHighlightedIndex != territoryIndex) {
				int cacheIndex = GetCacheIndexForTerritoryRegion (territoryIndex);
				if (surfaces.ContainsKey (cacheIndex)) {
					if (surfaces[cacheIndex] == null) {
						surfaces.Remove(cacheIndex);
					} else {
						surfaces [cacheIndex].SetActive (false);
					}
				}
			}
			territories [territoryIndex].region.customMaterial = null;
		}


		public void ToggleTerritoryRegionSurface (int territoryIndex, bool visible, Color color) {
			ToggleTerritoryRegionSurface (territoryIndex, visible, color, null, Vector2.one, Vector2.zero, 0);
		}


		/// <summary>
		/// Colorize specified region of a territory by indexes.
		/// </summary>
		public void ToggleTerritoryRegionSurface (int territoryIndex, bool visible, Color color, Texture2D texture, Vector2 textureScale, Vector2 textureOffset, float textureRotation) {
			if (!visible) {
				HideTerritoryRegionSurface (territoryIndex);
				return;
			}
			GameObject surf = null;
			Region region = territories [territoryIndex].region;
			int cacheIndex = GetCacheIndexForTerritoryRegion (territoryIndex);
			// Checks if current cached surface contains a material with a texture, if it exists but it has not texture, destroy it to recreate with uv mappings
			if (surfaces.ContainsKey (cacheIndex) && surfaces [cacheIndex] != null) 
				surf = surfaces [cacheIndex];
			
			// Should the surface be recreated?
			Material surfMaterial;
			if (surf != null) {
				surfMaterial = surf.GetComponent<Renderer> ().sharedMaterial;
				if (texture != null && (region.customMaterial == null || textureScale != region.customTextureScale || textureOffset != region.customTextureOffset || 
				                        textureRotation != region.customTextureRotation || !region.customMaterial.name.Equals (texturizedMat.name))) {
					surfaces.Remove (cacheIndex);
					DestroyImmediate (surf);
					surf = null;
				}
			}
			// If it exists, activate and check proper material, if not create surface
			bool isHighlighted = territoryHighlightedIndex == territoryIndex;
			if (surf != null) {
				if (!surf.activeSelf)
					surf.SetActive (true);
				// Check if material is ok
				surfMaterial = surf.GetComponent<Renderer> ().sharedMaterial;
				if ((texture == null && !surfMaterial.name.Equals (coloredMat.name)) || (texture != null && !surfMaterial.name.Equals (texturizedMat.name)) 
				    || (surfMaterial.color != color && !isHighlighted) || (texture != null && region.customMaterial.mainTexture != texture)) {
					Material goodMaterial = GetColoredTexturedMaterial (color, texture);
					region.customMaterial = goodMaterial;
					ApplyMaterialToSurface (surf, goodMaterial);
				}
			} else {
				surfMaterial = GetColoredTexturedMaterial (color, texture);
				surf = GenerateTerritoryRegionSurface (territoryIndex, surfMaterial, textureScale, textureOffset, textureRotation);
				region.customMaterial = surfMaterial;
				region.customTextureOffset = textureOffset;
				region.customTextureRotation = textureRotation;
				region.customTextureScale = textureScale;
			}
			// If it was highlighted, highlight it again
			if (region.customMaterial != null && isHighlighted && region.customMaterial.color != hudMatTerritory.color) {
				Material clonedMat = Instantiate (region.customMaterial);
				clonedMat.hideFlags = HideFlags.DontSave;
				clonedMat.name = region.customMaterial.name;
				clonedMat.color = hudMatTerritory.color;
				surf.GetComponent<Renderer> ().sharedMaterial = clonedMat;
				highlightedObj = surf;
			}
			_lastVertexCount += surf.GetComponent<MeshFilter>().sharedMesh.vertexCount;

		}

		/// <summary>
		/// Gets the cell's center position in world space.
		/// </summary>
		public Vector3 GetCellPosition(int cellIndex) {
			Vector2 cellGridCenter = cells[cellIndex].center;
			Vector3 localCenter = new Vector3((cellGridCenter.x+0.5f) * terrainWidth, 0, (cellGridCenter.y+0.5f) * terrainDepth);
			return terrain.transform.TransformPoint(localCenter);
		}

		/// <summary>
		/// Returns a list of neighbour cells for specificed cell index.
		/// </summary>
		public List<Cell> GetCellNeighbours(int cellIndex) {
			List<Cell>neighbours = new List<Cell>();
			Region region = cells[cellIndex].region;
			for (int k=0;k<region.neighbours.Count;k++) {
				neighbours.Add ( (Cell)region.neighbours[k].entity);
			}
			return neighbours;
		}

		/// <summary>
		/// Returns a list of neighbour cells for specificed cell index.
		/// </summary>
		public List<Territory> GetTerritoryNeighbours(int territoryIndex) {
			List<Territory>neighbours = new List<Territory>();
			Region region = territories[territoryIndex].region;
			for (int k=0;k<region.neighbours.Count;k++) {
				neighbours.Add ( (Territory)region.neighbours[k].entity);
			}
			return neighbours;
		}

		/// <summary>
		/// Merges cell2 into cell1. Cell2 is removed.
		/// Only cells which are neighbours can be merged.
		/// </summary>
		public bool CellMerge(Cell cell1, Cell cell2) {
			if (cell1==null || cell2==null) return false;
			if (!cell1.region.neighbours.Contains(cell2.region)) return false;
			cell1.center = (cell2.center + cell1.center)/2.0f;
			// Polygon UNION operation between both regions
			PolygonClipper pc = new PolygonClipper(cell1.polygon, cell2.polygon);
			pc.Compute(PolygonOp.UNION);
			// Remove cell2 from lists
			int territoryIndex = cell2.territoryIndex;
			if (territories[territoryIndex].cells.Contains(cell2)) territories[territoryIndex].cells.Remove(cell2);
			if (cells.Contains(cell2)) cells.Remove(cell2);
			// Updates geometry data on cell1
			UpdateCellGeometry(cell1, pc.subject);
			return true;
		}

		#endregion


	
	}
}

