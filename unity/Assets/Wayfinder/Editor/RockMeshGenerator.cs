using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wayfinder.Unity.EditorTools
{
    /// Procedural rock-scatter archetype meshes for #18 part 3 (spec §1A,
    /// docs/research/2026-07-22-ultrareal-specs.md). 100% generated in-editor —
    /// no marketplace, no AI-generator, no import gate. Every mesh is the SAME
    /// deterministic displacement field re-evaluated on a coarser base solid, so
    /// the three LODs share a silhouette (minimal pop) with NO decimation lib.
    ///
    /// Geology per site (spec §1A):
    ///   mars-olympus   basalt blocks    — cube base + planar chamfer cuts (columnar-jointing angularity)
    ///   mars-valles    talus shards     — icosphere flattened Y x0.55 + sharp mid-frequency noise (broken plates)
    ///   moon-shackleton ejecta boulders — icosphere + rounded low-frequency noise; two half-buried flat-base variants
    ///
    /// Per site: 6 archetypes x 3 LODs = 18 tiny meshes. Flat faceted normals
    /// (triangle-soup verts + RecalculateNormals) so the shader skips a
    /// normal-map fetch. Sky-occlusion AO baked into vertex color (crevices +
    /// undersides darker) replaces an AO texture sample.
    ///
    /// F8 (determinism): the noise is a hand-written hash-based value/fbm field
    /// seeded by hash(siteId, archetypeIndex). NOT Mathf.PerlinNoise (documented
    /// as not bit-stable across platforms/versions) and NOT System.String
    /// .GetHashCode (randomized per run). Re-runs are byte-stable → referencing
    /// ScatterFieldData never churns.
    ///
    /// LOD tri counts (actual, at or under the §4 budget ceiling of 380/130/46):
    ///   icosphere (talus/boulder):  k = 4 / 2 / 1  ->  320 / 80 / 20 tris
    ///   cube (basalt):              N = 5 / 3 / 2  ->  300 / 108 / 48 tris
    /// Under-budget is intentional headroom for the 72 fps floor; the runtime
    /// visible cap is derived from the tri ceiling, never the reverse.
    public static class RockMeshGenerator
    {
        // -------------------------------------------------------------------- //
        //  Menu                                                                //
        // -------------------------------------------------------------------- //

        [MenuItem("Wayfinder/Scatter/Generate Rock Meshes/mars-olympus")]
        public static void GenerateMarsOlympus() => Generate("mars-olympus");

        [MenuItem("Wayfinder/Scatter/Generate Rock Meshes/mars-valles")]
        public static void GenerateMarsValles() => Generate("mars-valles");

        [MenuItem("Wayfinder/Scatter/Generate Rock Meshes/moon-shackleton")]
        public static void GenerateMoonShackleton() => Generate("moon-shackleton");

        [MenuItem("Wayfinder/Scatter/Generate Rock Meshes/Generate All")]
        public static void GenerateAll()
        {
            Generate("mars-olympus");
            Generate("mars-valles");
            Generate("moon-shackleton");
        }

        // -------------------------------------------------------------------- //
        //  Config                                                              //
        // -------------------------------------------------------------------- //

        private enum Geology { BasaltBlock, TalusShard, EjectaBoulder }

        private const int ArchetypesPerSite = 6;
        private const int LodCount = 3;

        // Icosphere subdivision factor k per LOD (tris = 20 * k^2).
        private static readonly int[] IcoSubdiv = { 4, 2, 1 }; // LOD0, LOD1, LOD2
        // Cube segments-per-edge N per LOD (tris = 12 * N^2).
        private static readonly int[] CubeSeg = { 5, 3, 2 };   // LOD0, LOD1, LOD2

        private struct SiteConfig
        {
            public Geology geology;
            public float radius;      // base half-size (metres, ~1 m archetype)
            public float noiseFreq;   // displacement field frequency
            public float noiseAmp;    // displacement amplitude (fraction of radius)
            public int octaves;       // fbm octaves
            public float yScale;      // vertical flatten (talus)
            public int chamferCuts;   // basalt planar cuts
        }

        private static SiteConfig ConfigFor(string siteId)
        {
            switch (siteId)
            {
                case "mars-olympus":
                    return new SiteConfig
                    {
                        geology = Geology.BasaltBlock,
                        radius = 0.55f, noiseFreq = 3.0f, noiseAmp = 0.10f,
                        octaves = 2, yScale = 1.0f, chamferCuts = 4
                    };
                case "mars-valles":
                    return new SiteConfig
                    {
                        geology = Geology.TalusShard,
                        radius = 0.50f, noiseFreq = 5.0f, noiseAmp = 0.20f,
                        octaves = 3, yScale = 0.55f, chamferCuts = 0
                    };
                case "moon-shackleton":
                    return new SiteConfig
                    {
                        geology = Geology.EjectaBoulder,
                        radius = 0.52f, noiseFreq = 2.2f, noiseAmp = 0.11f,
                        octaves = 3, yScale = 1.0f, chamferCuts = 0
                    };
                default:
                    throw new System.ArgumentException($"Unknown scatter site '{siteId}'.");
            }
        }

        // -------------------------------------------------------------------- //
        //  Result payload — carries bounds + colliderRadius per archetype so a  //
        //  caller (the placement baker) can read them without re-deriving.      //
        // -------------------------------------------------------------------- //

        public struct ArchetypeResult
        {
            public Mesh[] lods;         // [LodCount], LOD0..LOD2
            public Bounds bounds;       // object-space bounds of LOD0
            public float colliderRadius;// horizontal footprint radius (max of X/Z extent)
        }

        // -------------------------------------------------------------------- //
        //  Generation entry                                                    //
        // -------------------------------------------------------------------- //

        public static void Generate(string siteId)
        {
            SiteConfig cfg = ConfigFor(siteId);
            string folder = "Assets/Wayfinder/Scatter/Meshes/" + siteId;
            EnsureFolder(folder);

            uint siteSeed = StableStringHash(siteId);

            int savedMeshes = 0;
            for (int a = 0; a < ArchetypesPerSite; a++)
            {
                ArchetypeResult result = BuildArchetypeInternal(cfg, siteSeed, a);

                for (int lod = 0; lod < LodCount; lod++)
                {
                    string assetPath = $"{folder}/{siteId}_a{a}_lod{lod}.asset";
                    if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
                        AssetDatabase.DeleteAsset(assetPath);
                    AssetDatabase.CreateAsset(result.lods[lod], assetPath);
                    savedMeshes++;
                }

                Debug.Log($"[RockMeshGenerator] {siteId} archetype {a}: " +
                          $"bounds size {result.bounds.size:F3}, colliderRadius {result.colliderRadius:F3} m, " +
                          $"tris {result.lods[0].triangles.Length / 3}/" +
                          $"{result.lods[1].triangles.Length / 3}/" +
                          $"{result.lods[2].triangles.Length / 3}.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RockMeshGenerator] {siteId}: wrote {savedMeshes} meshes to {folder}.");
        }

        /// Public so a placement baker can regenerate archetype data (bounds +
        /// colliderRadius + the LOD meshes) deterministically at bake time,
        /// without re-saving assets. Same seed -> byte-identical meshes (F8).
        public static ArchetypeResult BuildArchetype(string siteId, int archetypeIndex)
            => BuildArchetypeInternal(ConfigFor(siteId), StableStringHash(siteId), archetypeIndex);

        private static ArchetypeResult BuildArchetypeInternal(SiteConfig cfg, uint siteSeed, int archetypeIndex)
        {
            // hash(siteId, archetypeIndex) — the F8 per-archetype seed.
            uint seed = Hash(siteSeed + (uint)(archetypeIndex + 1) * 0x9E3779B1u);

            // Per-archetype shape variety (deterministic): a gentle ellipsoid
            // stretch and — for boulders — a half-buried flag on two of six.
            var rng = new DetRng(Hash(seed ^ 0xA5A5A5A5u));
            Vector3 stretch = new Vector3(
                rng.Range(0.82f, 1.22f),
                rng.Range(0.82f, 1.22f),
                rng.Range(0.82f, 1.22f));

            bool flatBase = cfg.geology == Geology.EjectaBoulder && archetypeIndex >= 4; // 2 of 6 half-buried

            // Chamfer planes (basalt): generated once from the seed and applied
            // identically to every LOD so silhouettes match across LODs.
            Vector4[] cutPlanes = null;
            if (cfg.geology == Geology.BasaltBlock && cfg.chamferCuts > 0)
            {
                var cutRng = new DetRng(Hash(seed ^ 0x1B873593u));
                cutPlanes = new Vector4[cfg.chamferCuts];
                for (int c = 0; c < cfg.chamferCuts; c++)
                {
                    Vector3 n = cutRng.OnUnitSphere();
                    // Offset as a fraction of the block's extent ALONG n (L1
                    // support of the cube), so each cut shaves the outer corner
                    // instead of over-carving the whole block.
                    float extent = cfg.radius * (Mathf.Abs(n.x) + Mathf.Abs(n.y) + Mathf.Abs(n.z));
                    float d = extent * cutRng.Range(0.72f, 0.90f);
                    cutPlanes[c] = new Vector4(n.x, n.y, n.z, d);
                }
            }

            var result = new ArchetypeResult { lods = new Mesh[LodCount] };
            for (int lod = 0; lod < LodCount; lod++)
            {
                Mesh m = BuildLod(cfg, seed, archetypeIndex, lod, stretch, flatBase, cutPlanes);
                result.lods[lod] = m;
            }

            result.bounds = result.lods[0].bounds;
            Vector3 ext = result.bounds.extents;
            result.colliderRadius = Mathf.Max(ext.x, ext.z);
            return result;
        }

        // -------------------------------------------------------------------- //
        //  One LOD                                                             //
        // -------------------------------------------------------------------- //

        private static Mesh BuildLod(SiteConfig cfg, uint seed, int archetypeIndex, int lod,
                                     Vector3 stretch, bool flatBase, Vector4[] cutPlanes)
        {
            // Base solid as a triangle SOUP (each triangle owns 3 unique verts).
            // Split verts up front give exact flat faceting from
            // RecalculateNormals, and shared-edge verts land at identical
            // positions -> identical displacement -> no cracks between them.
            var positions = new List<Vector3>();

            if (cfg.geology == Geology.BasaltBlock)
                BuildCubeSoup(CubeSeg[lod], cfg.radius, positions);
            else
                BuildIcosphereSoup(IcoSubdiv[lod], cfg.radius, positions);

            // Displace every vertex by the shared field, capturing the crevice
            // term for AO. Field is a pure function of position+seed, so it is
            // identical across LODs -> matching silhouettes.
            var colors = new Color[positions.Count];
            float minY = float.MaxValue, maxY = float.MinValue;

            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 p = positions[i];
                Vector3 displaced = DisplaceVertex(cfg, seed, p, stretch, out float crevice);
                positions[i] = displaced;
                if (displaced.y < minY) minY = displaced.y;
                if (displaced.y > maxY) maxY = displaced.y;
                // Stash crevice term in color.a for now; finalize AO below.
                colors[i] = new Color(0, 0, 0, crevice);
            }

            // Geometry finishing ops (same params on every LOD).
            if (cfg.geology == Geology.BasaltBlock && cutPlanes != null)
                ApplyChamfers(positions, cutPlanes);

            if (flatBase)
            {
                // Clip the bottom to a flat base (half-buried ejecta boulder):
                // push everything below the cut plane up onto it.
                float cutY = Mathf.Lerp(minY, maxY, 0.34f);
                for (int i = 0; i < positions.Count; i++)
                {
                    Vector3 p = positions[i];
                    if (p.y < cutY) { p.y = cutY; positions[i] = p; }
                }
                minY = cutY;
            }

            // Recompute Y extent after finishing ops for the AO underside term.
            minY = float.MaxValue; maxY = float.MinValue;
            for (int i = 0; i < positions.Count; i++)
            {
                float y = positions[i].y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            float yRange = Mathf.Max(1e-4f, maxY - minY);

            // Bake AO: crevices darker (from displacement) x undersides darker
            // (cheap sky occlusion). Grey into rgb; a mirrors it.
            for (int i = 0; i < positions.Count; i++)
            {
                float crevice = colors[i].a;                          // 0 = pit, 1 = ridge
                float height = (positions[i].y - minY) / yRange;      // 0 = base, 1 = top
                float ao = Mathf.Lerp(0.38f, 1.0f, crevice);          // crevice floor
                ao *= Mathf.Lerp(0.62f, 1.0f, Mathf.SmoothStep(0f, 1f, height)); // ground occlusion
                ao = Mathf.Clamp01(ao);
                colors[i] = new Color(ao, ao, ao, ao);
            }

            var mesh = new Mesh { name = "Rock_" + archetypeIndex + "_LOD" + lod };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16; // < 65k verts always

            int[] tris = new int[positions.Count];
            for (int i = 0; i < tris.Length; i++) tris[i] = i;         // soup: 1:1 vert<->index

            mesh.SetVertices(positions);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();  // soup has no shared indices -> flat faceted normals
            mesh.RecalculateBounds();
            mesh.Optimize();            // GPU vertex-cache order; deterministic, no welding
            mesh.UploadMeshData(false); // keep readable for editor tooling
            return mesh;
        }

        // -------------------------------------------------------------------- //
        //  Displacement field (shared across LODs)                             //
        // -------------------------------------------------------------------- //

        private static Vector3 DisplaceVertex(SiteConfig cfg, uint seed, Vector3 basePos,
                                              Vector3 stretch, out float crevice)
        {
            // Ellipsoid variety first (part of the shared field, so all LODs
            // agree).
            Vector3 p = Vector3.Scale(basePos, stretch);

            // Direction to push along: radial for round solids; for the cube we
            // still push radially so displacement reads as surface relief.
            Vector3 dir = p.sqrMagnitude > 1e-8f ? p.normalized : Vector3.up;

            // fbm sample in [-1,1]. Sample on the direction (stable across LODs).
            float n = Fbm(dir * cfg.noiseFreq, seed, cfg.octaves);

            float disp;
            if (cfg.geology == Geology.TalusShard)
            {
                // Sharpen into broken angular plates: ridged transform.
                float ridged = 1.0f - Mathf.Abs(n);        // [0,1], creases at n=0
                disp = (ridged - 0.5f) * 2.0f * cfg.noiseAmp * cfg.radius;
            }
            else
            {
                disp = n * cfg.noiseAmp * cfg.radius;       // rounded / blocky
            }

            Vector3 outPos = p + dir * disp;

            // Vertical flatten (talus) applied after displacement.
            if (cfg.yScale != 1.0f)
                outPos.y *= cfg.yScale;

            // crevice term for AO: map disp (inward = pit) to [0,1].
            crevice = Mathf.Clamp01(0.5f + 0.5f * (disp / Mathf.Max(1e-4f, cfg.noiseAmp * cfg.radius)));
            return outPos;
        }

        // Slice corners flat with planar chamfer cuts (basalt columnar feel).
        private static void ApplyChamfers(List<Vector3> positions, Vector4[] planes)
        {
            for (int c = 0; c < planes.Length; c++)
            {
                Vector3 n = new Vector3(planes[c].x, planes[c].y, planes[c].z);
                float d = planes[c].w;
                for (int i = 0; i < positions.Count; i++)
                {
                    Vector3 p = positions[i];
                    float sd = Vector3.Dot(p, n) - d;
                    if (sd > 0f) positions[i] = p - sd * n; // project outside verts onto the plane
                }
            }
        }

        // -------------------------------------------------------------------- //
        //  Base solids (triangle soup, outward-wound)                          //
        // -------------------------------------------------------------------- //

        // Subdivided cube in [-radius, radius]^3, NOT projected to a sphere
        // (keeps basalt angular). 6 faces x N^2 quads x 2 tris = 12 N^2 tris.
        private static void BuildCubeSoup(int n, float radius, List<Vector3> outTris)
        {
            // (normal, right, up) per face with right x up = outward normal.
            Vector3[] N = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3[] R = { Vector3.up, Vector3.forward, Vector3.forward, Vector3.right, Vector3.right, Vector3.up };
            Vector3[] U = { Vector3.forward, Vector3.up, Vector3.right, Vector3.forward, Vector3.up, Vector3.right };

            for (int f = 0; f < 6; f++)
            {
                Vector3 nAxis = N[f] * radius;
                Vector3 r = R[f];
                Vector3 u = U[f];
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        Vector3 c00 = nAxis + r * ((i / (float)n - 0.5f) * 2f * radius) + u * ((j / (float)n - 0.5f) * 2f * radius);
                        Vector3 c10 = nAxis + r * (((i + 1) / (float)n - 0.5f) * 2f * radius) + u * ((j / (float)n - 0.5f) * 2f * radius);
                        Vector3 c01 = nAxis + r * ((i / (float)n - 0.5f) * 2f * radius) + u * (((j + 1) / (float)n - 0.5f) * 2f * radius);
                        Vector3 c11 = nAxis + r * (((i + 1) / (float)n - 0.5f) * 2f * radius) + u * (((j + 1) / (float)n - 0.5f) * 2f * radius);

                        // CCW from outside (right x up = normal toward viewer).
                        outTris.Add(c00); outTris.Add(c10); outTris.Add(c11);
                        outTris.Add(c00); outTris.Add(c11); outTris.Add(c01);
                    }
                }
            }
        }

        // Geodesic icosphere: icosahedron (20 faces) barycentrically tessellated
        // into k^2 sub-triangles per face and projected to the unit sphere.
        // 20 * k^2 tris. Emitted as soup (no welding needed — displacement is a
        // pure function of position, so duplicate edge verts stay coincident).
        private static void BuildIcosphereSoup(int k, float radius, List<Vector3> outTris)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] v =
            {
                new Vector3(-1,  t,  0), new Vector3( 1,  t,  0), new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
                new Vector3( 0, -1,  t), new Vector3( 0,  1,  t), new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
                new Vector3( t,  0, -1), new Vector3( t,  0,  1), new Vector3(-t,  0, -1), new Vector3(-t,  0,  1),
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;

            int[] faces =
            {
                0,11,5,  0,5,1,   0,1,7,   0,7,10,  0,10,11,
                1,5,9,   5,11,4,  11,10,2, 10,7,6,  7,1,8,
                3,9,4,   3,4,2,   3,2,6,   3,6,8,   3,8,9,
                4,9,5,   2,4,11,  6,2,10,  8,6,7,   9,8,1,
            };

            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = v[faces[f]];
                Vector3 b = v[faces[f + 1]];
                Vector3 c = v[faces[f + 2]];
                TessellateFace(a, b, c, k, radius, outTris);
            }
        }

        // Barycentric grid of a spherical triangle into k^2 sub-triangles,
        // preserving the parent A->B->C winding (outward).
        private static void TessellateFace(Vector3 a, Vector3 b, Vector3 c, int k, float radius, List<Vector3> outTris)
        {
            for (int i = 0; i < k; i++)
            {
                for (int j = 0; j < k - i; j++)
                {
                    Vector3 p00 = SpherePoint(a, b, c, i, j, k, radius);
                    Vector3 p10 = SpherePoint(a, b, c, i + 1, j, k, radius);
                    Vector3 p01 = SpherePoint(a, b, c, i, j + 1, k, radius);

                    outTris.Add(p00); outTris.Add(p10); outTris.Add(p01);

                    if (i + j < k - 1)
                    {
                        Vector3 p11 = SpherePoint(a, b, c, i + 1, j + 1, k, radius);
                        outTris.Add(p10); outTris.Add(p11); outTris.Add(p01);
                    }
                }
            }
        }

        private static Vector3 SpherePoint(Vector3 a, Vector3 b, Vector3 c, int i, int j, int k, float radius)
        {
            float wb = i / (float)k;
            float wc = j / (float)k;
            float wa = 1f - wb - wc;
            Vector3 p = a * wa + b * wb + c * wc;
            return p.normalized * radius;
        }

        // -------------------------------------------------------------------- //
        //  Deterministic hash noise (F8 — NOT Mathf.PerlinNoise)               //
        // -------------------------------------------------------------------- //

        // FNV-1a over the site string. System.String.GetHashCode is randomized
        // per process on modern runtimes, so it can NOT seed a stable bake.
        private static uint StableStringHash(string s)
        {
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 16777619u;
            }
            return h;
        }

        // Integer avalanche hash (Murmur/lowbias style). Pure bit ops -> bit
        // stable across platforms and Unity versions.
        private static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352du;
            x ^= x >> 15; x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }

        private static uint HashLattice(int xi, int yi, int zi, uint seed)
        {
            uint h = seed;
            h = Hash(h ^ ((uint)xi * 0x9E3779B1u));
            h = Hash(h ^ ((uint)yi * 0x85EBCA77u));
            h = Hash(h ^ ((uint)zi * 0xC2B2AE3Du));
            return h;
        }

        private static float HashToUnit(uint h) => (h & 0xFFFFFFu) / (float)0x1000000; // [0,1)

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f); // quintic

        // 3D value noise in [-1,1].
        private static float ValueNoise(Vector3 p, uint seed)
        {
            int xi = Mathf.FloorToInt(p.x);
            int yi = Mathf.FloorToInt(p.y);
            int zi = Mathf.FloorToInt(p.z);
            float xf = p.x - xi, yf = p.y - yi, zf = p.z - zi;
            float u = Fade(xf), vv = Fade(yf), w = Fade(zf);

            float c000 = HashToUnit(HashLattice(xi,     yi,     zi,     seed));
            float c100 = HashToUnit(HashLattice(xi + 1, yi,     zi,     seed));
            float c010 = HashToUnit(HashLattice(xi,     yi + 1, zi,     seed));
            float c110 = HashToUnit(HashLattice(xi + 1, yi + 1, zi,     seed));
            float c001 = HashToUnit(HashLattice(xi,     yi,     zi + 1, seed));
            float c101 = HashToUnit(HashLattice(xi + 1, yi,     zi + 1, seed));
            float c011 = HashToUnit(HashLattice(xi,     yi + 1, zi + 1, seed));
            float c111 = HashToUnit(HashLattice(xi + 1, yi + 1, zi + 1, seed));

            float x00 = Mathf.Lerp(c000, c100, u);
            float x10 = Mathf.Lerp(c010, c110, u);
            float x01 = Mathf.Lerp(c001, c101, u);
            float x11 = Mathf.Lerp(c011, c111, u);
            float y0 = Mathf.Lerp(x00, x10, vv);
            float y1 = Mathf.Lerp(x01, x11, vv);
            float val = Mathf.Lerp(y0, y1, w);
            return val * 2f - 1f; // [-1,1]
        }

        // Fractal Brownian motion (3 octaves), normalized to ~[-1,1].
        private static float Fbm(Vector3 p, uint seed, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(p * freq, seed + (uint)o * 0x27d4eb2fu);
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        // Deterministic xorshift32 RNG for per-archetype variety + chamfer
        // planes. Pure integer ops -> stable, unlike System.Random.
        private struct DetRng
        {
            private uint s;
            public DetRng(uint seed) { s = seed == 0u ? 1u : seed; }

            public uint NextUint()
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                return s;
            }

            public float NextFloat() => (NextUint() & 0xFFFFFFu) / (float)0x1000000;
            public float Range(float a, float b) => a + (b - a) * NextFloat();

            public Vector3 OnUnitSphere()
            {
                float z = Range(-1f, 1f);
                float theta = Range(0f, 2f * Mathf.PI);
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                return new Vector3(r * Mathf.Cos(theta), r * Mathf.Sin(theta), z);
            }
        }

        // -------------------------------------------------------------------- //
        //  Folder helper                                                       //
        // -------------------------------------------------------------------- //

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
