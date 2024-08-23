using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace C2R.Export
{
    public static class TextureExporter
    {
        public static string Export(string targetFolder, Cubemap cubemap)
        {
            if (cubemap == null)
            {
                Debug.LogError("Cubemap is null.");
                return null;
            }
            
#if UNITY_EDITOR
            var filepath = AssetDatabase.GetAssetPath(cubemap);
            if (!string.IsNullOrEmpty(filepath))
            {
                var fileName = Path.GetFileName(filepath);
                File.Copy(filepath, Path.Combine(targetFolder, fileName));
                return fileName;
            }
#endif
            
            int width = Mathf.RoundToInt(cubemap.width * 2.625f);
            int height = Mathf.RoundToInt(cubemap.height * 1.3125f);

            Texture2D equirectangularTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, false);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = 1.0f - (float)x / (width - 1);
                    float v = 1.0f - (float)y / (height - 1);

                    Vector3 direction = EquirectangularToDirection(u, v);
                    Color pixelColor = SampleCubemap(cubemap, direction);

                    equirectangularTexture.SetPixel(x, y, pixelColor);
                }
            }

            equirectangularTexture.Apply();

            byte[] bytes = equirectangularTexture.EncodeToEXR();
            
            var ext = ".exr";
            var resultFile = PathHelper.ToSafeFilename(targetFolder, cubemap.name, ext);
            File.WriteAllBytes(resultFile, bytes);

            Debug.Log("Cubemap exported to " + resultFile);
            return Path.GetFileName(resultFile);
        }

        private static Vector3 EquirectangularToDirection(float u, float v)
        {
            float theta = u * 2.0f * Mathf.PI;
            float phi = v * Mathf.PI;

            float x = Mathf.Sin(phi) * Mathf.Cos(theta);
            float y = Mathf.Cos(phi);
            float z = Mathf.Sin(phi) * Mathf.Sin(theta);

            return new Vector3(x, y, z);
        }

        private static Color SampleCubemap(Cubemap cubemap, Vector3 direction)
        {
            float absX = Mathf.Abs(direction.x);
            float absY = Mathf.Abs(direction.y);
            float absZ = Mathf.Abs(direction.z);

            CubemapFace face;
            float u, v;

            if (absX >= absY && absX >= absZ)
            {
                if (direction.x > 0)
                {
                    face = CubemapFace.PositiveX;
                    u = -direction.z / direction.x;
                    v = -direction.y / direction.x;
                }
                else
                {
                    face = CubemapFace.NegativeX;
                    u = direction.z / -direction.x;
                    v = -direction.y / -direction.x;
                }
            }
            else if (absY >= absX && absY >= absZ)
            {
                if (direction.y > 0)
                {
                    face = CubemapFace.PositiveY;
                    u = direction.x / direction.y;
                    v = direction.z / direction.y;
                }
                else
                {
                    face = CubemapFace.NegativeY;
                    u = direction.x / -direction.y;
                    v = -direction.z / -direction.y;
                }
            }
            else
            {
                if (direction.z > 0)
                {
                    face = CubemapFace.PositiveZ;
                    u = direction.x / direction.z;
                    v = -direction.y / direction.z;
                }
                else
                {
                    face = CubemapFace.NegativeZ;
                    u = -direction.x / -direction.z;
                    v = -direction.y / -direction.z;
                }
            }

            u = (u + 1.0f) * 0.5f;
            v = (v + 1.0f) * 0.5f;

            return cubemap.GetPixel(face, Mathf.RoundToInt(u * cubemap.width), Mathf.RoundToInt(v * cubemap.height));
        }
    }
}