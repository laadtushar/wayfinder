using UnityEngine;

namespace Wayfinder.Unity.Scatter
{
    /// Renders the baked rock field each frame via Graphics.DrawMeshInstanced
    /// (matrices + per-batch MaterialPropertyBlock fade/tint — the well-trodden
    /// SPI-safe path, F3). Lives on the additively-loaded site scene root, so
    /// it loads/unloads with the world — no lifecycle code (World-Package
    /// doctrine). Additive presence: disabling it can never break a world or
    /// fail the frame gate.
    public sealed class ScatterRenderer : MonoBehaviour
    {
        [SerializeField] private ScatterFieldData field;
        [SerializeField] private ScatterArchetypeSet set;

        const int BatchMax = 1023; // DrawMeshInstanced hard cap per call (F4)

        Camera _cam;
        Transform _camT;

        // Fully preallocated draw buffers, one per (archetype, LOD). Nothing
        // is allocated in LateUpdate.
        Matrix4x4[,][] _mtx;   // [arch, lod] -> Matrix4x4[BatchMax]
        int[,] _count;
        RenderParams[,] _rp;   // per (arch,lod); its matProps carries the geology tint
        static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ID_GroundY = Shader.PropertyToID("_GroundY");
        int _archCount;

        void Awake()
        {
            if (field == null) throw new System.InvalidOperationException($"{name}: no ScatterFieldData.");
            if (set == null) throw new System.InvalidOperationException($"{name}: no ScatterArchetypeSet.");
            if (set.Material == null) throw new System.InvalidOperationException($"{name}: ScatterArchetypeSet has no material.");
            _archCount = set.Archetypes != null ? set.Archetypes.Length : 0;

            _mtx = new Matrix4x4[_archCount, 3][];
            _rp = new RenderParams[_archCount, 3];
            // Cull bounds from the BAKED field extent, not transform.position —
            // the field can sit kilometres from origin (Shackleton rim), and an
            // origin-centred box would cull the whole draw away.
            var bounds = field.WorldBounds;
            for (int a = 0; a < _archCount; a++)
            {
                // One geology tint per archetype, delivered as a per-batch
                // _BaseColor uniform on the RenderParams' matProps.
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(ID_BaseColor, set.Archetypes[a].Tint);
                // Object-space Y where this archetype emerges from the regolith:
                // the baker embeds by colliderRadius * scale * [0.15..0.30], and
                // object space is scale-invariant, so the visible waterline sits
                // at ~colliderRadius * 0.22. The rock shader darkens just above it.
                mpb.SetFloat(ID_GroundY, set.Archetypes[a].ColliderRadius * 0.22f);
                for (int l = 0; l < 3; l++)
                {
                    _mtx[a, l] = new Matrix4x4[BatchMax];
                    _rp[a, l] = new RenderParams(set.Material)
                    {
                        matProps = mpb,
                        shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                        receiveShadows = false,
                        worldBounds = bounds,
                    };
                }
            }
            _count = new int[_archCount, 3];
        }

        void OnEnable()
        {
            _cam = Camera.main;
            _camT = _cam != null ? _cam.transform : null;
        }

        void LateUpdate()
        {
            if (_cam == null) { _cam = Camera.main; _camT = _cam != null ? _cam.transform : null; if (_cam == null) return; }
            if (_archCount == 0 || field.Count == 0) return;

            Vector3 cam = _camT.position;
            float radiusSq = set.ScatterRadius * set.ScatterRadius;
            float lod0 = set.Lod0Distance, lod1 = set.Lod1Distance;
            int visibleCap = set.RuntimeVisibleCap;

            for (int a = 0; a < _archCount; a++)
                for (int l = 0; l < 3; l++) _count[a, l] = 0;

            var pos = field.Positions;
            var rot = field.Rotations;
            var scl = field.Scales;
            var arch = field.ArchetypeIndex;
            int n = field.Count;
            int emitted = 0;

            for (int i = 0; i < n && emitted < visibleCap; i++)
            {
                float dx = pos[i].x - cam.x, dz = pos[i].z - cam.z;
                float d2 = dx * dx + dz * dz;
                if (d2 > radiusSq) continue;

                int a = arch[i];
                if (a >= _archCount) continue;
                float d = Mathf.Sqrt(d2);
                int lod = d < lod0 ? 0 : d < lod1 ? 1 : 2;

                int slot = _count[a, lod];
                if (slot >= BatchMax) continue; // batch full; picked up next frame
                _mtx[a, lod][slot] = Matrix4x4.TRS(pos[i], rot[i], Vector3.one * scl[i]);
                _count[a, lod] = slot + 1;
                emitted++;
            }

            for (int a = 0; a < _archCount; a++)
            {
                var archetype = set.Archetypes[a];
                for (int l = 0; l < 3; l++)
                {
                    int cnt = _count[a, l];
                    if (cnt == 0) continue;
                    var mesh = archetype.Lod(l);
                    if (mesh == null) continue;

                    // RenderMeshInstanced: the URP/SRP-integrated instanced draw
                    // (legacy DrawMeshInstanced isn't reliably drawn by URP). The
                    // per-batch geology tint rides RenderParams.matProps (set in
                    // Awake); hard LOD switch, no per-instance data (F5 opaque).
                    Graphics.RenderMeshInstanced(_rp[a, l], mesh, 0, _mtx[a, l], cnt);
                }
            }
        }
    }
}
