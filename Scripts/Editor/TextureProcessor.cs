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

        readonly static string[] _packingTypeNames =
            {
                "YCoCg (Low quality)",
                "YCbCr (Good quality)",
            };

        readonly Texture2D[] _sources = new Texture2D[3];

        PackedColorType _colorType = PackedColorType.YCoCg;

        void OnEnable () {
            titleContent.text = Title;
        }

        void OnGUI () {
            _colorType = (PackedColorType) EditorGUILayout.Popup ("Packing type", (int) _colorType, _packingTypeNames);

            _sources[0] = EditorGUILayout.ObjectField ("Texture (R)", _sources[0], typeof (Texture2D), false) as Texture2D;
            _sources[1] = EditorGUILayout.ObjectField ("Texture (G)", _sources[1], typeof (Texture2D), false) as Texture2D;
            _sources[2] = EditorGUILayout.ObjectField ("Texture (B)", _sources[2], typeof (Texture2D), false) as Texture2D;

            if (GUILayout.Button ("Process")) {
                var res = Process (_sources, _colorType);
                EditorUtility.DisplayDialog (Title, res ?? "Success", "Close");
            }
        }

        public static string Process (Texture2D[] sources, PackedColorType colorType) {
            return Process (null, sources, colorType);
        }

        public static string Process (string path, Texture2D[] sources, PackedColorType colorType) {
            var width = -1;
            var height = -1;
            TextureImporter importer;
            string gsName = null;
            foreach (var source in sources) {
                if (source != null) {
                    importer = AssetImporter.GetAtPath (AssetDatabase.GetAssetPath (source)) as TextureImporter;
                    if (importer == null || !importer.isReadable) {
                        return "Source texture " + source.name + " should be readable";
                    }
                    if (width == -1) {
                        width = source.width;
                        height = source.height;
                    }
                    if (source.width != width || source.height != height) {
                        return "All textures should be same size";
                    }
                    if (gsName != null) {
                        gsName += "_";
                    }
                    gsName += source.name;
                }
            }

            if (width == -1) {
                return "One or more textures should be defined";
            }

            if (path == null) {
                path = EditorUtility.SaveFolderPanel (SelectFolderTitle, string.Empty, string.Empty);
            }
            if (string.IsNullOrEmpty (path)) {
                return "Target path not defined";
            }

            // make folder path relative
            if (path.IndexOf (Application.dataPath) == 0) {
                path = "Assets" + path.Substring (Application.dataPath.Length);
            }

            var gsData = new Color[width * height];
            var tex = new Texture2D (width, height, TextureFormat.RGB24, false);
            string fileName = null;
            Color[] colData;
            Color c;
            float gs;
            for (var channel = 0; channel < 3; channel++) {
                if (sources[channel] != null) {
                    colData = sources[channel].GetPixels ();
                    var invDataLength = 1 / (float) colData.Length;
                    for (int i = 0, iMax = colData.Length; i < iMax; i++) {
                        if (i % 1000 == 0) {
                            EditorUtility.DisplayProgressBar (Title, string.Format ("Processing {0}...", sources[channel].name), i * invDataLength);
                        }
                        c = colData[i];
                        if (colorType == PackedColorType.YCoCg) {
                            gs = 0.25f * c.r + 0.5f * c.g + 0.25f * c.b;
                            colData[i] = new Color (0.5f * c.r - 0.5f * c.b + 0.5f, -0.25f * c.r + 0.5f * c.g - 0.25f * c.b + 0.5f, 0f);
                            colData[i].b = 1.0f - colData[i].r;
                        } else {
                            gs = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                            colData[i] = new Color (-.168736f * c.r - .331264f * c.g + 0.5f * c.b + 0.5f, 0.5f * c.r - 0.418688f * c.g - 0.081312f * c.b + 0.5f, 0f);
                            colData[i].b = -0.2989548f * c.r + 0.412898048f * c.g - 0.113943232f * c.b + 0.5f; // Without it dx9 will add 2 alu wtf??
                        }
                        gsData[i][channel] = gs;
                    }

                    EditorUtility.ClearProgressBar ();

                    try {
                        fileName = AssetDatabase.GenerateUniqueAssetPath (Path.Combine (path, string.Format ("{0}.col.png", sources[channel].name)));
                        tex.SetPixels (colData);
                        File.WriteAllBytes (fileName, tex.EncodeToPNG ());
                    } catch (Exception ex) {
                        DestroyImmediate (tex);
                        AssetDatabase.Refresh ();
                        return ex.Message;
                    }
                }
            }

            try {
                fileName = AssetDatabase.GenerateUniqueAssetPath (Path.Combine (path, string.Format ("{0}.gs.png", gsName)));
                tex.SetPixels (gsData);
                File.WriteAllBytes (fileName, tex.EncodeToPNG ());
                DestroyImmediate (tex);
            } catch (Exception ex) {
                DestroyImmediate (tex);
                AssetDatabase.Refresh ();
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

    public enum PackedColorType {
        YCoCg = 0,
        YCbCr
    }
}