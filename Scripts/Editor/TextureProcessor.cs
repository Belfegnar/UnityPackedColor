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

        Texture2D _source;

        void OnEnable () {
            titleContent.text = Title;
        }

        void OnGUI () {
            _source = EditorGUILayout.ObjectField ("Source texture", _source, typeof (Texture2D), false) as Texture2D;

            GUI.enabled = _source != null;
            if (GUILayout.Button ("Process")) {
                var res = Process (_source);
                EditorUtility.DisplayDialog (Title, res ?? "Success", "Close");
            }
            GUI.enabled = true;
        }

        public static string Process (Texture2D source) {
            return Process (null, source);
        }

        public static string Process (string path, Texture2D source) {
            if (path == null) {
                path = EditorUtility.SaveFolderPanel (SelectFolderTitle, string.Empty, string.Empty);
            }
            if (string.IsNullOrEmpty (path)) {
                return "Target path no defined";
            }
            if (source == null) {
                return "Source texture not defined";
            }
            var importer = AssetImporter.GetAtPath (AssetDatabase.GetAssetPath (source)) as TextureImporter;
            if (importer == null || !importer.isReadable) {
                return "Source texture should be readable";
            }

            // make folder path relative
            if (path.IndexOf (Application.dataPath) == 0) {
                path = "Assets" + path.Substring (Application.dataPath.Length);
            }

            var gsData = source.GetPixels ();
            var colData = new Color[gsData.Length];
            Color c;
            float gs;
            var invDataLength = 1 / (float) gsData.Length;
            for (int i = 0, iMax = gsData.Length; i < iMax; i++) {
                if (i % 1000 == 0) {
                    EditorUtility.DisplayProgressBar (Title, "Processing...", i * invDataLength);
                }
                c = gsData [i];

                colData [i] = new Color (0.5f * c.r - 0.5f * c.b + 0.5f, -0.25f * c.r + 0.5f * c.g - 0.25f * c.b + 0.5f, 0f);

                gs = 0.25f * c.r + 0.5f * c.g + 0.25f * c.b;
                gsData [i] = new Color (gs, gs, gs);
            }

            EditorUtility.ClearProgressBar ();

            var tex = new Texture2D (source.width, source.height, TextureFormat.RGB24, false);
            try {
                var fileName = AssetDatabase.GenerateUniqueAssetPath (Path.Combine (path, string.Format ("{0}.col.png", source.name)));
                tex.SetPixels (colData);
                File.WriteAllBytes (fileName, tex.EncodeToPNG ());
                fileName = AssetDatabase.GenerateUniqueAssetPath (Path.Combine (path, string.Format ("{0}.gs.png", source.name)));
                tex.SetPixels (gsData);
                File.WriteAllBytes (fileName, tex.EncodeToPNG ());
                DestroyImmediate (tex);
            } catch (Exception ex) {
                DestroyImmediate (tex);
                return ex.Message;
            }

            AssetDatabase.Refresh ();
            return null;
        }

        [MenuItem ("LeopotamGroup/PackedColor/Process color texture...")]
        public static void ShowColorProcessDialog () {
            GetWindow<TextureProcessor> ();
        }
    }
}