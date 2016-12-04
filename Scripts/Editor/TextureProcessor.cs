//-------------------------------------------------------
// Copyright (c) Leopotam <leopotam@gmail.com>
// Copyright (c) Belfegnar <belfegnarinc@gmail.com>
// License: CC BY-NC-SA 4.0
//-------------------------------------------------------

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LeopotamGroup.PackedColor.UnityEditors {
    sealed class TextureProcessor : EditorWindow {
        const string Title = "Texture processor";

        const string SelectFolderTitle = "Select target folder to save";

        const string GenerateLookupTitle = "Lookup texture generator";

        const string SelectLookupFileTitle = "Select lookup filename to save";

		Texture2D[] _sources = new Texture2D[3];

		PackedColorType _colorType = PackedColorType.YCoCg;

        void OnEnable () {
            titleContent.text = Title;
        }

        void OnGUI () {

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField ("Textures for R, G and B channels");
			_colorType = (PackedColorType)EditorGUILayout.EnumPopup (_colorType);
			EditorGUILayout.EndHorizontal ();

			_sources[0] = EditorGUILayout.ObjectField (_sources[0], typeof (Texture2D), false) as Texture2D;
			_sources[1] = EditorGUILayout.ObjectField (_sources[1], typeof (Texture2D), false) as Texture2D;
			_sources[2] = EditorGUILayout.ObjectField (_sources[2], typeof (Texture2D), false) as Texture2D;

            GUI.enabled = _sources[0] != null;
            if (GUILayout.Button ("Process")) {
				var res = Process (_sources, _colorType);
                EditorUtility.DisplayDialog (Title, res ?? "Success", "Close");
            }
            GUI.enabled = true;
        }

		public static string Process (Texture2D[] sources, PackedColorType colorType) {
			return Process (null, sources, colorType);
        }

		public static string Process (string path, Texture2D[] sources, PackedColorType colorType) {
            if (path == null) {
                path = EditorUtility.SaveFolderPanel (SelectFolderTitle, string.Empty, string.Empty);
            }
            if (string.IsNullOrEmpty (path)) {
                return "Target path no defined";
            }
            if (sources[0] == null) {
                return "Source texture not defined";
            }

            var importer = AssetImporter.GetAtPath (AssetDatabase.GetAssetPath (sources[0])) as TextureImporter;
            if (importer == null || !importer.isReadable) {
                return "Source texture should be readable";
            }

			foreach (Texture2D source in sources) {
				if (source != sources [0] && source != null) {
					importer = AssetImporter.GetAtPath (AssetDatabase.GetAssetPath (source)) as TextureImporter;
					if (importer == null || !importer.isReadable) {
						return "Source texture " + source.name + " should be readable";
					}
					if (source.width != sources [0].width || source.height != sources [0].height) {
						return "All textures should be same size";
					}
				}
			}

            // make folder path relative
            if (path.IndexOf (Application.dataPath) == 0) {
                path = "Assets" + path.Substring (Application.dataPath.Length);
            }

			Color[] gsData = new Color[sources [0].width * sources [0].height];
			var tex = new Texture2D (sources[0].width, sources[0].height, TextureFormat.RGB24, false);
			string fileName = null;
			for(int s = 0; s < 3; s++)
			{
				if(sources[s] == null) continue;

				var colData = sources[s].GetPixels ();

				Color c;
				float gs;
				var invDataLength = 1 / (float) colData.Length;
				for (int i = 0, iMax = colData.Length; i < iMax; i++) {
					if (i % 1000 == 0) {
						EditorUtility.DisplayProgressBar (Title, "Processing...", i * invDataLength);
					}
					c = colData [i];
					if (colorType == PackedColorType.YCoCg) {
						gs = 0.25f * c.r + 0.5f * c.g + 0.25f * c.b;
						colData [i] = new Color (0.5f * c.r - 0.5f * c.b + 0.5f, -0.25f * c.r + 0.5f * c.g - 0.25f * c.b + 0.5f, 0f);
						colData [i].b = 1.0f - colData [i].r;
					} else {
						gs = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
						colData [i] = new Color (-.168736f * c.r - .331264f * c.g + 0.5f * c.b + 0.5f, 0.5f * c.r - 0.418688f * c.g - 0.081312f * c.b + 0.5f, 0f);
						colData [i].b = -.2989548f * c.r + 0.412898048f * c.g - 0.113943232f * c.b + .5f;// Without it dx9 will add 2 alu wtf??
					}
					gsData [i][s] = gs;
				}

				EditorUtility.ClearProgressBar ();

				try {
					fileName = AssetDatabase.GenerateUniqueAssetPath (Path.Combine (path, string.Format ("{0}.col.png", sources[s].name)));
					tex.SetPixels (colData);
					File.WriteAllBytes (fileName, tex.EncodeToPNG ());
				} catch (Exception ex) {
					DestroyImmediate (tex);
					return ex.Message;
				}
			}

			fileName = AssetDatabase.GenerateUniqueAssetPath (Path.Combine (path, string.Format ("{0}.gs.png", sources[0].name)));
			tex.SetPixels (gsData);
			File.WriteAllBytes (fileName, tex.EncodeToPNG ());
			DestroyImmediate (tex);

            AssetDatabase.Refresh ();
            return null;
        }

        [MenuItem ("LeopotamGroup/PackedColor/Process color texture...")]
        public static void ShowColorProcessDialog () {
            GetWindow<TextureProcessor> ();
        }
    }

	public enum PackedColorType
	{
		YCoCg,
		YCbCr
	}
}