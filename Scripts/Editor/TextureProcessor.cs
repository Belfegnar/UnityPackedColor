
// -------------------------------------------------------
// Copyright (c) Leopotam <leopotam@gmail.com>
// Copyright (c) Belfegnar <belfegnarinc@gmail.com>
// License: CC BY-NC-SA 4.0
// -------------------------------------------------------

using System.IO;
using System;
using UnityEditor;
using UnityEngine;

namespace LeopotamGroup.PackedColor.UnityEditors {
    sealed class TextureProcessor : EditorWindow {
        const string Title = "Texture processor";

        const string SelectFolderTitle = "Select target folder to save";

        const string GenerateLookupTitle = "Lookup texture generator";

        const string SelectLookupFileTitle = "Select lookup filename to save";

        static readonly string[] _packingTypeNames = {
            "YCoCg (Low quality)",
            "YCbCr (Good quality)",
        };

        static readonly float[][][] _ditheringCores = {
            // Floyd-Steinberg
            new float[][] {
                new[] { 0 / 16f, 0 / 16f, 7 / 16f },
                new[] { 3 / 16f, 5 / 16f, 1 / 16f },
            },

            // Jarvis-Judice-Ninke
            new float[][] {
                new[] { 0 / 48f, 0 / 48f, 0 / 48f, 7 / 48f, 5 / 48f },
                new[] { 3 / 48f, 5 / 48f, 7 / 48f, 5 / 48f, 3 / 48f },
                new[] { 1 / 48f, 3 / 48f, 5 / 48f, 3 / 48f, 1 / 48f },
            },

            // Burkes
            new float[][] {
                new[] { 0 / 32f, 0 / 32f, 0 / 32f, 8 / 32f, 4 / 32f },
                new[] { 2 / 32f, 4 / 32f, 8 / 32f, 4 / 32f, 2 / 32f },
            },

            // Sierra-3
            new float[][] {
                new[] { 0 / 32f, 0 / 32f, 0 / 32f, 5 / 32f, 3 / 32f },
                new[] { 2 / 32f, 4 / 32f, 5 / 32f, 4 / 32f, 2 / 32f },
                new[] { 0 / 32f, 2 / 32f, 3 / 32f, 2 / 32f, 0 / 32f },
            },

            // Sierra-2
            new float[][] {
                new[] { 0 / 16f, 0 / 16f, 0 / 16f, 4 / 16f, 3 / 16f },
                new[] { 1 / 16f, 2 / 16f, 3 / 16f, 2 / 16f, 1 / 16f },
            },

            // Sierra-Lite
            new float[][] {
                new[] { 0 / 4f, 0 / 4f, 2 / 4f },
                new[] { 1 / 4f, 1 / 4f, 0 / 4f },
            },
        };

        readonly Texture2D[] _sources = new Texture2D[3];

        PackedColorType _colorType = PackedColorType.YCbCr;

        DitheringType _imgDitheringType = DitheringType.None;

        DitheringType _gsDitheringType = DitheringType.FloydSteinberg;

        void OnEnable () {
            titleContent.text = Title;
        }

        void OnGUI () {
            _colorType = (PackedColorType) EditorGUILayout.Popup ("Packing type", (int) _colorType, _packingTypeNames);
            _imgDitheringType = (DitheringType) EditorGUILayout.EnumPopup ("Preprocess dithering", _imgDitheringType);
            _gsDitheringType = (DitheringType) EditorGUILayout.EnumPopup ("Grayscale dithering", _gsDitheringType);
            _sources[0] = EditorGUILayout.ObjectField ("Texture (R)", _sources[0], typeof (Texture2D), false) as Texture2D;
            _sources[1] = EditorGUILayout.ObjectField ("Texture (G)", _sources[1], typeof (Texture2D), false) as Texture2D;
            _sources[2] = EditorGUILayout.ObjectField ("Texture (B)", _sources[2], typeof (Texture2D), false) as Texture2D;

            if (GUILayout.Button ("Process")) {
                var res = Process (_sources, _colorType, _imgDitheringType, _gsDitheringType);
                EditorUtility.DisplayDialog (Title, res ?? "Success", "Close");
            }
        }

