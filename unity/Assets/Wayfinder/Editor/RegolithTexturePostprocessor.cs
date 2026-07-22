using UnityEditor;

namespace Wayfinder.Unity.EditorTools
{
    /// Import settings for the generated regolith detail textures (spec:
    /// docs/research/2026-07-22-ultrareal-specs.md §3). Without this the
    /// normal imports as a Default sRGB texture — hardware sRGB decode then
    /// tilts every texel and the unpack path is wrong on device.
    public sealed class RegolithTexturePostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Wayfinder/Terrain/Regolith/"))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            importer.anisoLevel = 1;

            if (assetPath.EndsWith("_normal.png"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
            }
            else if (assetPath.EndsWith("macro_noise.png"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false; // pure data
            }
            else // *_albedo.png
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true; // generator sRGB-encodes, linear mean 0.5
            }
        }
    }
}
