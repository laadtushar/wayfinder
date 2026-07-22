using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wayfinder.Unity.EditorTools
{
    /// Imports a site's 16-bit big-endian RAW heightmap (produced by the
    /// dem-to-terrain pipeline) into a TerrainData asset at true metric scale,
    /// wires it into the site scene and the WorldPackage. Repeatable: re-run
    /// after regenerating the RAW. Values come from the site's meta.json —
    /// never hand-typed (CLAUDE.md).
    public static class TerrainImporter
    {
        [MenuItem("Wayfinder/Import Terrain/mars-olympus")]
        public static void ImportMarsOlympus() => Import("mars-olympus");

        [MenuItem("Wayfinder/Import Terrain/mars-valles")]
        public static void ImportMarsValles() => Import("mars-valles");

        [MenuItem("Wayfinder/Import Terrain/moon-shackleton")]
        public static void ImportMoonShackleton() => Import("moon-shackleton");

        public static void Import(string siteId)
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string siteDir = Path.Combine(repoRoot, "assets", "terrain", siteId);
            string metaJson = File.ReadAllText(Path.Combine(siteDir, "meta.json"));

            int resolution = IntField(metaJson, "heightmapResolution");
            float width = FloatField(metaJson, "widthMeters");
            float length = FloatField(metaJson, "lengthMeters");
            float heightRange = FloatField(metaJson, "heightRangeMeters");
            string byteOrder = StringField(metaJson, "byteOrder");
            if (byteOrder != "big-endian")
                throw new System.InvalidOperationException(
                    $"{siteId} meta.json byteOrder is '{byteOrder}' — this importer reads big-endian per the pipeline contract.");

            string rawPath = Path.Combine(siteDir, "heightmap_" + resolution + ".raw");
            byte[] bytes = File.ReadAllBytes(rawPath);
            long expected = (long)resolution * resolution * 2;
            if (bytes.Length != expected)
                throw new System.InvalidOperationException(
                    $"{rawPath}: {bytes.Length} bytes, expected {expected}.");

            // GDAL writes north-up, first row = north edge. Unity's SetHeights
            // is [row, col] with row 0 at the terrain's -Z (south) edge — flip
            // rows or the site is mirrored north-south.
            var heights = new float[resolution, resolution];
            for (int row = 0; row < resolution; row++)
            {
                int srcRow = resolution - 1 - row;
                long b = (long)srcRow * resolution * 2;
                for (int col = 0; col < resolution; col++)
                {
                    int hi = bytes[b + col * 2];
                    int lo = bytes[b + col * 2 + 1];
                    heights[row, col] = ((hi << 8) | lo) / 65535f;
                }
            }

            Color baseColor = ColorField(metaJson, "baseColor");

            if (!AssetDatabase.IsValidFolder("Assets/Wayfinder/Terrain"))
                AssetDatabase.CreateFolder("Assets/Wayfinder", "Terrain");
            string assetPath = "Assets/Wayfinder/Terrain/" + siteId + ".asset";
            // Overwrite in place so the GUID survives re-imports — referencers
            // (scene, package, future POI tooling) must never silently break.
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);
            bool isNew = data == null;
            if (isNew) data = new TerrainData();
            data.heightmapResolution = resolution;
            data.size = new Vector3(width, heightRange, length);
            data.SetHeights(0, 0, heights);
            data.terrainLayers = new[] { GetOrCreateBaseLayer(siteId, baseColor, width, length) };
            if (isNew) AssetDatabase.CreateAsset(data, assetPath);
            else EditorUtility.SetDirty(data);

            WireIntoScene(siteId, data, width, length, baseColor);
            WireIntoPackage(siteId, data);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TerrainImporter] {siteId}: {resolution}x{resolution}, {width}x{heightRange}x{length} m imported.");
        }

        static void WireIntoScene(string siteId, TerrainData data, float width, float length, Color baseColor)
        {
            var savedSetup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/Site_" + siteId + ".unity", UnityEditor.SceneManagement.OpenSceneMode.Single);

            foreach (var rootName in new[] { "SiteGround", "Landmark", "SiteTerrain" })
            {
                var old = GameObject.Find(rootName);
                if (old != null) Object.DestroyImmediate(old);
            }

            var terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "SiteTerrain";
            terrainGo.isStatic = true;
            var terrain = terrainGo.GetComponent<Terrain>();
            // Mobile terrain settings — the fps gate ticket tunes further.
            terrain.heightmapPixelError = 8f;
            // Single solid-color layer: the splat path is as cheap as the
            // basemap, and the auto-baked basemap blows out white — cover the
            // whole 20 km clip. Revisit when the imagery drape lands.
            terrain.basemapDistance = 20000f;
            terrain.drawInstanced = true;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // CreateTerrainGameObject assigns the Built-in default terrain
            // material, which URP renders as fallback checker. Give each site
            // a URP Terrain/Lit material tinted from its meta.json baseColor
            // (real imagery drape replaces this in a later ticket).
            string matPath = "Assets/Wayfinder/Materials/Terrain_" + siteId + ".mat";
            var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (terrainMat == null)
            {
                terrainMat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
                AssetDatabase.CreateAsset(terrainMat, matPath);
            }
            // Real imagery carries its own color — a tinted material would
            // double-apply it. Tint only the solid-color fallback.
            bool hasAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Wayfinder/Terrain/" + siteId + "_albedo.png") != null;
            terrainMat.color = hasAlbedo ? Color.white : baseColor;
            EditorUtility.SetDirty(terrainMat);
            terrain.materialTemplate = terrainMat;

            // The whole site surface is walkable: teleport ray interactors
            // (hands: point + pinch) target this area (build-plan Task 2.4).
            var teleArea = terrainGo.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>();
            if (teleArea == null)
                teleArea = terrainGo.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>();
            teleArea.matchOrientation = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.MatchOrientation.WorldSpaceUp;
            // Bit 31 = XRI's "Teleport" interaction layer — the rig's Teleport
            // Interactors cast only on this layer, and keeping the giant
            // terrain collider OFF Default stops it swallowing every
            // near-far interaction on the site.
            teleArea.interactionLayers = new UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask { value = 1 << 31 };

            // Center the clip on the origin, then drop it so the surface under
            // the player's spawn point sits at y=0.
            terrainGo.transform.position = new Vector3(-width / 2f, 0f, -length / 2f);
            float centerSurface = terrain.SampleHeight(Vector3.zero);
            terrainGo.transform.position += new Vector3(0f, -centerSurface, 0f);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            if (savedSetup != null && savedSetup.Length > 0)
                UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(savedSetup);
        }

        /// A terrain with no layers renders URP's checkerboard fallback.
        /// Preferred diffuse: the site's REAL orbital imagery
        /// (<site>_albedo.png, clipped to the exact DEM window — see
        /// data-sources.md), tiled exactly once across the full extent.
        /// Fallback when no imagery exists yet: a solid-color layer.
        static TerrainLayer GetOrCreateBaseLayer(string siteId, Color color, float width, float length)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Wayfinder/Terrain"))
                AssetDatabase.CreateFolder("Assets/Wayfinder", "Terrain");
            string layerPath = "Assets/Wayfinder/Terrain/" + siteId + "_base.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }

            string albedoPath = "Assets/Wayfinder/Terrain/" + siteId + "_albedo.png";
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            if (albedo != null)
            {
                // GDAL wrote north-up rows; Unity terrain UVs run south->north.
                // The importer flips heights; the texture must flip too — done
                // via the TextureImporter (flipGreenChannel is normal-map only,
                // so bake the vertical flip at import): simplest correct move
                // is sampling flip via tileOffset — Unity has none for V-flip,
                // so the png is imported as-is and flipped on disk by the
                // imagery pipeline instead (clip_imagery writes north-up; the
                // pipeline's Unity delivery step flips vertically).
                layer.diffuseTexture = albedo;
                layer.tileSize = new Vector2(width, length);
                layer.tileOffset = Vector2.zero;
            }
            else
            {
                string texPath = "Assets/Wayfinder/Terrain/" + siteId + "_base_tex.asset";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null)
                {
                    tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var pixels = new Color[16];
                    for (int i = 0; i < 16; i++) pixels[i] = color;
                    tex.SetPixels(pixels);
                    tex.Apply();
                    AssetDatabase.CreateAsset(tex, texPath);
                }
                layer.diffuseTexture = tex;
                layer.tileSize = new Vector2(1000f, 1000f);
            }
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static void WireIntoPackage(string siteId, TerrainData data)
        {
            var pkg = AssetDatabase.LoadAssetAtPath<WorldPackage>("Assets/Wayfinder/Sites/" + siteId + ".asset");
            if (pkg == null)
                throw new System.InvalidOperationException("no WorldPackage for " + siteId);
            var so = new SerializedObject(pkg);
            var prop = so.FindProperty("terrain");
            if (prop == null)
                throw new System.InvalidOperationException(
                    "WorldPackage has no serialized 'terrain' field — renamed without updating the importer (and [FormerlySerializedAs])?");
            prop.objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static int IntField(string json, string field) => (int)FloatField(json, field);

        static float FloatField(string json, string field)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"" + field + "\"\\s*:\\s*([0-9.]+)");
            if (!m.Success) throw new System.InvalidOperationException("meta.json missing " + field);
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        static string StringField(string json, string field)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"" + field + "\"\\s*:\\s*\"([^\"]*)\"");
            if (!m.Success) throw new System.InvalidOperationException("meta.json missing " + field);
            return m.Groups[1].Value;
        }

        static Color ColorField(string json, string field)
        {
            string hex = StringField(json, field);
            if (!ColorUtility.TryParseHtmlString(hex, out var color))
                throw new System.InvalidOperationException($"meta.json {field} '{hex}' is not a parseable color.");
            return color;
        }
    }
}
