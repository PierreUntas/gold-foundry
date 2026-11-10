using System.Collections.Generic;
using UnityEngine;

namespace GoldFoundry
{
    /// <summary>Marque un rendu de "matière" (liquide, filet) : il n'est jamais masqué par un sprite.</summary>
    public class GFMatter : MonoBehaviour { }

    /// <summary>
    /// Direction artistique procédurale inspirée des images de la vision :
    /// tour en pierre, étagères de pierre claire, machines bleu acier rivetées, rayures jaune/noir.
    /// + le "crochet" vers de vraies images : Resources/Art/&lt;nom&gt;.png remplace la machine dessinée en code.
    /// </summary>
    public static class GFArt
    {
        static readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();
        static Mesh[] rocks;
        static Mesh ingot, quad;
        static Material spriteTemplate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            tex.Clear();
            rocks = null;
            ingot = null;
            quad = null;
            shapes.Clear();
            spriteTemplate = null;
        }

        // ------------------------------------------------------------------ palette

        public static readonly Color Navy = new Color(0.2f, 0.3f, 0.56f);
        public static readonly Color NavyDark = new Color(0.13f, 0.18f, 0.34f);
        public static readonly Color StoneLight = new Color(0.86f, 0.88f, 0.92f);
        public static readonly Color Pillar = new Color(0.36f, 0.41f, 0.54f);
        public static readonly Color StoneDark = new Color(0.17f, 0.22f, 0.36f);
        public static readonly Color Chrome = new Color(0.72f, 0.75f, 0.8f);
        public static readonly Color PipeGreen = new Color(0.3f, 0.78f, 0.22f);
        public static readonly Color OreBrown = new Color(0.46f, 0.31f, 0.18f);

        // ------------------------------------------------------------------ textures

        static Texture2D Get(string key, System.Func<Texture2D> make)
        {
            Texture2D t;
            if (!tex.TryGetValue(key, out t) || t == null)
            {
                t = make();
                tex[key] = t;
            }
            return t;
        }

