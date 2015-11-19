using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using TGS;

namespace TGS_Editor {
	[CustomEditor(typeof(TerrainGridSystem))]
	public class TGSInspector : UnityEditor.Editor {

		TerrainGridSystem tgs;
		Texture2D _headerTexture, _blackTexture;
		string[] selectionModeOptions, reshapeExplanation, topologyOptions, overlayModeOptions;
		GUIStyle blackStyle;
		GUIContent[] reshapeToolbarIcons;

		void OnEnable () {

			_blackTexture = MakeTex (4, 4, new Color (0.18f, 0.18f, 0.18f));
			_blackTexture.hideFlags = HideFlags.DontSave;
			_headerTexture = Resources.Load<Texture2D> ("EditorHeader");
			blackStyle = new GUIStyle ();
			blackStyle.normal.background = _blackTexture;

			selectionModeOptions = new string[] { "None", "Territories", "Cells" };
			overlayModeOptions = new string[] { "Overlay", "Ground" };
			topologyOptions = new string[] { "Irregular", "Box", "Rectangular", "Hexagonal" };

			// Load UI icons
			Texture2D[] icons = new Texture2D[5];
			icons [(int)RESHAPE_MODE.NONE] = Resources.Load<Texture2D> ("IconLock");
			icons [(int)RESHAPE_MODE.ADD] = Resources.Load<Texture2D> ("IconPen");
			icons [(int)RESHAPE_MODE.MOVE] = Resources.Load<Texture2D> ("IconMove");
			icons [(int)RESHAPE_MODE.ERASE] = Resources.Load<Texture2D> ("IconEraser");
			icons [(int)RESHAPE_MODE.MERGE] = Resources.Load<Texture2D> ("IconMerge");

			reshapeToolbarIcons = new GUIContent[5];
			reshapeToolbarIcons [(int)RESHAPE_MODE.NONE] = new GUIContent ("", icons [(int)RESHAPE_MODE.NONE], "");
			reshapeToolbarIcons [(int)RESHAPE_MODE.ADD] = new GUIContent ("Add", icons [(int)RESHAPE_MODE.ADD], "Add a new cell to the grid");
			reshapeToolbarIcons [(int)RESHAPE_MODE.MOVE] = new GUIContent ("Move", icons [(int)RESHAPE_MODE.MOVE], "Move a border point");
			reshapeToolbarIcons [(int)RESHAPE_MODE.ERASE] = new GUIContent ("Erase", icons [(int)RESHAPE_MODE.ERASE], "Remove a cell");
			reshapeToolbarIcons [(int)RESHAPE_MODE.MERGE] = new GUIContent ("Merge", icons [(int)RESHAPE_MODE.MERGE], "Merge two cells");
			reshapeExplanation = new string[5];
			reshapeExplanation [(int)RESHAPE_MODE.ADD] = "CLICK on the grid to add a new cell ";
			reshapeExplanation [(int)RESHAPE_MODE.MOVE] = "DRAG a border point on the grid"; 
			reshapeExplanation [(int)RESHAPE_MODE.ERASE] = "CLICK on any cell to delete it."; 
			reshapeExplanation [(int)RESHAPE_MODE.MERGE] = "CLICK on two or more cells in sequence to merge them."; 

			tgs = (TerrainGridSystem)target;
			if (tgs.territories == null) {
				tgs.Init ();
			}
			HideEditorMesh();
		}

