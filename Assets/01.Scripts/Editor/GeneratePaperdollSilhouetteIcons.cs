using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace MySecondWorld.EditorTools
{
    public static class GeneratePaperdollSilhouetteIcons
    {
        private const int TargetSize = 256;
        private const int HiRes = 512;
        private const string OutDir = "Assets/07.Textures/UI/PaperdollIcons";

        [MenuItem("Tools/Generate Paperdoll Silhouette Icons")]
        public static void GenerateAll()
        {
            if (!Directory.Exists(OutDir))
                Directory.CreateDirectory(OutDir);

            SaveIcon("Silhouette_Pants.png", DrawPants);
            SaveIcon("Silhouette_Greaves.png", DrawGreaves);
            SaveIcon("Silhouette_ArmUpper.png", DrawArmUpper);
            SaveIcon("Silhouette_ArmLower.png", DrawArmLower);
            SaveIcon("Silhouette_Shoulder.png", DrawShoulder);
            SaveIcon("Silhouette_Elbow.png", DrawElbow);
            SaveIcon("Silhouette_Belt.png", DrawBelt);
            SaveIcon("Silhouette_HelmetAttach.png", DrawHelmetAttach);

            AssetDatabase.Refresh();

            // Import as Sprites
            string[] files = Directory.GetFiles(OutDir, "*.png");
            foreach (var f in files)
            {
                var importer = AssetImporter.GetAtPath(f) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }
            }

            Debug.Log("[GeneratePaperdollSilhouetteIcons] 8개 세부 부위 전용 실루엣 아이콘 생성 완료!");
        }

        private static void SaveIcon(string filename, Func<int, int, float> sdfFunc)
        {
            float[,] highRes = new float[HiRes, HiRes];
            for (int y = 0; y < HiRes; y++)
            {
                for (int x = 0; x < HiRes; x++)
                {
                    highRes[x, y] = Mathf.Clamp01(sdfFunc(x, y));
                }
            }

            Texture2D tex = new Texture2D(TargetSize, TargetSize, TextureFormat.RGBA32, false);
            for (int y = 0; y < TargetSize; y++)
            {
                for (int x = 0; x < TargetSize; x++)
                {
                    // 2x2 Downsample
                    float a = (highRes[x * 2, y * 2] +
                               highRes[x * 2 + 1, y * 2] +
                               highRes[x * 2, y * 2 + 1] +
                               highRes[x * 2 + 1, y * 2 + 1]) * 0.25f;

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            string fullPath = Path.Combine(OutDir, filename);
            File.WriteAllBytes(fullPath, png);
            UnityEngine.Object.DestroyImmediate(tex);
        }

        // 1. Pants (Hips)
        private static float DrawPants(int x, int y)
        {
            float cx = Mathf.Abs(x - 256);
            float cy = y;

            if (cy >= 390 && cy <= 435 && cx <= 140) return 1f;

            if (cy >= 80 && cy <= 400)
            {
                float t = (cy - 80f) / 320f;
                float outerX = Mathf.Lerp(120f, 145f, t);
                float innerX = Mathf.Lerp(35f, 0f, Mathf.Pow(t, 0.7f));
                if (cx >= innerX && cx <= outerX)
                {
                    if (cy <= 86) return 0f;
                    return 1f;
                }
            }

            if (cx <= 4 && cy >= 260 && cy <= 390) return 0f;

            return 0f;
        }

        // 2. Greaves (Legs)
        private static float DrawGreaves(int x, int y)
        {
            float cx = Mathf.Abs(x - 256);
            float cy = y;

            if (cx >= 40 && cx <= 132 && cy >= 80 && cy <= 430)
            {
                if (cy >= 370)
                {
                    float dx = (cx - 86f) / 44f;
                    float dy = (cy - 390f) / 38f;
                    if (dx * dx + dy * dy <= 1f) return 1f;
                }
                float t = (cy - 80f) / 290f;
                float w = Mathf.Lerp(32f, 44f, Mathf.Sin(t * Mathf.PI));
                if (Mathf.Abs(cx - 86f) <= w)
                {
                    if (Mathf.Abs(cy - 220) <= 3 || Mathf.Abs(cy - 300) <= 3) return 0f;
                    return 1f;
                }
            }
            return 0f;
        }

        // 3. ArmUpper (Bicep guard)
        private static float DrawArmUpper(int x, int y)
        {
            float cx = Mathf.Abs(x - 256);
            float cy = y;

            if (cx >= 55 && cx <= 135 && cy >= 140 && cy <= 370)
            {
                float t = (cy - 140f) / 230f;
                float w = Mathf.Lerp(32f, 38f, Mathf.Sin(t * Mathf.PI));
                if (Mathf.Abs(cx - 95f) <= w)
                {
                    if (Mathf.Abs(cy - 250) <= 4) return 0f;
                    return 1f;
                }
            }
            return 0f;
        }

        // 4. ArmLower (Vambrace / Forearm bracer)
        private static float DrawArmLower(int x, int y)
        {
            float cx = Mathf.Abs(x - 256);
            float cy = y;

            if (cy >= 120 && cy <= 390)
            {
                float t = (cy - 120f) / 270f;
                float w = Mathf.Lerp(26f, 45f, t);
                if (Mathf.Abs(cx - 95f) <= w)
                {
                    if (cy > 360)
                    {
                        float pt = (cy - 360f) / 30f;
                        if (Mathf.Abs(cx - 95f) > w * (1f - pt)) return 0f;
                    }
                    if (Mathf.Abs(cx - 95f) <= 2 && cy >= 150 && cy <= 350) return 0f;
                    if (Mathf.Abs(cy - 200) <= 3 || Mathf.Abs(cy - 280) <= 3) return 0f;
                    return 1f;
                }
            }
            return 0f;
        }

        // 5. Shoulder (Pauldron)
        private static float DrawShoulder(int x, int y)
        {
            float cx = Mathf.Abs(x - 256);
            float cy = y;

            if (cy >= 280 && cy <= 430)
            {
                float dx = cx / 155f;
                float dy = (cy - 330f) / 95f;
                if (dx * dx + dy * dy <= 1f) return 1f;
            }

            if (cy >= 190 && cy <= 295)
            {
                float dx = (cx - 10f) / 125f;
                float dy = (cy - 230f) / 65f;
                if (dx * dx + dy * dy <= 1f) return 1f;
            }

            if (cy >= 110 && cy <= 205)
            {
                float dx = (cx - 15f) / 95f;
                float dy = (cy - 145f) / 55f;
                if (dx * dx + dy * dy <= 1f) return 1f;
            }

            return 0f;
        }

        // 6. Elbow (Couter)
        private static float DrawElbow(int x, int y)
        {
            float dx = Mathf.Abs(x - 256) / 140f;
            float dy = Mathf.Abs(y - 256) / 130f;

            if (dx + dy <= 1f)
            {
                if (dx + dy >= 0.72f && dx + dy <= 0.82f) return 0f;
                return 1f;
            }

            float cx = Mathf.Abs(x - 256);
            float cy = y - 256;
            if (cx <= 160 && Mathf.Abs(cy) <= 45)
            {
                float t = (160f - cx) / 60f;
                if (t >= 0 && Mathf.Abs(cy) <= t * 45f) return 1f;
            }

            return 0f;
        }

        // 7. Belt (HipsAttachment)
        private static float DrawBelt(int x, int y)
        {
            float cx = Mathf.Abs(x - 256);
            float cy = y;

            if (cy >= 230 && cy <= 290 && cx <= 180)
            {
                float sag = (cx / 180f) * (cx / 180f) * 20f;
                if (cy >= 230 + sag && cy <= 285 + sag)
                {
                    return 1f;
                }
            }

            if (cx <= 50 && cy >= 220 && cy <= 305)
            {
                if (cx >= 38 || cy <= 232 || cy >= 293) return 1f;
                if (cx <= 8 && cy >= 230 && cy <= 295) return 1f;
            }

            if (x >= 280 && x <= 325 && y >= 90 && y <= 250)
            {
                float t = (250f - y) / 160f;
                float curX = 300f + t * 15f;
                if (Mathf.Abs(x - curX) <= 18f)
                {
                    if (y <= 110 && Mathf.Abs(x - curX) > (y - 90f)) return 0f;
                    return 1f;
                }
            }

            return 0f;
        }

        // 8. Helmet Attachment (Wings / Horns Crest)
        private static float DrawHelmetAttach(int x, int y)
        {
            float cx = Mathf.Abs(x - 256);
            float cy = y;

            for (float t = 0; t <= 1f; t += 0.005f)
            {
                float u = 1f - t;
                float bx = u * u * u * 35f + 3f * u * u * t * 90f + 3f * u * t * t * 210f + t * t * t * 175f;
                float by = u * u * u * 150f + 3f * u * u * t * 230f + 3f * u * t * t * 340f + t * t * t * 430f;

                float r = Mathf.Lerp(28f, 3f, t);
                float d = Vector2.Distance(new Vector2(cx, cy), new Vector2(bx, by));
                if (d <= r) return 1f;
            }

            float mdx = cx / 35f;
            float mdy = (cy - 160f) / 25f;
            if (mdx * mdx + mdy * mdy <= 1f)
            {
                if (mdx * mdx + mdy * mdy >= 0.65f && mdx * mdx + mdy * mdy <= 0.82f) return 0f;
                return 1f;
            }

            return 0f;
        }
    }
}
