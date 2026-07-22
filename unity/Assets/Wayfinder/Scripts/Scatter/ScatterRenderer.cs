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
        Vector4[,][] _tint;
        float[,][] _fade;
        int[,] _count;
        MaterialPropertyBlock _mpb;
        static readonly int ID_Tint = Shader.PropertyToID("_Tint");
        static readonly int ID_Fade = Shader.PropertyToID("_Fade");
        int _archCount;

        void Awake()
        {
            if (field == null) throw new System.InvalidOperationException($"{name}: no ScatterFieldData.");
            if (set == null) throw new System.InvalidOperationException($"{name}: no ScatterArchetypeSet.");
            if (set.Material == null) throw new System.InvalidOperationException($"{name}: ScatterArchetypeSet has no material.");
            _archCount = set.Archetypes != null ? set.Archetypes.Length : 0;

            _mtx = new Matrix4x4[_archCount, 3][];
            _tint = new Vector4[_archCount, 3][];
            _fade = new float[_archCount, 3][];
            for (int a = 0; a < _archCount; a++)
                for (int l = 0; l < 3; l++)
                {
                    _mtx[a, l] = new Matrix4x4[BatchMax];
                    _tint[a, l] = new Vector4[BatchMax];
                    _fade[a, l] = new float[BatchMax];
                }
            _count = new int[_archCount, 3];
            _mpb = new MaterialPropertyBlock();
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
            var tints = field.Tints;
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
                var t = tints[i];
                _tint[a, lod][slot].x = t.r; _tint[a, lod][slot].y = t.g;
                _tint[a, lod][slot].z = t.b; _tint[a, lod][slot].w = t.a;
                _fade[a, lod][slot] = 1f; // steady-state opaque preserves early-Z (F5)
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

                    _mpb.Clear();
                    // SetVectorArray/SetFloatArray upload the whole array; the
                    // draw reads `cnt` instances. Buffers are preallocated to
                    // BatchMax so this is allocation-free.
                    _mpb.SetVectorArray(ID_Tint, _tint[a, l]);
                    _mpb.SetFloatArray(ID_Fade, _fade[a, l]);

                    Graphics.DrawMeshInstanced(mesh, 0, set.Material, _mtx[a, l], cnt, _mpb,
                        UnityEngine.Rendering.ShadowCastingMode.Off, false);
                }
            }
        }
    }
}
