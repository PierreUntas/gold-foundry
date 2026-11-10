using UnityEngine;

namespace GoldFoundry
{
    /// <summary>Un morceau de minerai : lourd, nombreux, il "veut se rassembler".</summary>
    public class GFOre : MonoBehaviour
    {
        public Rigidbody rb;
        public Material mat;
        public Color baseCol;
        public Vector3 baseScale;
        public bool melting;
        float meltT, meltDur;

        static readonly Color HotBase = new Color(1f, 0.45f, 0.1f);
        static readonly Color HotEmit = new Color(4f, 1.5f, 0.25f);

        public void Spawn(Vector3 pos)
        {
            transform.position = pos;
            transform.rotation = Random.rotation;
            transform.localScale = baseScale;
            rb.position = pos;
            rb.rotation = transform.rotation;
            gameObject.SetActive(true);
            rb.linearVelocity = new Vector3(Random.Range(-0.3f, 0.3f), -1f, Random.Range(-0.2f, 0.2f));
            rb.angularVelocity = Random.insideUnitSphere * 3f;
            melting = false;
            GFMat.SetBase(mat, baseCol);
            GFMat.SetEmission(mat, Color.black);
        }

        public void StartMelt(float dur)
        {
            melting = true;
            meltT = 0f;
            meltDur = dur;
        }

        /// <summary>Renvoie true quand le morceau a complètement fondu.</summary>
        public bool TickMelt(float dt)
        {
            meltT += dt;
            float k = Mathf.Clamp01(meltT / meltDur);
            GFMat.SetBase(mat, Color.Lerp(baseCol, HotBase, Mathf.Clamp01(k * 2f)));
            GFMat.SetEmission(mat, HotEmit * Mathf.Clamp01(k * 1.6f));
            transform.localScale = baseScale * Mathf.Max(0.05f, 1f - k * k);
            return k >= 1f;
        }

        void OnCollisionEnter(Collision c)
        {
            if (melting || GFSfx.I == null) return;
            float v = c.relativeVelocity.magnitude;
            if (v > 1.2f) GFSfx.I.OreHit((v - 1.2f) / 5f);
        }
    }

    /// <summary>Tapis roulant : entraîne les rigidbodies posés dessus, fait défiler sa texture.</summary>
    public class GFBelt : MonoBehaviour
    {
        public bool Running = true;
        public float Speed = 1.3f;
        public Material mat;
        public Transform[] rollers;
        float offset;

        void OnCollisionStay(Collision c)
        {
            Rigidbody r = c.rigidbody;
            if (r == null || r.isKinematic) return;
            Vector3 v = r.linearVelocity;
            v.x = Mathf.MoveTowards(v.x, Running ? Speed : 0f, 25f * Time.fixedDeltaTime);
            r.linearVelocity = v;
        }

        void Update()
        {
            if (!Running) return;
            float dt = Time.deltaTime;
            offset -= Speed * dt / 0.45f;
            if (mat != null) mat.mainTextureOffset = new Vector2(offset, 0f);
            if (rollers != null)
                foreach (Transform r in rollers)
                    if (r != null) r.Rotate(0f, -Speed * dt / 0.17f * Mathf.Rad2Deg, 0f, Space.Self);
        }
    }

    /// <summary>Zone tactile invisible qui déclenche un micro-geste.</summary>
    public class GFTap : MonoBehaviour
    {
        public System.Action OnTap;
    }

    /// <summary>Petit rebond vertical pour rendre un toucher visible.</summary>
    public class GFPunch : MonoBehaviour
    {
        Vector3 basePos;
        float t = 1f;

        void Awake() { basePos = transform.localPosition; }

        public void Punch() { t = 0f; }

