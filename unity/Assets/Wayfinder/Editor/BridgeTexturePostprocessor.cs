using UnityEditor;

namespace Wayfinder.Unity.EditorTools
{
    /// Import settings for the generated bridge trim sheet (#22 spec §1):
    /// albedo sRGB, normal as a NormalMap (linear + correct unpack), mask
    /// linear data. ASTC 6x6, mipmaps, on device.
    public sealed class BridgeTexturePostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Wayfinder/Textures/Bridge/"))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.mipmapEnabled = true;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp; // trim bands: no wrap across V
            importer.anisoLevel = 2;
            importer.maxTextureSize = 2048;

            if (assetPath.EndsWith("Hull_Normal.png"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
            }
            else if (assetPath.EndsWith("Hull_Mask.png"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false; // R metallic / G AO / B emissive / A smoothness — data
            }
            else // Hull_Albedo.png
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
            }

            var astc = new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                format = TextureImporterFormat.ASTC_6x6,
                maxTextureSize = 2048,
            };
            importer.SetPlatformTextureSettings(astc);
        }
    }
}