        static void ProcessDithering (Color[] colData, int width, int height, DitheringType ditheringType) {
            if (ditheringType == DitheringType.None) {
                return;
            }

            const float inv16 = 1 / 16f;

            var core = _ditheringCores[(int) ditheringType - 1];
            var coreHgt = core.Length;
            var coreWth = core[0].Length;
            var coreWthOffset = (coreWth - 1) >> 1;

            float r;
            float g;
            float b;
            int i;
            int j;
            int offset;

            for (var y = 0; y < height; y++) {
                for (var x = 0; x < width; x++) {
                    offset = y * width + x;
                    r = colData[offset].r;
                    g = colData[offset].g;
                    b = colData[offset].b;

                    // clamping to 4bit pallete colors
                    colData[offset].r = Mathf.Clamp01 (Mathf.RoundToInt (r * 16) * inv16);
                    colData[offset].g = Mathf.Clamp01 (Mathf.RoundToInt (g * 16) * inv16);
                    colData[offset].b = Mathf.Clamp01 (Mathf.RoundToInt (b * 16) * inv16);

                    // finding quantization errors
                    r -= colData[offset].r;
                    g -= colData[offset].g;
                    b -= colData[offset].b;

                    for (var coreY = 0; coreY < coreHgt; coreY++) {
                        j = y + coreY;
                        if (j < height) {
                            for (var coreX = 0; coreX < coreWth; coreX++) {
                                i = x + coreX - coreWthOffset;
                                if (i >= 0 && i < width) {
                                    offset = j * width + i;
                                    colData[offset].r += core[coreY][coreX] * r;
                                    colData[offset].g += core[coreY][coreX] * g;
                                    colData[offset].b += core[coreY][coreX] * b;
                                }
                            }
                        }
                    }
                }
            }
        }

        public static string Process (Texture2D[] sources, PackedColorType colorType,
                                      DitheringType imgDitheringType, DitheringType gsDitheringType) {
            return Process (null, sources, colorType, imgDitheringType, gsDitheringType);
        }

        public static string Process (string path, Texture2D[] sources, PackedColorType colorType,
                                      DitheringType imgDitheringType, DitheringType gsDitheringType) {
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
                        return "All textures should have same size";
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
                    ProcessDithering (colData, width, height, imgDitheringType);
                    var invDataLength = 1 / (float) colData.Length;
                    for (int i = 0, iMax = colData.Length; i < iMax; i++) {
                        if (i % 1000 == 0) {
                            EditorUtility.DisplayProgressBar (Title,
                                                              string.Format ("Processing {0}...", sources[channel].name),
                                                              i * invDataLength);
                        }
                        c = colData[i];
                        if (colorType == PackedColorType.YCoCg) {
                            gs = 0.25f * c.r + 0.5f * c.g + 0.25f * c.b;
                            colData[i] = new Color (0.5f * c.r - 0.5f * c.b + 0.5f, -0.25f * c.r + 0.5f * c.g - 0.25f * c.b + 0.5f, 0f);
                            colData[i].b = 1.0f - colData[i].r;
                        } else {
                            // gs = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                            gs = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                            colData[i] = new Color (-.168736f * c.r - .331264f * c.g + 0.5f * c.b + 0.5f,
                                                    0.5f * c.r - 0.418688f * c.g - 0.081312f * c.b + 0.5f, 0f);

                            // Without it dx9 will add 2 alu wtf??
                            colData[i].b = -0.2989548f * c.r + 0.412898048f * c.g - 0.113943232f * c.b + 0.5f;
                        }
                        gsData[i][channel] = gs;
                    }

                    EditorUtility.ClearProgressBar ();

                    try {
                        fileName = AssetDatabase.GenerateUniqueAssetPath (
                            Path.Combine (path, string.Format ("{0}.col.png", sources[channel].name)));
                        tex.SetPixels (colData);
                        File.WriteAllBytes (fileName, tex.EncodeToPNG ());
                    } catch (Exception ex) {
                        DestroyImmediate (tex);
                        AssetDatabase.Refresh ();
                        return ex.Message;
                    }
                }
            }

            ProcessDithering (gsData, width, height, gsDitheringType);

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

    public enum DitheringType {
        None = 0,
        FloydSteinberg,
        JarvisJudiceNinke,
        Burkes,
        Sierra3,
        Sierra2,
        SierraLite
    }

    public enum PackedColorType {
        YCoCg = 0,
        YCbCr
    }
}