        void Update()
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.deltaTime / 0.3f);
            transform.localPosition = basePos + Vector3.up * (0.12f * Mathf.Sin(t * Mathf.PI) * (1f - t));
        }
    }

    /// <summary>Un volume de métal liquide (cube émissif) dont on règle le niveau.</summary>
    public class GFLiquid
    {
        public readonly Transform t;
        public readonly Material mat;
        readonly Vector3 bottom;
        readonly float w, maxH, d;
        float level, shown;
        float wobble = 1.2f;

        public GFLiquid(Transform parent, Vector3 bottomCenterLocal, float width, float maxHeight, float depth, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(g.GetComponent<Collider>());
            g.name = "Liquid";
            g.AddComponent<GFMatter>();
            t = g.transform;
            t.SetParent(parent, false);
            Renderer r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mat = m;
            bottom = bottomCenterLocal;
            w = width; maxH = maxHeight; d = depth;
            g.SetActive(false);
        }

        public float Level { get { return level; } }
        public float ShownHeight { get { return shown * maxH; } }
        public float MaxHeight { get { return maxH; } }

        public void SetLevel(float k) { level = Mathf.Clamp01(k); }
        public void Snap(float k) { level = shown = Mathf.Clamp01(k); Apply(); }
        public void SetWobble(float degrees) { wobble = degrees; }

        public void Tick(float dt)
        {
            shown = Mathf.Lerp(shown, level, 1f - Mathf.Exp(-10f * dt));
            Apply();
        }

        void Apply()
        {
            bool vis = shown > 0.004f;
            if (t.gameObject.activeSelf != vis) t.gameObject.SetActive(vis);
            if (!vis) return;
            float h = Mathf.Max(0.002f, shown * maxH);
            t.localScale = new Vector3(w, h, d);
            t.localPosition = bottom + Vector3.up * (h * 0.5f);
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 6.3f + bottom.x) * wobble);
        }
    }

    /// <summary>Filet de métal en fusion : la tête tombe, la queue suit quand on coupe le flux.</summary>
    public class GFStream
    {
        readonly Transform t;
        readonly Light light;
        readonly GFHalo halo;
        public float width;   // réglable : les améliorations élargissent la coulée
        bool on;
        Vector3 src;
        float targetY, headY, tailY;

        public GFStream(Transform parent, Material mat, float width)
        {
            GameObject g = GFArt.NewShape(PrimitiveType.Cylinder, "Molten Stream");
            g.AddComponent<GFMatter>();
            t = g.transform;
            t.SetParent(parent, false);
            Renderer r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.SetActive(false);

            GameObject lg = new GameObject("Stream Light");
            lg.transform.SetParent(parent, false);
            light = lg.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.15f);
            light.range = 3.2f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            halo = new GFHalo(parent, new Color(1f, 0.55f, 0.15f, 0.6f));
            this.width = width;
        }

        public bool On { get { return on; } }
        public bool Visible { get { return t.gameObject.activeSelf; } }

        public void Begin(Vector3 source, float target)
        {
            on = true;
            src = source;
            targetY = target;
            headY = tailY = source.y;
            t.gameObject.SetActive(true);
        }

        public void SetSource(Vector3 s) { src = s; }
        public void SetTarget(float y) { targetY = y; }
        public void End() { on = false; }

        public void Tick(float dt)
        {
            if (!t.gameObject.activeSelf)
            {
                light.intensity = Mathf.MoveTowards(light.intensity, 0f, dt * 20f);
                halo.Set(new Vector3(src.x, targetY + 0.1f, 0f), 1.9f, light.intensity / 3f);
                return;
            }
            headY = Mathf.Max(targetY, headY - 16f * dt);
            if (on) tailY = src.y;
            else tailY -= 16f * dt;
            if (!on && tailY <= targetY + 0.01f)
            {
                t.gameObject.SetActive(false);
                return;
            }
            float top = tailY;
            float bottom = Mathf.Min(headY, top - 0.01f);
            float len = Mathf.Max(0.01f, top - bottom);
            float wob = 1f + 0.18f * Mathf.Sin(Time.time * 37f) + 0.1f * Mathf.Sin(Time.time * 23f + 1.3f);
            float w = width * wob * (on ? 1f : 0.7f);
            t.position = new Vector3(src.x, (top + bottom) * 0.5f, src.z);
            t.rotation = Quaternion.identity;
            t.localScale = new Vector3(w, len * 0.5f, w);

            bool landed = headY <= targetY + 0.01f;
            if (landed && on && GFFx.I != null && Random.value < 0.6f)
                GFFx.I.Splash(new Vector3(src.x, targetY, src.z), 1);

            light.transform.position = new Vector3(src.x, targetY + 0.35f, -0.9f);
            light.intensity = Mathf.MoveTowards(light.intensity, on ? 3f : 0f, dt * 12f);
            halo.Set(new Vector3(src.x, targetY + 0.1f, 0f), 1.9f, light.intensity / 3f);
        }
    }
}
