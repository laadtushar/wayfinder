using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wayfinder.Unity.EditorTools
{
    /// Procedural modular mesh kit for the Star-Trek-grade bridge interior
    /// (#22, docs/research/2026-07-22-ultrareal-specs.md §2, trim-band UV layout
    /// §1). 100% generated in-editor — no marketplace, no AI generator, no import
    /// gate. Matches the repo's "committable, deterministic, re-runnable" ethos
    /// (see RockMeshGenerator / ScatterBaker) and guarantees the UV -> trim-band
    /// mapping is exact so the whole interior shares ONE material (Hull_Trim.mat,
    /// shader Wayfinder/TrimLit) and collapses to ~1 SetPass under the SRP Batcher.
    ///
    /// Trim sheet V-bands (§1, a single 2048² sheet, six horizontal bands):
    ///   A 0.00–0.25  hull panel (recessed grid, bevels, rivets)   emissive 0
    ///   B 0.25–0.44  sub-panels / access hatches / screw rows      emissive 0
    ///   C 0.44–0.60  greeble strip: vents, boxes, conduit clamps   emissive tiny
    ///   D 0.60–0.72  horizontal pipe / conduit (cylinder profile)  emissive 0
    ///   E 0.72–0.84  LIGHT-COVE channel — hot emissive accent      emissive 1
    ///   F 0.84–1.00  deck-edge kick / grating                      emissive 0
    /// Every face UVs into exactly one band; a small BandEps insets each band so
    /// bilinear filtering never bleeds across a band edge.
    ///
    /// Meshes are built as triangle SOUP (each quad owns its own verts) so band
    /// seams never fight over a shared vertex's UV, and RecalculateNormals gives
    /// crisp per-face normals. Every kit mesh carries UVs + normals + TANGENTS
    /// (TrimLit is a tangent-space normal-mapped Lit shader) + bounds.
    ///
    /// Two menu items:
    ///   Wayfinder/Bridge/Generate Kit          -> writes .mesh assets to
    ///                                             Assets/Wayfinder/Meshes/Bridge/
    ///   Wayfinder/Bridge/Assemble Kit In Scene -> instantiates an octagonal
    ///                                             bridge under BridgeVisuals in
    ///                                             Bridge.unity, keeping the
    ///                                             Viewscreen/Console/BridgeFloor
    ///                                             name+comfort contracts that
    ///                                             BridgeSceneTests enforces.
    ///
    /// COMFORT LAW (CLAUDE.md / BridgeSceneTests): the octagon interior is ~3.5 m
    /// radius (fine — walls are gaze content), but the Console and Viewscreen
    /// CENTRES must be within 2.0 m horizontal of the XR Origin (world 0,0,0).
    /// The console anchors at z=1.2 (1.2 m) and the Viewscreen quad at z≈1.88
    /// (1.88 m) — both inside the 2.0 m radius. The BridgeFloor keeps a flat,
    /// non-capsule BoxCollider whose top sits at y≈0.
    public static class BridgeKitGenerator
    {
        // -------------------------------------------------------------------- //
        //  Paths / constants                                                   //
        // -------------------------------------------------------------------- //

        private const string MeshDir = "Assets/Wayfinder/Meshes/Bridge";
        private const string HullTrimMatPath = "Assets/Wayfinder/Materials/Hull_Trim.mat";
        private const string ScreenMatPath = "Assets/Wayfinder/Materials/Viewscreen_Display.mat";
        private const string BridgeScenePath = "Assets/Scenes/Bridge.unity";
        private const string KitRootName = "BridgeKit";

        // Room layout (metres). Apothem = centre-to-wall-face distance.
        private const float WallApothem = 3.5f;   // ~3.5 m radius octagon (§2)
        private const float CoveApothem = 3.3f;   // ceiling cove ring, just inboard of the wall
        private const float ConsoleZ = 1.2f;      // console centre ~1.2 m in front of origin
        private const float ViewscreenZ = 1.9f;   // bezel front plane; quad pulled to ~1.88 m

        // Trim band edges (V). Kept as literals so the mapping is auditable.
        private const float BandEps = 0.004f;
        private enum Band { A, B, C, D, E, F, AB }

        // -------------------------------------------------------------------- //
        //  Menu items                                                          //
        // -------------------------------------------------------------------- //

        [MenuItem("Wayfinder/Bridge/Generate Kit")]
        public static void GenerateKit()
        {
            EnsureFolder(MeshDir);

            var built = new (string name, Mesh mesh)[]
            {
                ("WallPanel",       BuildWallPanel()),
                ("CornerPillar",    BuildCornerPillar()),
                ("CeilingCove",     BuildCeilingCove()),
                ("ViewscreenBezel", BuildViewscreenBezel()),
                ("ConsoleShell",    BuildConsoleShell()),
                ("DeckFloor",       BuildDeckFloor()),
                ("Greeble_0",       BuildGreeble(0)),
                ("Greeble_1",       BuildGreeble(1)),
                ("Greeble_2",       BuildGreeble(2)),
                ("DisplayQuad",     BuildDisplayQuad()),
            };

            foreach (var (name, mesh) in built) SaveMesh(mesh, name);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var report = new System.Text.StringBuilder("[BridgeKitGenerator] wrote meshes to " + MeshDir + ":\n");
            foreach (var (name, mesh) in built)
                report.Append($"  Bridge_{name}: {mesh.triangles.Length / 3} tris, {mesh.vertexCount} verts\n");
            Debug.Log(report.ToString());
        }

        [MenuItem("Wayfinder/Bridge/Assemble Kit In Scene")]
        public static void AssembleKitInScene()
        {
            // Operate on the open Bridge scene; open it if it isn't active. The
            // parent runs this via the bridge, so the scene may not be loaded.
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != BridgeScenePath)
                scene = EditorSceneManager.OpenScene(BridgeScenePath, OpenSceneMode.Single);

            var bridgeVisuals = GameObject.Find("BridgeVisuals");
            if (bridgeVisuals == null)
                throw new InvalidOperationException("BridgeKitGenerator: no 'BridgeVisuals' GameObject in " + BridgeScenePath);

            KitMeshes meshes = LoadOrGenerateMeshes();

            var hullMat = AssetDatabase.LoadAssetAtPath<Material>(HullTrimMatPath);
            if (hullMat == null)
                throw new InvalidOperationException("BridgeKitGenerator: missing shared hull material at " + HullTrimMatPath);
            var screenMat = AssetDatabase.LoadAssetAtPath<Material>(ScreenMatPath);
            if (screenMat == null)
            {
                Debug.LogWarning("[BridgeKitGenerator] no Viewscreen_Display.mat — displays fall back to Hull_Trim.");
                screenMat = hullMat;
            }

            // Idempotent: drop any prior BridgeKit (removes previous canonical
            // Console/Viewscreen/BridgeFloor) BEFORE touching greybox, so the
            // greybox rename below never double-fires on a re-run.
            var priorKit = bridgeVisuals.transform.Find(KitRootName);
            if (priorKit != null) UnityEngine.Object.DestroyImmediate(priorKit.gameObject);

            // Preserve — never delete — the greybox. Walls + the three contract
            // objects are disabled so the kit reads clean in Direct Preview; a
            // later step removes them after visual confirmation. Renaming the
            // contract objects frees the names for the kit's canonical versions
            // (GameObject.Find ignores inactive objects, so rename is belt-and-
            // braces against name-collision ambiguity).
            DisableGreybox(bridgeVisuals.transform);

            var kit = new GameObject(KitRootName);
            kit.transform.SetParent(bridgeVisuals.transform, false);
            BuildAssembly(kit.transform, meshes, hullMat, screenMat);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BridgeKitGenerator] assembled BridgeKit under BridgeVisuals in " + BridgeScenePath +
                      " (8 walls, 8 pillars, 8 coves, floor, bezel+Viewscreen, Console+3 screens, 8 greebles).");
        }

        // -------------------------------------------------------------------- //
        //  Assembly                                                            //
        // -------------------------------------------------------------------- //

        private static void DisableGreybox(Transform bridgeVisuals)
        {
            var children = new List<Transform>();
            foreach (Transform c in bridgeVisuals) children.Add(c);

            foreach (var c in children)
            {
                if (c.name == KitRootName) continue;
                if (c.name.StartsWith("HullSegment"))
                {
                    c.gameObject.SetActive(false);
                }
                else if (c.name == "Console" || c.name == "Viewscreen" || c.name == "BridgeFloor")
                {
                    c.name = c.name + "_Greybox";
                    c.gameObject.SetActive(false);
                }
                // BridgeCeiling / KeyLight / probes are left as-is.
            }
        }

        private static void BuildAssembly(Transform kit, KitMeshes m, Material hull, Material screen)
        {
            // Deck floor — the 'BridgeFloor' contract object. Flat box collider,
            // top surface at y≈0 (kit sits at world origin under BridgeVisuals).
            var floor = MakePiece("BridgeFloor", m.deckFloor, hull, kit);
            floor.transform.localPosition = Vector3.zero;
            var col = floor.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, -0.05f, 0f);
            col.size = new Vector3(2f * WallApothem, 0.1f, 2f * WallApothem); // top at y = -0.05 + 0.05 = 0

            // Octagon of walls (on the flat edges) + pillars (on the vertices) +
            // ceiling coves (a soffit ring at the top). Front edge is centred on
            // +Z, so octagon edges are centred at (90° + 45°k).
            float edgeLen = 2f * WallApothem * Mathf.Tan(Mathf.PI / 8f);
            float wallScaleX = (edgeLen * 1.02f) / 2.0f; // stretch the 2 m module to fill/overlap the edge
            float vertexRadius = WallApothem / Mathf.Cos(Mathf.PI / 8f);

            for (int k = 0; k < 8; k++)
            {
                float edgeAng = (90f + 45f * k) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(edgeAng), 0f, Mathf.Sin(edgeAng)); // outward
                Quaternion inward = Quaternion.LookRotation(-dir, Vector3.up);          // local +Z -> centre

                var wall = MakePiece($"WallPanel_{k}", m.wall, hull, kit);
                wall.transform.localPosition = dir * WallApothem;
                wall.transform.localRotation = inward;
                wall.transform.localScale = new Vector3(wallScaleX, 1f, 1f);

                var cove = MakePiece($"CeilingCove_{k}", m.cove, hull, kit);
                cove.transform.localPosition = dir * CoveApothem;
                cove.transform.localRotation = inward;

                var greeble = MakePiece($"Greeble_{k}", m.greeble[k % 3], hull, kit);
                greeble.transform.localPosition = dir * (WallApothem - 0.25f) + Vector3.up * 0.5f;
                greeble.transform.localRotation = inward;

                float vertAng = (112.5f + 45f * k) * Mathf.Deg2Rad;
                Vector3 vdir = new Vector3(Mathf.Cos(vertAng), 0f, Mathf.Sin(vertAng));
                var pillar = MakePiece($"CornerPillar_{k}", m.pillar, hull, kit);
                pillar.transform.localPosition = vdir * vertexRadius;
            }

            // Viewscreen bezel on the front wall. The frame's front normal is +Z;
            // a 180° yaw turns it to face −Z (the player). The 'Viewscreen'
            // display quad is a child pulled ~0.02 m toward the player so its
            // centre lands at z≈1.88 (inside the 2.0 m comfort radius).
            var bezel = MakePiece("ViewscreenBezel", m.bezel, hull, kit);
            bezel.transform.localPosition = new Vector3(0f, 1.5f, ViewscreenZ);
            bezel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var viewscreen = MakePiece("Viewscreen", m.displayQuad, screen, bezel.transform);
            viewscreen.transform.localPosition = new Vector3(0f, 0f, 0.02f); // → world z = 1.88 after 180° yaw
            viewscreen.transform.localScale = new Vector3(2.4f, 1.4f, 1f);

            // Console — the hero. Centre 1.2 m in front of the player origin.
            var console = MakePiece("Console", m.console, hull, kit);
            console.transform.localPosition = new Vector3(0f, 0f, ConsoleZ);

            float[] screenX = { -0.42f, 0f, 0.42f };
            for (int i = 0; i < screenX.Length; i++)
            {
                var sc = MakePiece($"ConsoleScreen_{i}", m.displayQuad, screen, console.transform);
                sc.transform.localPosition = new Vector3(screenX[i], 1.115f, 0.06f);
                sc.transform.localRotation = Quaternion.Euler(-95f, 0f, 0f); // face up, tilted toward the player
                sc.transform.localScale = new Vector3(0.34f, 0.22f, 1f);
            }
        }

        private static GameObject MakePiece(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        // -------------------------------------------------------------------- //
        //  Mesh: WallPanel  (2 m × 2.5 m, beveled front, A/B upper + C lower)   //
        // -------------------------------------------------------------------- //

        private static Mesh BuildWallPanel()
        {
            var mb = new MeshBuf();
            const float W = 2.0f, H = 2.5f, D = 0.13f;
            const float zRim = 0.0f, zFace = 0.03f, zBack = -D;
            const float insetX = 0.10f, insetY = 0.12f;

            float x0 = -W * 0.5f, x1 = W * 0.5f;
            float fx0 = x0 + insetX, fx1 = x1 - insetX;
            float fy0 = insetY, fy1 = H - insetY;
            float splitY = fy0 + (fy1 - fy0) / 3f; // lower third boundary

            // Raised front face — lower third into band C, upper two-thirds A/B.
            BuildFaceGrid(mb, fx0, fx1, fy0, splitY, zFace, 4, 2, Band.C);
            BuildFaceGrid(mb, fx0, fx1, splitY, fy1, zFace, 4, 4, Band.AB);

            // Bevel rim (outer rim z=0 -> raised face z=0.03) — band A so the
            // normal-mapped panel lines catch the key light on the chamfer.
            mb.Quad(V3(x0, 0, zRim), V3(x1, 0, zRim), V3(fx1, fy0, zFace), V3(fx0, fy0, zFace),
                    UV(Band.A, 0, 0), UV(Band.A, 1, 0), UV(Band.A, 1, 0.15f), UV(Band.A, 0, 0.15f));
            mb.Quad(V3(fx0, fy1, zFace), V3(fx1, fy1, zFace), V3(x1, H, zRim), V3(x0, H, zRim),
                    UV(Band.A, 0, 0.85f), UV(Band.A, 1, 0.85f), UV(Band.A, 1, 1), UV(Band.A, 0, 1));
            mb.Quad(V3(x0, 0, zRim), V3(fx0, fy0, zFace), V3(fx0, fy1, zFace), V3(x0, H, zRim),
                    UV(Band.A, 0, 0), UV(Band.A, 0.15f, 0), UV(Band.A, 0.15f, 1), UV(Band.A, 0, 1));
            mb.Quad(V3(x1, 0, zRim), V3(x1, H, zRim), V3(fx1, fy1, zFace), V3(fx1, fy0, zFace),
                    UV(Band.A, 1, 0), UV(Band.A, 1, 1), UV(Band.A, 0.85f, 1), UV(Band.A, 0.85f, 0));

            // Side depth walls (band B) + back (band A) — thin edges seen at joints.
            mb.Quad(V3(x0, 0, zBack), V3(x0, 0, zRim), V3(x0, H, zRim), V3(x0, H, zBack),
                    UV(Band.B, 0, 0), UV(Band.B, 1, 0), UV(Band.B, 1, 1), UV(Band.B, 0, 1));
            mb.Quad(V3(x1, 0, zRim), V3(x1, 0, zBack), V3(x1, H, zBack), V3(x1, H, zRim),
                    UV(Band.B, 0, 0), UV(Band.B, 1, 0), UV(Band.B, 1, 1), UV(Band.B, 0, 1));
            mb.Quad(V3(x0, 0, zBack), V3(x1, 0, zBack), V3(x1, 0, zRim), V3(x0, 0, zRim),
                    UV(Band.B, 0, 0), UV(Band.B, 1, 0), UV(Band.B, 1, 1), UV(Band.B, 0, 1));
            mb.Quad(V3(x0, H, zRim), V3(x1, H, zRim), V3(x1, H, zBack), V3(x0, H, zBack),
                    UV(Band.B, 0, 0), UV(Band.B, 1, 0), UV(Band.B, 1, 1), UV(Band.B, 0, 1));
            mb.Quad(V3(x1, 0, zBack), V3(x0, 0, zBack), V3(x0, H, zBack), V3(x1, H, zBack),
                    UV(Band.A, 0, 0), UV(Band.A, 1, 0), UV(Band.A, 1, 1), UV(Band.A, 0, 1));

            return mb.ToMesh("Bridge_WallPanel");
        }

        // Flat +Z-facing subdivided rectangle, its rows swept through one band.
        private static void BuildFaceGrid(MeshBuf mb, float x0, float x1, float y0, float y1,
                                          float z, int cols, int rows, Band band)
        {
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    float ax = Mathf.Lerp(x0, x1, (float)c / cols), bx = Mathf.Lerp(x0, x1, (float)(c + 1) / cols);
                    float ay = Mathf.Lerp(y0, y1, (float)r / rows), by = Mathf.Lerp(y0, y1, (float)(r + 1) / rows);
                    float u0 = (float)c / cols, u1 = (float)(c + 1) / cols;
                    float t0 = (float)r / rows, t1 = (float)(r + 1) / rows;
                    mb.Quad(V3(ax, ay, z), V3(bx, ay, z), V3(bx, by, z), V3(ax, by, z),
                            UV(band, u0, t0), UV(band, u1, t0), UV(band, u1, t1), UV(band, u0, t1));
                }
        }

        // -------------------------------------------------------------------- //
        //  Mesh: CornerPillar  (octagon-joint post, band D pipe)               //
        // -------------------------------------------------------------------- //

        private static Mesh BuildCornerPillar()
        {
            var mb = new MeshBuf();
            const int sides = 8, segs = 3;
            const float r = 0.16f, H = 2.6f;

            for (int s = 0; s < sides; s++)
            {
                float a0 = (float)s / sides * Mathf.PI * 2f, a1 = (float)(s + 1) / sides * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r;
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r;
                float af0 = (float)s / sides, af1 = (float)(s + 1) / sides;

                for (int v = 0; v < segs; v++)
                {
                    float y0 = H * v / segs, y1 = H * (v + 1) / segs;
                    float hf0 = (float)v / segs, hf1 = (float)(v + 1) / segs;
                    Vector3 b0 = d0 + Vector3.up * y0, t0 = d0 + Vector3.up * y1;
                    Vector3 b1 = d1 + Vector3.up * y0, t1 = d1 + Vector3.up * y1;
                    // Band D's painted cylinder runs across V; wrap the pillar's
                    // circumference onto V (round highlight goes around the post),
                    // height onto U.
                    mb.Quad(b0, t0, t1, b1,
                            UV(Band.D, hf0, af0), UV(Band.D, hf1, af0),
                            UV(Band.D, hf1, af1), UV(Band.D, hf0, af1));
                }
            }

            // Caps.
            Vector3 top = Vector3.up * H;
            for (int s = 0; s < sides; s++)
            {
                float a0 = (float)s / sides * Mathf.PI * 2f, a1 = (float)(s + 1) / sides * Mathf.PI * 2f;
                Vector3 p0t = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r + top;
                Vector3 p1t = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r + top;
                Vector3 p0b = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r;
                Vector3 p1b = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r;
                mb.Tri(top, p0t, p1t, UV(Band.D, 0.5f, 0.5f), UV(Band.D, Mathf.Cos(a0) * 0.5f + 0.5f, 0.5f), UV(Band.D, Mathf.Cos(a1) * 0.5f + 0.5f, 0.5f));
                mb.Tri(Vector3.zero, p1b, p0b, UV(Band.D, 0.5f, 0.5f), UV(Band.D, Mathf.Cos(a1) * 0.5f + 0.5f, 0.5f), UV(Band.D, Mathf.Cos(a0) * 0.5f + 0.5f, 0.5f));
            }

            return mb.ToMesh("Bridge_CornerPillar");
        }

        // -------------------------------------------------------------------- //
        //  Mesh: CeilingCove  (angled soffit hiding an emissive strip, band E)  //
        // -------------------------------------------------------------------- //

        private static Mesh BuildCeilingCove()
        {
            var mb = new MeshBuf();
            const float L = 3.0f;
            const int nx = 4;

            // Cross-section (z,y); local +Z = inward (toward room centre after
            // placement). Soffit slopes down-inward and HIDES the lip; the lip
            // (band E) faces into the room and reads as the glowing accent line.
            Vector2 p0 = new Vector2(-0.30f, 2.50f); // outer top (at the wall)
            Vector2 p1 = new Vector2(+0.15f, 2.32f); // soffit lower-inner
            Vector2 p2 = new Vector2(+0.10f, 2.08f); // lip bottom
            Vector2 p3 = new Vector2(-0.05f, 2.06f); // underside back

            BuildExtruded(mb, p0, p1, L, nx, Band.A, flip: false); // soffit (down-facing)
            BuildExtruded(mb, p1, p2, L, nx, Band.E, flip: true);  // emissive lip (faces room)
            BuildExtruded(mb, p2, p3, L, nx, Band.A, flip: true);  // underside (down-facing)

            return mb.ToMesh("Bridge_CeilingCove");
        }

        // Extrude cross-section edge a->b along local X, nx segments. 'flip'
        // chooses which of the two surface normals faces out.
        private static void BuildExtruded(MeshBuf mb, Vector2 a, Vector2 b, float L, int nx, Band band, bool flip)
        {
            for (int i = 0; i < nx; i++)
            {
                float x0 = -L * 0.5f + L * i / nx, x1 = -L * 0.5f + L * (i + 1) / nx;
                float u0 = (float)i / nx, u1 = (float)(i + 1) / nx;
                Vector3 a0 = new Vector3(x0, a.y, a.x), a1 = new Vector3(x1, a.y, a.x);
                Vector3 b0 = new Vector3(x0, b.y, b.x), b1 = new Vector3(x1, b.y, b.x);
                if (flip)
                    mb.Quad(a0, b0, b1, a1, UV(band, u0, 0), UV(band, u0, 1), UV(band, u1, 1), UV(band, u1, 0));
                else
                    mb.Quad(a0, a1, b1, b0, UV(band, u0, 0), UV(band, u1, 0), UV(band, u1, 1), UV(band, u0, 1));
            }
        }

        // -------------------------------------------------------------------- //
        //  Mesh: ViewscreenBezel  (chamfered frame, band A + chamfer band B)    //
        // -------------------------------------------------------------------- //

        private static Mesh BuildViewscreenBezel()
        {
            var mb = new MeshBuf();
            const float ox = 1.2f, oy = 0.7f; // opening half-extents (2.4 × 1.4 m)
            const int sps = 5;                 // segments per octagon... rectangle side

            var l0 = RectLoop(ox, oy, 0.02f, sps);                 // inner opening edge
            var l1 = RectLoop(ox + 0.06f, oy + 0.06f, 0.07f, sps); // raised front-inner (chamfer)
            var l2 = RectLoop(1.40f, 0.90f, 0.03f, sps);           // front flat outer
            var l3 = RectLoop(1.42f, 0.92f, 0.00f, sps);           // outer rim (chamfer)
            var l4 = RectLoop(1.42f, 0.92f, -0.10f, sps);          // back depth

            ConnectLoops(mb, l0, l1, Band.B); // inner chamfer
            ConnectLoops(mb, l1, l2, Band.A); // front flat
            ConnectLoops(mb, l2, l3, Band.B); // outer chamfer
            ConnectLoops(mb, l3, l4, Band.B); // outer side

            return mb.ToMesh("Bridge_ViewscreenBezel");
        }

        private static List<Vector3> RectLoop(float hx, float hy, float z, int sps)
        {
            var pts = new List<Vector3>();
            Vector2[] corners = { new Vector2(-hx, -hy), new Vector2(hx, -hy), new Vector2(hx, hy), new Vector2(-hx, hy) };
            for (int e = 0; e < 4; e++)
            {
                Vector2 a = corners[e], b = corners[(e + 1) % 4];
                for (int i = 0; i < sps; i++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (float)i / sps);
                    pts.Add(new Vector3(p.x, p.y, z));
                }
            }
            return pts; // 4*sps points, CCW
        }

        private static void ConnectLoops(MeshBuf mb, List<Vector3> a, List<Vector3> b, Band band)
        {
            int n = a.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                mb.Quad(a[i], b[i], b[j], a[j],
                        UV(band, (float)i / n, 0), UV(band, (float)i / n, 1),
                        UV(band, (float)j / n, 1), UV(band, (float)j / n, 0));
            }
        }

        // -------------------------------------------------------------------- //
        //  Mesh: ConsoleShell  (hero swept console, A/B + screen recesses)      //
        // -------------------------------------------------------------------- //

        private static Mesh BuildConsoleShell()
        {
            var mb = new MeshBuf();

            // Cross-section (z,y). Front is toward −Z (the player). Points 0–6
            // front face (band A), 6–10 angled top ~1.1 m (band B, screens sit
            // here), 10–14 back panel (band A), seg 14 closes the bottom.
            Vector2[] prof =
            {
                new Vector2(-0.38f, 0.00f),
                new Vector2(-0.40f, 0.16f),
                new Vector2(-0.40f, 0.34f),
                new Vector2(-0.40f, 0.52f),
                new Vector2(-0.39f, 0.70f),
                new Vector2(-0.36f, 0.86f),
                new Vector2(-0.30f, 0.98f), // front top lip
                new Vector2(-0.16f, 1.05f), // angled top start
                new Vector2( 0.02f, 1.09f), // top mid
                new Vector2( 0.20f, 1.10f), // top back
                new Vector2( 0.30f, 1.06f), // back top edge
                new Vector2( 0.32f, 0.78f),
                new Vector2( 0.32f, 0.42f),
                new Vector2( 0.31f, 0.12f),
                new Vector2( 0.28f, 0.00f),
            };
            int p = prof.Length;   // 15
            const int ns = 160;    // arc stations — the console can be lavish (§2)
            const float rc = 1.6f, beta = 0.45f;

            Vector3 Pt(int j, int i)
            {
                float phi = Mathf.Lerp(-beta, beta, (float)j / ns);
                float rad = rc + prof[i].x;
                return new Vector3(Mathf.Sin(phi) * rad, prof[i].y, Mathf.Cos(phi) * rad - rc);
            }

            void SegUV(int i, out Band b, out float v0, out float v1)
            {
                if (i < 6) { b = Band.A; v0 = i / 6f; v1 = (i + 1) / 6f; }
                else if (i < 10) { b = Band.B; v0 = (i - 6) / 4f; v1 = (i - 6 + 1) / 4f; }
                else { b = Band.A; v0 = (i - 10) / 5f; v1 = (i - 10 + 1) / 5f; }
            }

            for (int j = 0; j < ns; j++)
                for (int i = 0; i < p; i++)
                {
                    int i2 = (i + 1) % p;
                    Vector3 a = Pt(j, i), b = Pt(j, i2), c = Pt(j + 1, i2), d = Pt(j + 1, i);
                    SegUV(i, out Band band, out float v0, out float v1);
                    float u0 = 2f * j / ns, u1 = 2f * (j + 1) / ns;
                    mb.Quad(a, b, c, d, UV(band, u0, v0), UV(band, u0, v1), UV(band, u1, v1), UV(band, u1, v0));
                }

            // End caps (band A) — fan over the closed profile at each arc end.
            for (int end = 0; end < 2; end++)
            {
                int j = end == 0 ? 0 : ns;
                Vector3 centroid = Vector3.zero;
                for (int i = 0; i < p; i++) centroid += Pt(j, i);
                centroid /= p;
                for (int i = 0; i < p; i++)
                {
                    int i2 = (i + 1) % p;
                    Vector3 a = Pt(j, i), b = Pt(j, i2);
                    if (end == 0)
                        mb.Tri(centroid, a, b, UV(Band.A, 0.5f, 0.5f), UV(Band.A, 0, 0), UV(Band.A, 1, 0));
                    else
                        mb.Tri(centroid, b, a, UV(Band.A, 0.5f, 0.5f), UV(Band.A, 1, 0), UV(Band.A, 0, 0));
                }
            }

            return mb.ToMesh("Bridge_ConsoleShell");
        }

        // -------------------------------------------------------------------- //
        //  Mesh: DeckFloor  (octagonal inset grid, A field + F edge)            //
        // -------------------------------------------------------------------- //

        private static Mesh BuildDeckFloor()
        {
            var mb = new MeshBuf();
            const float ro = 3.5f, ri = 3.0f;
            const int edgeSub = 3;
            int n = 8 * edgeSub; // perimeter points

            float[] fieldRings = { 0f, 1.0f, 2.0f, ri };

            // Central octagon field (band A): centre fan + concentric ring strips.
            for (int gi = 0; gi < fieldRings.Length - 1; gi++)
            {
                float rInner = fieldRings[gi], rOuter = fieldRings[gi + 1];
                float vInner = rInner / ri, vOuter = rOuter / ri;
                for (int pIdx = 0; pIdx < n; pIdx++)
                {
                    int pj = (pIdx + 1) % n;
                    if (rInner <= 0f)
                    {
                        mb.Tri(Vector3.zero, OctPoint(pIdx, edgeSub, rOuter), OctPoint(pj, edgeSub, rOuter),
                               UV(Band.A, 0.5f, 0f),
                               UV(Band.A, (float)pIdx / n, vOuter), UV(Band.A, (float)pj / n, vOuter));
                    }
                    else
                    {
                        mb.Quad(OctPoint(pIdx, edgeSub, rInner), OctPoint(pj, edgeSub, rInner),
                                OctPoint(pj, edgeSub, rOuter), OctPoint(pIdx, edgeSub, rOuter),
                                UV(Band.A, (float)pIdx / n, vInner), UV(Band.A, (float)pj / n, vInner),
                                UV(Band.A, (float)pj / n, vOuter), UV(Band.A, (float)pIdx / n, vOuter));
                    }
                }
            }

            // Deck-edge kick ring (band F, ri..ro).
            for (int pIdx = 0; pIdx < n; pIdx++)
            {
                int pj = (pIdx + 1) % n;
                mb.Quad(OctPoint(pIdx, edgeSub, ri), OctPoint(pj, edgeSub, ri),
                        OctPoint(pj, edgeSub, ro), OctPoint(pIdx, edgeSub, ro),
                        UV(Band.F, (float)pIdx / n, 0f), UV(Band.F, (float)pj / n, 0f),
                        UV(Band.F, (float)pj / n, 1f), UV(Band.F, (float)pIdx / n, 1f));
            }

            return mb.ToMesh("Bridge_DeckFloor");
        }

        // Point on a regular octagon (apothem = 'apothem'), one flat edge centred
        // on +Z. p in [0, 8*edgeSub); returned on the y=0 plane.
        private static Vector3 OctPoint(int pIdx, int edgeSub, float apothem)
        {
            int edge = pIdx / edgeSub;
            float t = (float)(pIdx % edgeSub) / edgeSub;
            float rc = apothem / Mathf.Cos(Mathf.PI / 8f);
            float a0 = (22.5f + 45f * edge) * Mathf.Deg2Rad;
            float a1 = (22.5f + 45f * (edge + 1)) * Mathf.Deg2Rad;
            Vector2 c0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * rc;
            Vector2 c1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * rc;
            Vector2 e = Vector2.Lerp(c0, c1, t);
            return new Vector3(e.x, 0f, e.y);
        }

        // -------------------------------------------------------------------- //
        //  Mesh: Greeble archetypes  (small boxes/vents, band C)                //
        // -------------------------------------------------------------------- //

        private static Mesh BuildGreeble(int arch)
        {
            var mb = new MeshBuf();
            switch (arch)
            {
                case 0: // louvered vent
                    AddBox(mb, new Vector3(0f, 0.16f, 0f), new Vector3(0.50f, 0.32f, 0.16f), Band.C);
                    for (int i = 0; i < 9; i++)
                        AddBox(mb, new Vector3(0f, 0.05f + i * 0.03f, 0.085f), new Vector3(0.44f, 0.018f, 0.03f), Band.C);
                    AddBox(mb, new Vector3(-0.24f, 0.16f, 0.06f), new Vector3(0.03f, 0.30f, 0.08f), Band.C);
                    AddBox(mb, new Vector3(0.24f, 0.16f, 0.06f), new Vector3(0.03f, 0.30f, 0.08f), Band.C);
                    break;
                case 1: // junction box + conduit stubs + bolts
                    AddBox(mb, new Vector3(0f, 0.14f, 0f), new Vector3(0.40f, 0.28f, 0.24f), Band.C);
                    for (int i = 0; i < 3; i++)
                        AddBox(mb, new Vector3(-0.12f + i * 0.12f, 0.30f, 0f), new Vector3(0.05f, 0.12f, 0.05f), Band.C);
                    for (int i = 0; i < 8; i++)
                        AddBox(mb, new Vector3((i % 4) * 0.10f - 0.15f, (i / 4) * 0.16f + 0.05f, 0.13f), new Vector3(0.03f, 0.03f, 0.03f), Band.C);
                    break;
                default: // conduit-clamp stack
                    AddBox(mb, new Vector3(0f, 0.05f, 0f), new Vector3(0.46f, 0.10f, 0.30f), Band.C);
                    for (int i = 0; i < 4; i++)
                        AddBox(mb, new Vector3(0f, 0.14f + i * 0.09f, 0f), new Vector3(0.40f - i * 0.06f, 0.06f, 0.24f), Band.C);
                    for (int i = 0; i < 6; i++)
                        AddBox(mb, new Vector3(-0.20f + i * 0.08f, 0.10f, 0.14f), new Vector3(0.04f, 0.16f, 0.04f), Band.C);
                    break;
            }
            return mb.ToMesh("Bridge_Greeble_" + arch);
        }

        private static void AddBox(MeshBuf mb, Vector3 c, Vector3 s, Band band)
        {
            Vector3 h = s * 0.5f;
            float xm = c.x - h.x, xp = c.x + h.x, ym = c.y - h.y, yp = c.y + h.y, zm = c.z - h.z, zp = c.z + h.z;
            Vector2 u00 = UV(band, 0, 0), u10 = UV(band, 1, 0), u11 = UV(band, 1, 1), u01 = UV(band, 0, 1);
            mb.Quad(V3(xm, ym, zp), V3(xp, ym, zp), V3(xp, yp, zp), V3(xm, yp, zp), u00, u10, u11, u01); // +Z
            mb.Quad(V3(xp, ym, zm), V3(xm, ym, zm), V3(xm, yp, zm), V3(xp, yp, zm), u00, u10, u11, u01); // -Z
            mb.Quad(V3(xp, ym, zp), V3(xp, ym, zm), V3(xp, yp, zm), V3(xp, yp, zp), u00, u10, u11, u01); // +X
            mb.Quad(V3(xm, ym, zm), V3(xm, ym, zp), V3(xm, yp, zp), V3(xm, yp, zm), u00, u10, u11, u01); // -X
            mb.Quad(V3(xm, yp, zp), V3(xp, yp, zp), V3(xp, yp, zm), V3(xm, yp, zm), u00, u10, u11, u01); // +Y
            mb.Quad(V3(xm, ym, zm), V3(xp, ym, zm), V3(xp, ym, zp), V3(xm, ym, zp), u00, u10, u11, u01); // -Y
        }

        // -------------------------------------------------------------------- //
        //  Mesh: DisplayQuad  (shared unit quad, +Z normal, UV 0..1)            //
        // -------------------------------------------------------------------- //

        private static Mesh BuildDisplayQuad()
        {
            var mb = new MeshBuf();
            mb.Quad(V3(-0.5f, -0.5f, 0), V3(0.5f, -0.5f, 0), V3(0.5f, 0.5f, 0), V3(-0.5f, 0.5f, 0),
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            return mb.ToMesh("Bridge_DisplayQuad");
        }

        // -------------------------------------------------------------------- //
        //  Trim-band UV mapping                                                 //
        // -------------------------------------------------------------------- //

        private static Vector2 UV(Band b, float u, float t) => new Vector2(u, BandV(b, t));

        private static float BandV(Band b, float t)
        {
            switch (b)
            {
                case Band.A: return Lerp(0.00f, 0.25f, t);
                case Band.B: return Lerp(0.25f, 0.44f, t);
                case Band.C: return Lerp(0.44f, 0.60f, t);
                case Band.D: return Lerp(0.60f, 0.72f, t);
                case Band.E: return Lerp(0.72f, 0.84f, t);
                case Band.F: return Lerp(0.84f, 1.00f, t);
                case Band.AB: return Lerp(0.00f, 0.44f, t); // "upper two-thirds into A/B"
                default: return 0f;
            }
        }

        // Band-inset lerp — BandEps keeps every V just inside its band so the
        // trim sheet's bilinear filter never samples across a band boundary.
        private static float Lerp(float lo, float hi, float t) => Mathf.Lerp(lo + BandEps, hi - BandEps, Mathf.Clamp01(t));

        private static Vector3 V3(float x, float y, float z) => new Vector3(x, y, z);

        // -------------------------------------------------------------------- //
        //  Mesh buffer (triangle soup) + finalize                              //
        // -------------------------------------------------------------------- //

        private sealed class MeshBuf
        {
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<Vector2> _uv = new List<Vector2>();

            public void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
                             Vector2 t0, Vector2 t1, Vector2 t2, Vector2 t3)
            {
                _v.Add(p0); _uv.Add(t0); _v.Add(p1); _uv.Add(t1); _v.Add(p2); _uv.Add(t2);
                _v.Add(p0); _uv.Add(t0); _v.Add(p2); _uv.Add(t2); _v.Add(p3); _uv.Add(t3);
            }

            public void Tri(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 t0, Vector2 t1, Vector2 t2)
            {
                _v.Add(p0); _uv.Add(t0); _v.Add(p1); _uv.Add(t1); _v.Add(p2); _uv.Add(t2);
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.indexFormat = _v.Count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16;
                int[] idx = new int[_v.Count];
                for (int i = 0; i < idx.Length; i++) idx[i] = i; // soup: 1:1 vert<->index
                m.SetVertices(_v);
                m.SetUVs(0, _uv);
                m.SetTriangles(idx, 0);
                m.RecalculateNormals();  // flat per-face normals (soup)
                m.RecalculateTangents(); // TrimLit is tangent-space normal-mapped
                m.RecalculateBounds();
                m.UploadMeshData(false); // keep readable for editor tooling
                return m;
            }
        }

        // -------------------------------------------------------------------- //
        //  Asset IO                                                            //
        // -------------------------------------------------------------------- //

        private struct KitMeshes
        {
            public Mesh wall, pillar, cove, bezel, console, deckFloor, displayQuad;
            public Mesh[] greeble;
        }

        private static Mesh LoadMesh(string name) =>
            AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/Bridge_{name}.asset");

        private static KitMeshes LoadMeshes() => new KitMeshes
        {
            wall = LoadMesh("WallPanel"),
            pillar = LoadMesh("CornerPillar"),
            cove = LoadMesh("CeilingCove"),
            bezel = LoadMesh("ViewscreenBezel"),
            console = LoadMesh("ConsoleShell"),
            deckFloor = LoadMesh("DeckFloor"),
            displayQuad = LoadMesh("DisplayQuad"),
            greeble = new[] { LoadMesh("Greeble_0"), LoadMesh("Greeble_1"), LoadMesh("Greeble_2") },
        };

        private static KitMeshes LoadOrGenerateMeshes()
        {
            var m = LoadMeshes();
            bool complete = m.wall && m.pillar && m.cove && m.bezel && m.console && m.deckFloor &&
                            m.displayQuad && m.greeble[0] && m.greeble[1] && m.greeble[2];
            if (!complete)
            {
                GenerateKit();
                m = LoadMeshes();
            }
            return m;
        }

        private static void SaveMesh(Mesh mesh, string name)
        {
            string path = $"{MeshDir}/Bridge_{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
        }

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
