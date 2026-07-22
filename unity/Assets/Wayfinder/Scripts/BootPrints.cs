using UnityEngine;
using Wayfinder.Core;

namespace Wayfinder.Unity
{
    /// Leaves fading boot prints as the player walks a surface — a presence
    /// anchor (the research's "human-scale trail"): you see where you've been,
    /// which sells the ground as real and your motion as real. A capped ring
    /// buffer of quads, alpha-faded over time; oldest overwritten. Surface-only.
    public sealed class BootPrints : MonoBehaviour
    {
        [Tooltip("The head/camera — its ground position leaves prints, so physical walking (not just teleport) marks the trail.")]
        [SerializeField] private Transform head;
        [Tooltip("Flat quad prefab/mesh laid on the ground. If null, a built-in quad is used.")]
        [SerializeField] private Mesh printMesh;
        [SerializeField] private Material printMaterial;

        [Header("Trail")]
        [SerializeField] private int capacity = 48;
        [Tooltip("Metres of travel between prints.")]
        [SerializeField] private float strideLength = 0.75f;
        [SerializeField] private float printSize = 0.28f;
        [Tooltip("Seconds for a print to fade to nothing.")]
        [SerializeField] private float fadeSeconds = 45f;
        [Tooltip("Terrain layer mask for grounding prints.")]
        [SerializeField] private LayerMask groundMask = ~0;

        Transform[] _prints;
        MeshRenderer[] _renderers;
        GameObject _pool;
        MaterialPropertyBlock _mpb;
        float[] _bornAt;
        int _laid;
        Vector3 _lastPos;
        bool _active;
        Color _printRgb;
        static readonly int ID_Color = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            if (head == null) throw new System.InvalidOperationException($"{name}: no head assigned.");
            if (printMaterial == null) throw new System.InvalidOperationException($"{name}: no print material.");
            if (printMesh == null)
            {
                // built-in quad
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                printMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
            }
            _printRgb = printMaterial.HasProperty(ID_Color) ? printMaterial.GetColor(ID_Color) : Color.black;

            // The pool is NOT parented to the rig — prints mark the WORLD and
            // must stay put when the player teleports or snap-turns.
            _pool = new GameObject("BootPrintPool");
            _prints = new Transform[capacity];
            _renderers = new MeshRenderer[capacity];
            _bornAt = new float[capacity];
            _mpb = new MaterialPropertyBlock();
            for (int i = 0; i < capacity; i++)
            {
                var go = new GameObject("BootPrint_" + i);
                go.transform.SetParent(_pool.transform, false);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat
                go.transform.localScale = Vector3.one * printSize;
                var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = printMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = printMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
                _prints[i] = go.transform;
                _renderers[i] = mr;
                _bornAt[i] = -1f;
            }
            _lastPos = HeadGround();
        }

        void OnDestroy()
        {
            if (_pool != null) Destroy(_pool);
        }

        Vector3 HeadGround()
        {
            var p = head.position; p.y = 0f; return p;
        }

        /// Enable/disable trail laying (surface-only). Clears on disable.
        public void SetActive(bool active)
        {
            _active = active;
            if (!active)
            {
                for (int i = 0; i < capacity; i++) { _renderers[i].enabled = false; _bornAt[i] = -1f; }
                _laid = 0;
            }
            _lastPos = HeadGround();
        }

        void Update()
        {
            // Fade existing prints regardless (so a trail fades after you stop).
            for (int i = 0; i < capacity; i++)
            {
                if (_bornAt[i] < 0f) continue;
                float age = Time.time - _bornAt[i];
                if (age >= fadeSeconds) { _renderers[i].enabled = false; _bornAt[i] = -1f; continue; }
                float a = 1f - age / fadeSeconds;
                // Keep the material's authored dark-scuff RGB, vary only alpha.
                _mpb.SetColor(ID_Color, new Color(_printRgb.r, _printRgb.g, _printRgb.b, _printRgb.a * a));
                _renderers[i].SetPropertyBlock(_mpb);
            }

            if (!_active) return;

            Vector3 p = HeadGround();
            Vector3 d = p - _lastPos; d.y = 0f;
            float dist = d.magnitude;
            if (dist < strideLength) return;
            _lastPos = p;

            // Ground the print via a downward ray onto the terrain (world-space).
            int slot = PresenceMath.RingSlot(_laid, capacity);
            Vector3 groundPos = new Vector3(head.position.x, head.position.y, head.position.z);
            if (Physics.Raycast(groundPos + Vector3.up * 2f, Vector3.down, out var hit, 8f, groundMask))
                groundPos = hit.point + Vector3.up * 0.02f;
            // Alternate a small left/right offset for a two-foot gait.
            Vector3 right = new Vector3(d.z, 0f, -d.x).normalized;
            float side = (_laid % 2 == 0) ? 0.12f : -0.12f;
            groundPos += right * side;

            var t = _prints[slot];
            t.position = groundPos;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            t.rotation = Quaternion.Euler(90f, yaw, 0f);
            _renderers[slot].enabled = true;
            _bornAt[slot] = Time.time;
            _laid++;
        }
    }
}