		public override void OnInspectorGUI () {
			tgs.isDirty = false;

			EditorGUILayout.Separator ();
			GUI.skin.label.alignment = TextAnchor.MiddleCenter;  
			GUILayout.Label (_headerTexture, GUILayout.ExpandWidth (true));
			GUI.skin.label.alignment = TextAnchor.MiddleLeft;  

			EditorGUILayout.BeginVertical (blackStyle);

			DrawTitleLabel("Grid Configuration");

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label ("Topology", GUILayout.Width (120));
			tgs.gridTopology = (GRID_TOPOLOGY) EditorGUILayout.Popup((int)tgs.gridTopology, topologyOptions);
			EditorGUILayout.EndHorizontal ();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Territories", GUILayout.Width(120));
			tgs.numTerritories = EditorGUILayout.IntSlider (tgs.numTerritories, 1, Mathf.Min (tgs.numCells, TerrainGridSystem.MAX_TERRITORIES));
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Cells (aprox.)", GUILayout.Width(120));
			tgs.numCells = EditorGUILayout.IntSlider (tgs.numCells, 2, TerrainGridSystem.MAX_CELLS);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Curvature", GUILayout.Width(120));
			if (tgs.numCells>TerrainGridSystem.MAX_CELLS_FOR_CURVATURE) {
				DrawInfoLabel("not available with >" + TerrainGridSystem.MAX_CELLS_FOR_CURVATURE + " cells");
			} else {
				tgs.gridCurvature = EditorGUILayout.Slider (tgs.gridCurvature, 0, 0.1f);
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Relaxation", GUILayout.Width(120));
			if (tgs.gridTopology != GRID_TOPOLOGY.Irregular) {
				DrawInfoLabel("only available with irregular topology");
			} else if (tgs.numCells>TerrainGridSystem.MAX_CELLS_FOR_RELAXATION) {
				DrawInfoLabel("not available with >" + TerrainGridSystem.MAX_CELLS_FOR_RELAXATION + " cells");
			} else {
				tgs.gridRelaxation = EditorGUILayout.IntSlider (tgs.gridRelaxation, 1, 32);
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Roughness", GUILayout.Width(120));
			tgs.gridRoughness = EditorGUILayout.Slider (tgs.gridRoughness, 0.01f, 0.2f);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Seed", GUILayout.Width(120));
			tgs.seed = EditorGUILayout.IntSlider (tgs.seed, 1, 10000);
			if (GUILayout.Button("Redraw")) {
				tgs.Redraw();
			}
			EditorGUILayout.EndHorizontal ();

			int cellsCreated = tgs.cells == null ? 0: tgs.cells.Count;
			int territoriesCreated = tgs.territories == null ? 0: tgs.territories.Count;

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			DrawInfoLabel("Cells Created: " + cellsCreated + " / Territories Created: " + territoriesCreated + " / Vertex Count: " + tgs.lastVertexCount);
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal ();

			EditorGUILayout.EndVertical ();
			EditorGUILayout.Separator();
			EditorGUILayout.BeginVertical (blackStyle);

			/*
			DrawTitleLabel("Grid Maintenance");

			EditorGUILayout.BeginHorizontal ();
			GUILayout.FlexibleSpace ();
			int selectionGridRows = (reshapeToolbarIcons.Length - 1) / 5 + 1;
			GUIStyle selectionGridStyle = new GUIStyle (GUI.skin.button);
			selectionGridStyle.margin = new RectOffset (2, 2, 2, 2);
			ths.reshapeMode = (RESHAPE_MODE)GUILayout.SelectionGrid ((int)ths.reshapeMode, reshapeToolbarIcons, 5, selectionGridStyle, GUILayout.Height (24 * selectionGridRows), GUILayout.MaxWidth (340));
			GUILayout.FlexibleSpace ();
			EditorGUILayout.EndHorizontal ();

			if (ths.reshapeMode != RESHAPE_MODE.NONE) {
				EditorGUILayout.BeginHorizontal ();
				GUIStyle explanationStyle = new GUIStyle (GUI.skin.box);
				explanationStyle.normal.textColor = new Color (0.52f, 0.66f, 0.9f);
				GUILayout.Box (reshapeExplanation [(int)ths.reshapeMode], explanationStyle, GUILayout.ExpandWidth (true));
				EditorGUILayout.EndHorizontal ();
			}

			EditorGUILayout.BeginHorizontal ();
			if (GUILayout.Button("Load Grid")) {
				//				string k = ths.GetMapData();
			}
			if (GUILayout.Button("Save Grid")) {
//				string k = ths.GetMapData();
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical ();
			EditorGUILayout.Separator();
			EditorGUILayout.BeginVertical (blackStyle);
*/

			DrawTitleLabel("Grid Positioning");

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Hide Objects", GUILayout.Width(120));
			if (tgs.terrain!=null && GUILayout.Button("Toggle Terrain")) { tgs.terrain.enabled = !tgs.terrain.enabled; }
			if (GUILayout.Button("Toggle Grid")) { tgs.gameObject.SetActive(!tgs.gameObject.activeSelf); }
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Depth Offset", GUILayout.Width(120));
			tgs.gridDepthOffset = EditorGUILayout.IntSlider (tgs.gridDepthOffset, -10, -1);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Elevation", GUILayout.Width(120));
			tgs.gridElevation = EditorGUILayout.Slider (tgs.gridElevation, 0f, 5f);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Camera Offset", GUILayout.Width(120));
			tgs.gridCameraOffset = EditorGUILayout.Slider (tgs.gridCameraOffset, 0, 0.1f);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Normal Offset", GUILayout.Width(120));
			tgs.gridNormalOffset = EditorGUILayout.Slider (tgs.gridNormalOffset, 0.00f, 5f);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical ();
			EditorGUILayout.Separator();
			EditorGUILayout.BeginVertical (blackStyle);

			DrawTitleLabel("Grid Appearance");

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Show Territories", GUILayout.Width(120));
			tgs.showTerritories = EditorGUILayout.Toggle (tgs.showTerritories);
			if (tgs.showTerritories) {
				GUILayout.Label ("Frontier Color", GUILayout.Width (120));
				tgs.territoryFrontiersColor = EditorGUILayout.ColorField (tgs.territoryFrontiersColor, GUILayout.Width (50));
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal ();
			GUILayout.Label ("  Highlight Color", GUILayout.Width (120));
			tgs.territoryHighlightColor = EditorGUILayout.ColorField (tgs.territoryHighlightColor, GUILayout.Width (50));
			EditorGUILayout.EndHorizontal ();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("  Colorize Territories", GUILayout.Width(120));
			tgs.colorizeTerritories = EditorGUILayout.Toggle (tgs.colorizeTerritories);
			GUILayout.Label("Alpha");
			tgs.colorizedTerritoriesAlpha = EditorGUILayout.Slider (tgs.colorizedTerritoriesAlpha, 0.0f, 1.0f);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Show Cells", GUILayout.Width(120));
			tgs.showCells = EditorGUILayout.Toggle (tgs.showCells);
			if (tgs.showCells) {
				GUILayout.Label ("Border Color", GUILayout.Width (120));
				tgs.cellBorderColor = EditorGUILayout.ColorField (tgs.cellBorderColor, GUILayout.Width (50));
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label ("  Highlight Color", GUILayout.Width (120));
			tgs.cellHighlightColor = EditorGUILayout.ColorField (tgs.cellHighlightColor, GUILayout.Width (50));
			EditorGUILayout.EndHorizontal ();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label("Highlight Fade", GUILayout.Width(120));
			tgs.highlightFadeAmount = EditorGUILayout.Slider (tgs.highlightFadeAmount, 0.0f, 1.0f);
			EditorGUILayout.EndHorizontal();

//			EditorGUILayout.BeginHorizontal();
//			GUILayout.Label("LOD Multiplier", GUILayout.Width(120));
//			tgs.gridLOD = EditorGUILayout.Slider (tgs.gridLOD, 0.0f, 1.0f);
//			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical ();
			EditorGUILayout.Separator();
			EditorGUILayout.BeginVertical (blackStyle);
				
			DrawTitleLabel("Grid Behaviour");

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label ("Terrain", GUILayout.Width (120));
			Terrain prevTerrain = tgs.terrain;
			tgs.terrain =  (Terrain)EditorGUILayout.ObjectField(tgs.terrain, typeof(Terrain), true);
			if (tgs.terrain!=prevTerrain) {
				GUIUtility.ExitGUI();
			}
			EditorGUILayout.EndHorizontal ();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label ("Selection Mode", GUILayout.Width (120));
			tgs.highlightMode = (HIGHLIGHT_MODE) EditorGUILayout.Popup((int)tgs.highlightMode, selectionModeOptions);
			EditorGUILayout.EndHorizontal ();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Label ("Overlay Mode", GUILayout.Width (120));
			tgs.overlayMode = (OVERLAY_MODE) EditorGUILayout.Popup((int)tgs.overlayMode, overlayModeOptions);
			EditorGUILayout.EndHorizontal ();


			EditorGUILayout.EndVertical ();
 
			if (tgs.isDirty) {
				EditorUtility.SetDirty (target);

				// Hide mesh in Editor
				HideEditorMesh();

			}
		}

		void HideEditorMesh() {
			Renderer[] rr = tgs.GetComponentsInChildren<Renderer> (true);
			for (int k=0; k<rr.Length; k++) {
				EditorUtility.SetSelectedWireframeHidden (rr [k], true);
			}
		}
			
			Texture2D MakeTex (int width, int height, Color col) {
			Color[] pix = new Color[width * height];
			
			for (int i = 0; i < pix.Length; i++)
				pix [i] = col;
			
			Texture2D result = new Texture2D (width, height);
			result.SetPixels (pix);
			result.Apply ();
			
			return result;
		}

		void DrawTitleLabel (string s) {
			GUIStyle titleLabelStyle = new GUIStyle (GUI.skin.label);
			titleLabelStyle.normal.textColor = new Color (0.52f, 0.66f, 0.9f);
			GUILayout.Label (s, titleLabelStyle);
		}

		void DrawInfoLabel (string s) {
			GUIStyle infoLabelStyle = new GUIStyle (GUI.skin.label);
			infoLabelStyle.normal.textColor = new Color (0.76f, 0.52f, 0.52f);
			GUILayout.Label (s, infoLabelStyle);
		}

	}

}