        public static Texture2D Stone { get { return Get("stone", MakeStone); } }
        public static Texture2D Panel { get { return Get("panel", MakePanel); } }
        public static Texture2D Hazard { get { return Get("hazard", MakeHazard); } }
        public static Texture2D Chain { get { return Get("chain", MakeChain); } }
        public static Texture2D Grate { get { return Get("grate", MakeGrate); } }
        public static Texture2D CoinIcon { get { return Get("coin", MakeCoinIcon); } }
        public static Texture2D GaugeFace { get { return Get("gauge", MakeGauge); } }
        public static Texture2D RoundRect { get { return Get("round", MakeRoundRect); } }

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        static Texture2D NewTex(int w, int h, bool repeat)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = 2;
            return t;
        }

        /// <summary>Blocs de pierre décalés, avec joints et biseau (teinte = couleur du matériau).</summary>
        static Texture2D MakeStone()
        {
            const int S = 256, BW = 64, BH = 32;
            Texture2D t = NewTex(S, S, true);
            Color[] px = new Color[S * S];
            for (int y = 0; y < S; y++)
            {
                int row = y / BH;
                int off = (row % 2) * (BW / 2);
                for (int x = 0; x < S; x++)
                {
                    int xx = (x + off) % S;
                    int col = xx / BW;
                    int lx = xx % BW, ly = y % BH;
                    float v;
                    if (lx < 3 || ly < 3) v = 0.42f;
                    else
                    {
                        v = 0.8f + 0.18f * Hash(col, row) + (Hash(x, y) - 0.5f) * 0.07f;
                        if (ly > BH - 7) v += 0.12f;      // arête haute éclairée
                        if (lx < 8) v += 0.05f;           // arête gauche
                        if (ly < 8) v -= 0.12f;           // bas dans l'ombre
                        if (lx > BW - 6) v -= 0.06f;
                    }
                    px[y * S + x] = new Color(v, v, v, 1f);
                }
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Panneau de métal biseauté avec 4 rivets — appliqué sur chaque face des machines.</summary>
        static Texture2D MakePanel()
        {
            const int S = 128, B = 9;
            Texture2D t = NewTex(S, S, false);
            Color[] px = new Color[S * S];
            Vector2[] rivets = { new Vector2(17, 17), new Vector2(S - 17, 17), new Vector2(17, S - 17), new Vector2(S - 17, S - 17) };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float v = 0.78f + 0.14f * (y / (float)S);           // léger dégradé vertical
                    if (x < B || y > S - 1 - B) v = 1f;                 // biseau haut / gauche
                    if (x > S - 1 - B || y < B) v = 0.5f;               // biseau bas / droite
                    if (x < 2 || y < 2 || x > S - 3 || y > S - 3) v = 0.3f; // contour
                    foreach (Vector2 r in rivets)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), r);
                        if (d < 5f) v = d < 3f ? (y > r.y ? 1f : 0.85f) : 0.45f;
                    }
                    px[y * S + x] = new Color(v, v, v, 1f);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        static Texture2D MakeHazard()
        {
            const int S = 64;
            Texture2D t = NewTex(S, S, true);
            Color yellow = new Color(1f, 0.78f, 0.1f), black = new Color(0.12f, 0.12f, 0.14f);
            Color[] px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    px[y * S + x] = ((x + y) / 16) % 2 == 0 ? yellow : black;
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Chaîne de tapis roulant : maillons clairs sur fond sombre.</summary>
        static Texture2D MakeChain()
        {
            const int W = 64, H = 32;
            Texture2D t = NewTex(W, H, true);
            Color[] px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int lx = x % 16;
                    float v = 0.22f;
                    if (lx >= 2 && lx <= 13 && y >= 4 && y <= 27)
                        v = y > 22 ? 0.62f : 0.45f;
                    if ((lx == 0 || lx == 15) && (y == 8 || y == 23)) v = 0.75f;
                    px[y * W + x] = new Color(v, v, v, 1f);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Grille du four : barreaux sombres, jours blancs (sert aussi de masque d'émission).</summary>
        static Texture2D MakeGrate()
        {
            const int S = 64;
            Texture2D t = NewTex(S, S, true);
            Color[] px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    bool bar = (x % 16) < 4;
                    px[y * S + x] = bar ? new Color(0.08f, 0.08f, 0.1f, 1f) : Color.white;
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        static Texture2D MakeCoinIcon()
        {
            const int S = 64;
            Texture2D t = NewTex(S, S, false);
            Color[] px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - 31.5f, dy = y - 31.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = new Color(0f, 0f, 0f, 0f);
                    if (d < 30f)
                    {
                        float lit = Mathf.Clamp01(0.5f + (dy - dx) / 60f);
                        if (d > 25f) c = Color.Lerp(new Color(0.75f, 0.45f, 0.05f), new Color(0.95f, 0.65f, 0.15f), lit);
                        else if (d > 19f && d < 21.5f) c = new Color(0.85f, 0.55f, 0.1f);
                        else c = Color.Lerp(new Color(1f, 0.72f, 0.18f), new Color(1f, 0.9f, 0.45f), lit);
                        c.a = Mathf.Clamp01(30f - d);
                    }
                    px[y * S + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        static Texture2D MakeGauge()
        {
            const int S = 64;
            Texture2D t = NewTex(S, S, false);
            Color[] px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - 31.5f, dy = y - 31.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = new Color(0f, 0f, 0f, 0f);
                    if (d < 31f)
                    {
                        c = d > 26f ? new Color(0.25f, 0.27f, 0.32f) : new Color(0.95f, 0.95f, 0.92f);
                        float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        if (d > 19f && d < 25f && dy > -12f && Mathf.Abs(Mathf.Repeat(a + 7.5f, 30f) - 7.5f) < 2.5f)
                            c = new Color(0.2f, 0.2f, 0.22f);
                        c.a = Mathf.Clamp01(31f - d);
                    }
                    px[y * S + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        static Texture2D MakeRoundRect()
        {
            const int S = 64;
            const float R = 16f;
            Texture2D t = NewTex(S, S, false);
            Color[] px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, R, S - R), cy = Mathf.Clamp(y + 0.5f, R, S - R);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(R - d);
                    float edge = d > R - 3f ? 1f : 0.85f;
                    px[y * S + x] = new Color(edge, edge, edge, a);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        // ------------------------------------------------------------------ matériaux

        /// <summary>Matériau texturé et teinté, avec répétition (tiling) choisie.</summary>
        public static Material Textured(Texture2D t, Color tint, Vector2 tiling, float metallic, float smooth)
        {
            Material m = GFMat.Lit(tint, metallic, smooth);
            m.mainTexture = t;
            m.mainTextureScale = tiling;
            return m;
        }

        /// <summary>Pierre dont les blocs gardent une taille constante quelle que soit la taille de l'objet.</summary>
        public static Material StoneMat(Color tint, float width, float height)
        {
            // la texture contient 4 blocs × 8 rangées ; un bloc ≈ 0,9 × 0,42 unité
            return Textured(Stone, tint, new Vector2(width / 3.6f, height / 3.4f), 0f, 0.1f);
        }

        public static Material PanelMat(Color tint) { return Textured(Panel, tint, Vector2.one, 0.35f, 0.45f); }

        /// <summary>Dalle lisse et biseautée (étagères des maquettes), découpée en segments d'environ 1,4 unité.</summary>
        public static Material SlabMat(Color tint, float width) { return Textured(Panel, tint, new Vector2(Mathf.Max(1f, Mathf.Round(width / 1.4f)), 1f), 0.05f, 0.35f); }

        public static Material HazardMat(float width, float height)
        {
            return Textured(Hazard, Color.white, new Vector2(Mathf.Max(1f, width / 0.5f), Mathf.Max(1f, height / 0.5f)), 0.1f, 0.4f);
        }

        // ------------------------------------------------------------------ maillages

        /// <summary>Quelques variantes de roche low-poly (icosphère bosselée, facettes plates).</summary>
        public static Mesh Rock(int i)
        {
            if (rocks == null)
            {
                rocks = new Mesh[6];
                for (int k = 0; k < rocks.Length; k++) rocks[k] = MakeRock(k * 31 + 7);
            }
            return rocks[Mathf.Abs(i) % rocks.Length];
        }

        static Mesh MakeRock(int seed)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            List<Vector3> v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            int[] f =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;

            // une subdivision
            Dictionary<long, int> mid = new Dictionary<long, int>();
            List<int> f2 = new List<int>();
            for (int i = 0; i < f.Length; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Mid(v, mid, a, b), bc = Mid(v, mid, b, c), ca = Mid(v, mid, c, a);
                f2.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            // bosses
            for (int i = 0; i < v.Count; i++)
            {
                float r = 0.78f + 0.36f * Hash(i * 13 + seed, seed);
                Vector3 p = v[i] * r * 0.5f;
                p.y *= 0.82f;
                v[i] = p;
            }

            // facettes plates : sommets dupliqués par triangle, normales vers l'extérieur
            Vector3[] verts = new Vector3[f2.Count];
            int[] tris = new int[f2.Count];
            for (int i = 0; i < f2.Count; i += 3)
            {
                Vector3 a = v[f2[i]], b = v[f2[i + 1]], c = v[f2[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f) { Vector3 tmp = b; b = c; c = tmp; }
                verts[i] = a; verts[i + 1] = b; verts[i + 2] = c;
                tris[i] = i; tris[i + 1] = i + 1; tris[i + 2] = i + 2;
            }
            Mesh m = new Mesh();
            m.name = "Rock";
            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static int Mid(List<Vector3> v, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            int idx;
            if (cache.TryGetValue(key, out idx)) return idx;
            v.Add(((v[a] + v[b]) * 0.5f).normalized);
            idx = v.Count - 1;
            cache[key] = idx;
            return idx;
        }

        /// <summary>Lingot : prisme trapézoïdal (base large, dessus étroit), taille unitaire.</summary>
        public static Mesh Ingot()
        {
            if (ingot != null) return ingot;
            Vector3 b0 = new Vector3(-0.5f, -0.5f, -0.5f), b1 = new Vector3(0.5f, -0.5f, -0.5f),
                    b2 = new Vector3(0.5f, -0.5f, 0.5f), b3 = new Vector3(-0.5f, -0.5f, 0.5f);
            Vector3 t0 = new Vector3(-0.4f, 0.5f, -0.36f), t1 = new Vector3(0.4f, 0.5f, -0.36f),
                    t2 = new Vector3(0.4f, 0.5f, 0.36f), t3 = new Vector3(-0.4f, 0.5f, 0.36f);
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            Quad(verts, tris, t0, t1, b1, b0); // face avant
            Quad(verts, tris, t2, t3, b3, b2); // arrière
            Quad(verts, tris, t3, t2, t1, t0); // dessus
            Quad(verts, tris, b2, b3, b0, b1); // dessous
            Quad(verts, tris, t1, t2, b2, b1); // droite
            Quad(verts, tris, t3, t0, b0, b3); // gauche
            ingot = new Mesh();
            ingot.name = "Ingot";
            ingot.SetVertices(verts);
            ingot.SetTriangles(tris, 0);
            ingot.RecalculateNormals();
            ingot.RecalculateBounds();
            return ingot;
        }

        static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        static readonly Dictionary<PrimitiveType, Mesh> shapes = new Dictionary<PrimitiveType, Mesh>();

        /// <summary>
        /// Sphère, cylindre ou cube sans collision, à partir des modèles intégrés de Unity.
        /// (GameObject.CreatePrimitive ajoute un collider que le build iOS peut retirer : l'appel échoue alors sur iPhone.)
        /// </summary>
        public static GameObject NewShape(PrimitiveType type, string name)
        {
            Mesh m;
            if (!shapes.TryGetValue(type, out m) || m == null)
            {
                string file = type == PrimitiveType.Sphere ? "Sphere.fbx" : type == PrimitiveType.Cylinder ? "Cylinder.fbx" : "Cube.fbx";
                m = Resources.GetBuiltinResource<Mesh>(file);
                shapes[type] = m;
            }
            GameObject g = new GameObject(name);
            g.AddComponent<MeshFilter>().sharedMesh = m;
            g.AddComponent<MeshRenderer>();
            return g;
        }

        /// <summary>
        /// Rectangle d'affichage tourné vers la caméra, sans collision.
        /// (GameObject.CreatePrimitive(Quad) ajoute un MeshCollider, que le build iOS retire : l'appel échoue sur iPhone.)
        /// </summary>
        public static GameObject NewQuad(string name)
        {
            if (quad == null)
            {
                quad = new Mesh();
                quad.name = "GF Quad";
                quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
                quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                quad.triangles = new[] { 2, 3, 1, 2, 1, 0 };
                quad.RecalculateNormals();
                quad.RecalculateBounds();
            }
            GameObject g = new GameObject(name);
            g.AddComponent<MeshFilter>().sharedMesh = quad;
            g.AddComponent<MeshRenderer>();
            return g;
        }

        // ------------------------------------------------------------------ crochet vers de vraies images

        /// <summary>Charge Resources/Art/&lt;name&gt;.png s'il existe (null sinon).</summary>
        public static Texture2D Art(string name)
        {
            return Resources.Load<Texture2D>("Art/" + name);
        }

        /// <summary>
        /// Si Resources/Art/&lt;name&gt;.png existe : masque les formes dessinées en code sous <paramref name="root"/>
        /// (sauf la matière, marquée GFMatter) et pose l'image à la place, centrée sur <paramref name="center"/>
        /// (coordonnées monde), à la taille <paramref name="size"/> (unités monde).
        /// <paramref name="z"/> : devant (négatif, les zones transparentes de l'image laissent voir l'intérieur)
        /// ou derrière la matière (positif). Les collisions ne changent pas.
        /// </summary>
        public static bool Skin(string name, Transform root, Vector2 center, Vector2 size, float z)
        {
            Texture2D t = Art(name);
            if (t == null) return false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (r.GetComponent<GFMatter>() == null) r.enabled = false;

            if (spriteTemplate == null) spriteTemplate = new Material(Shader.Find("Sprites/Default"));
            GameObject q = NewQuad("Art: " + name);
            q.transform.SetParent(root, true);
            q.transform.position = new Vector3(center.x, center.y, z);
            q.transform.rotation = Quaternion.identity;
            Vector3 ls = root.lossyScale;
            q.transform.localScale = new Vector3(size.x / Mathf.Max(0.0001f, ls.x), size.y / Mathf.Max(0.0001f, ls.y), 1f);
            Material m = new Material(spriteTemplate);
            m.mainTexture = t;
            Renderer qr = q.GetComponent<Renderer>();
            qr.sharedMaterial = m;
            qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return true;
        }
    }
}
