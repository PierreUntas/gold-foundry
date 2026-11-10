using UnityEngine;

namespace GoldFoundry
{
    /// <summary>
    /// Matériaux et textures procédurales (pipeline de rendu intégré, comme Fronton / Une coupe).
    /// Resources/GF_Lit.mat référence le shader Standard avec l'émission activée,
    /// pour que le shader et sa variante "émissive" soient inclus dans les builds iPhone.
    /// </summary>
    public static class GFMat
    {
        static Material litTemplate, unlitTemplate;
        static Texture2D softDot;
        static bool init;

        // Remet les caches à zéro à chaque Play (utile si le "domain reload" est désactivé).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            init = false;
            litTemplate = unlitTemplate = null;
            softDot = null;
        }

        static void Init()
        {
            if (init) return;
            init = true;

            Material m = Resources.Load<Material>("GF_Lit");
            if (m != null) litTemplate = new Material(m);
            else
            {
                Shader sh = Shader.Find("Standard");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                litTemplate = new Material(sh);
            }

            // Sprites/Default est toujours inclus dans les builds et gère la transparence.
            unlitTemplate = new Material(Shader.Find("Sprites/Default"));
        }

        public static Material Lit(Color baseColor, float metallic, float smoothness, Color emission)
        {
            Init();
            Material m = new Material(litTemplate);
            SetBase(m, baseColor);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SetEmission(m, emission);
            return m;
        }

        public static Material Lit(Color baseColor, float metallic, float smoothness)
        {
            return Lit(baseColor, metallic, smoothness, Color.black);
        }

        /// <summary>Matériau non éclairé, transparent, avec le point doux (particules, halos).</summary>
        public static Material Glow(Color c)
        {
            Init();
            Material m = new Material(unlitTemplate);
            m.mainTexture = SoftDot();
            m.color = c;
            return m;
        }

        public static void SetBase(Material m, Color c)
        {
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }

        public static void SetEmission(Material m, Color c)
        {
            m.SetColor("_EmissionColor", c);
        }

        /// <summary>Point doux (halo) pour les particules et les lueurs.</summary>
        public static Texture2D SoftDot()
        {
            if (softDot != null) return softDot;
            const int S = 64;
            softDot = new Texture2D(S, S, TextureFormat.RGBA32, false);
            softDot.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S * 2f - 1f, dy = (y + 0.5f) / S * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            softDot.Apply();
            return softDot;
        }

        /// <summary>Bandes pour les tapis roulants (le défilement de l'offset donne le mouvement).</summary>
        public static Texture2D Stripes(Color a, Color b)
        {
            const int W = 32, H = 4;
            Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            for (int x = 0; x < W; x++)
            {
                Color c = x < W * 0.22f ? b : a;
                for (int y = 0; y < H; y++) t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }
    }

    /// <summary>
    /// Halo lumineux posé devant la scène : remplace le bloom (absent du pipeline intégré)
    /// pour faire "rayonner" le métal en fusion.
    /// </summary>
    public class GFHalo
    {
        readonly Transform t;
        readonly Material m;
        readonly Color col;

        public GFHalo(Transform parent, Color c)
        {
            GameObject g = GFArt.NewQuad("Halo");
            t = g.transform;
            t.SetParent(parent, false);
            col = c;
            m = GFMat.Glow(c);
            Renderer r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            g.SetActive(false);
        }

        /// <summary>Place le halo (coordonnées monde x/y), sa taille et son intensité (0..1).</summary>
        public void Set(Vector3 world, float size, float intensity)
        {
            bool vis = intensity > 0.01f;
            if (t.gameObject.activeSelf != vis) t.gameObject.SetActive(vis);
            if (!vis) return;
            t.position = new Vector3(world.x, world.y, -GFFactory.SLAB - 0.35f);
            t.rotation = Quaternion.identity;
            t.localScale = new Vector3(size, size, 1f);
            Color c = col;
            c.a = col.a * Mathf.Clamp01(intensity);
            m.color = c;
        }
    }
}
