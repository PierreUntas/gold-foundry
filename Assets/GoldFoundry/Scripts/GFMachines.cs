using System.Collections.Generic;
using UnityEngine;

namespace GoldFoundry
{
    // =====================================================================================
    // PISTON — accumulation → tension (le tapis s'arrête) → recul → coup → la pile s'effondre
    // =====================================================================================
    public class GFPiston : MonoBehaviour
    {
        public enum State { Idle, Anticipate, Windup, Ram, Hold, Retract }

        public State state = State.Idle;
        public float retractX = 3.1f, extendX = -1.25f, y = 4.82f;
        public float idleTime;
        public Material faceMat;
        public int level;   // amélioration « Piston » : pousse plus vite, frappe plus fort

        Rigidbody rb;
        float t, x, anticipateDur;
        const float WindupDist = 0.3f, WindupDur = 0.22f, RamDur = 0.55f, HoldDur = 0.15f, RetractDur = 0.9f;

        public bool Busy { get { return state != State.Idle; } }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            x = retractX;
        }

        public void Fire(bool fromTap)
        {
            if (state != State.Idle) return;
            state = State.Anticipate;
            t = 0f;
            anticipateDur = fromTap ? 0.2f : Mathf.Max(0.3f, 0.8f - 0.1f * level);
            GFFactory.I.Belt.Running = false;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            t += dt;
            switch (state)
            {
                case State.Idle:
                    x = retractX;
                    idleTime += dt;
                    break;

                case State.Anticipate:
                    x = retractX + Mathf.Sin(t * 70f) * 0.012f;
                    if (t >= anticipateDur) { state = State.Windup; t = 0f; }
                    break;

                case State.Windup:
                {
                    float k = Mathf.Clamp01(t / WindupDur);
                    x = retractX + WindupDist * (1f - (1f - k) * (1f - k));
                    if (k >= 1f)
                    {
                        state = State.Ram; t = 0f;
                        GFSfx.I.PlayVar(GFSfx.I.Whoosh, 0.6f, 0.1f);
                    }
                    break;
                }

                case State.Ram:
                {
                    float k = Mathf.Clamp01(t / (RamDur * (1f - 0.08f * level)));
                    x = Mathf.Lerp(retractX + WindupDist, extendX, Mathf.Pow(k, 1.6f));
                    if (k >= 1f) { state = State.Hold; t = 0f; OnSlam(); }
                    break;
                }

                case State.Hold:
                    x = extendX;
                    if (t >= HoldDur) { state = State.Retract; t = 0f; }
                    break;

                case State.Retract:
                {
                    float k = Mathf.Clamp01(t / (RetractDur * (1f - 0.09f * level)));
                    x = Mathf.Lerp(extendX, retractX, k * k * (3f - 2f * k));
                    if (k >= 1f)
                    {
                        state = State.Idle; t = 0f; idleTime = 0f;
                        GFFactory.I.Belt.Running = true;
                    }
                    break;
                }
            }
            rb.MovePosition(new Vector3(x, y, 0f));

            if (faceMat != null)
            {
                float glow = state == State.Anticipate || state == State.Windup ? 0.6f + 0.4f * Mathf.Sin(t * 40f) : 0f;
                GFMat.SetEmission(faceMat, new Color(1f, 0.7f, 0.1f) * glow);
            }
        }

        void OnSlam()
        {
            GFSfx.I.Play(GFSfx.I.Thump, 1f, 1f - 0.05f * level + Random.Range(-0.04f, 0.04f));   // plus grave à chaque niveau
            if (level >= 3) GFSfx.I.PlayVar(GFSfx.I.Clang, 0.25f + 0.05f * level, 0.1f);
            GFFx.I.Shake(0.22f + 0.05f * level);
            GFHaptics.Impact(1);
            GFFx.I.Sparks(new Vector3(extendX - 0.3f, y - 0.4f, -0.5f), 10 + 6 * level, 2.5f + 0.3f * level);
        }
    }

    // =====================================================================================
    // FOUR — le minerai fond, le métal s'accumule, puis coule dans le réservoir
    // =====================================================================================
    public class GFFurnace : MonoBehaviour
    {
        public float x0, x1, y0, top;
        public float meltTop = 1.15f;
        public float haloCenterX = float.NaN;   // le minerai commence à fondre quand il apparaît dans la fenêtre
        public float molten, heat = 0.35f;
        public int level;   // amélioration « Four » : plus chaud, plus lumineux, fond plus vite
        public Material wallMat, glowMat, windowMat;
        public Light glowLight;
        public Vector3 spoutPos;
        [System.NonSerialized] public GFLiquid pool;
        [System.NonSerialized] public GFStream stream;
        public GameObject troughMolten;
        [System.NonSerialized] public GFHalo halo;
        public Material[] indicatorMats;

        float stokeTimer, pourLeft;
        bool pouring;

        public bool Pouring { get { return pouring; } }
        public float MeltDuration { get { return (stokeTimer > 0f ? 0.35f : 0.8f) * (1f - 0.11f * level); } }

        public bool Contains(Vector3 p)
        {
            return p.x > x0 + 0.05f && p.x < x1 - 0.05f && p.y < Mathf.Min(top - 0.15f, meltTop) && p.y > y0 - 0.5f;
        }

        public void AddMolten(float u)
        {
            molten += u;
            heat = Mathf.Min(1.4f, heat + 0.06f);
        }

        /// <summary>Micro-geste : attiser le four (fonte plus rapide, gerbe d'étincelles) et couler si possible.</summary>
        public void Stoke()
        {
            stokeTimer = 3f;
            heat = Mathf.Min(1.6f, heat + 0.45f);
            GFSfx.I.PlayVar(GFSfx.I.Whoosh, 0.7f, 0.15f);
            GFSfx.I.PlayLimited(GFSfx.I.Sizzle, 0.5f, 0.2f, 0.1f);
            GFFx.I.Sparks(new Vector3((x0 + x1) * 0.5f, top - 0.3f, -0.4f), 26, 3f);
            TryPour(true);
        }

        public void TryPour(bool force)
        {
            if (pouring) return;
            GFTank T = GFFactory.I.Tank;
            float space = T.Capacity - T.Units;
            if (space < 1f || molten < 1f) return;
            // accumulation → relâche : on attend d'avoir une vraie coulée à verser
            if (!force && molten < Mathf.Min(space, T.Batch)) return;
            pouring = true;
            pourLeft = Mathf.Min(space, molten);
            stream.Begin(spoutPos, T.SurfaceY);
            GFSfx.I.PlayVar(GFSfx.I.Blub, 0.7f, 0.2f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            stokeTimer -= dt;

            float targetHeat = 0.35f + 0.12f * level + Mathf.Min(molten, 14f) / 22f + (stokeTimer > 0f ? 0.4f : 0f);
            heat = Mathf.MoveTowards(heat, targetHeat, dt * 0.5f);
            float flick = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 6f, 0.2f);

            if (glowLight != null) glowLight.intensity = (1.2f + heat * 5f) * flick;
            if (wallMat != null) GFMat.SetEmission(wallMat, new Color(1f, 0.32f, 0.06f) * heat * 0.25f * flick);
            if (glowMat != null) GFMat.SetEmission(glowMat, new Color(1f, 0.36f, 0.04f) * Mathf.Clamp01(0.45f + heat * 0.5f) * flick);
            if (windowMat != null) GFMat.SetEmission(windowMat, new Color(0.9f, 0.34f, 0.02f) * Mathf.Clamp01(0.5f + heat * 0.45f) * flick);
            if (indicatorMats != null)
                for (int i = 0; i < indicatorMats.Length; i++)
                {
                    bool on = heat > 0.4f + i * 0.25f;
                    GFMat.SetEmission(indicatorMats[i], on ? new Color(1f, 0.15f, 0.1f) * 1.5f : new Color(0.15f, 0.02f, 0.02f));
                }

            pool.SetLevel(Mathf.Clamp01(molten / 18f));
            pool.Tick(dt);
            if (halo != null)
                halo.Set(new Vector3(float.IsNaN(haloCenterX) ? (x0 + x1) * 0.5f : haloCenterX, y0 + 0.5f, 0f), float.IsNaN(haloCenterX) ? 4.4f : 2.6f, (0.3f + heat * 0.4f) * flick);

            if (pouring)
            {
                GFTank T = GFFactory.I.Tank;
                float r = Mathf.Min(8f * T.Flow * dt, pourLeft, molten);
                molten -= r;
                pourLeft -= r;
                T.Receive(r);
                stream.SetTarget(T.SurfaceY);
                if (Random.value < dt * 6f) GFSfx.I.PlayVar(GFSfx.I.Blub, 0.35f, 0.3f);
                if (pourLeft <= 0.001f || molten <= 0.001f)
                {
                    pouring = false;
                    stream.End();
                }
            }
            if (troughMolten != null) troughMolten.SetActive(pouring);
            stream.Tick(dt);

            if (Random.value < dt * (0.6f + heat * 2f + level * 0.8f))
                GFFx.I.Sparks(new Vector3(Random.Range(x0 + 0.3f, x1 - 0.3f), y0 + 0.2f + pool.ShownHeight, -0.3f), 2, 1.2f);

            GFSfx.I.SetRumble(0.12f + heat * 0.22f);
        }
    }

    // =====================================================================================
    // RÉSERVOIR — cuve vitrée : l'or s'y accumule, puis la vanne verse moule après moule
    // =====================================================================================
    public class GFTank : MonoBehaviour
    {
        public float Capacity = 15f, PerMold = 3f, Units;
        public float Batch = 6f, Flow = 1f;   // amélioration « Réservoir » : coulées plus grosses et plus rapides
        public float bottomY, maxH;
        [System.NonSerialized] public GFLiquid liquid;
        [System.NonSerialized] public GFStream stream;
        public Vector3 nozzlePos;
        public Transform gaugeNeedle;
        public Material valveMat;
        [System.NonSerialized] public GFHalo halo;

        bool pouring, requested;
        float poured;

        public bool Pouring { get { return pouring; } }
        public float SurfaceY { get { return bottomY + maxH * Mathf.Clamp01(Units / Capacity); } }

        public void Receive(float u)
        {
            Units = Mathf.Min(Capacity, Units + u);
        }

        /// <summary>Micro-geste : ouvrir la vanne maintenant.</summary>
        public void Tap()
        {
            if (!pouring && Units >= PerMold - 0.01f) requested = true;
            GFFx.I.Splash(new Vector3(transform.position.x, SurfaceY, -0.2f), 8);
            GFSfx.I.PlayVar(GFSfx.I.Blub, 0.5f, 0.3f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            GFFactory F = GFFactory.I;
            GFCarousel C = F.Carousel;

            if (!pouring && Units >= PerMold - 0.01f && C.ReadyForPour && (F.Auto || requested))
            {
                requested = false;
                pouring = true;
                poured = 0f;
                stream.Begin(nozzlePos, C.PourMold.SurfaceY);
                GFSfx.I.PlayVar(GFSfx.I.Blub, 0.6f, 0.25f);
            }

            if (pouring)
            {
                GFMold m = C.PourMold;
                float r = Mathf.Min(9f * Flow * dt, Units, PerMold - poured);
                Units -= r;
                poured += r;
                m.Fill(poured / PerMold);
                stream.SetTarget(m.SurfaceY);
                if (poured >= PerMold - 0.001f || Units <= 0.001f)
                {
                    pouring = false;
                    stream.End();
                    C.OnMoldFilled();
                }
            }

            float level = Mathf.Clamp01(Units / Capacity);
            liquid.SetLevel(level);
            liquid.Tick(dt);
            stream.Tick(dt);
            if (gaugeNeedle != null)
                gaugeNeedle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(120f, -120f, level));
            if (valveMat != null)
                GFMat.SetEmission(valveMat, pouring ? new Color(0.2f, 1f, 0.2f) : new Color(0.02f, 0.12f, 0.02f));
            if (halo != null)
                halo.Set(new Vector3(transform.position.x, bottomY + liquid.ShownHeight * 0.5f, 0f), 2.6f, level * 0.7f);
            if (level > 0.05f && Random.value < dt * 4f)
                GFFx.I.Splash(new Vector3(transform.position.x + Random.Range(-0.5f, 0.5f), SurfaceY, -0.2f), 1);
        }
    }

    // =====================================================================================
    // CARROUSEL DE MOULES — sous la buse, puis un cran vers la gauche à chaque coulée
    // =====================================================================================
    public class GFCarousel : MonoBehaviour
    {
        enum S { Idle, Releasing, Shifting }

        public float[] slots;      // x des emplacements ; [0] = sous la buse (à droite)
        public GFMold[] molds;     // molds[i] occupe slots[i]
        public GFBelt belt;
        S s = S.Idle;
        float t;

        public GFMold PourMold { get { return molds[0]; } }
        public bool ReadyForPour { get { return s == S.Idle && molds[0].IsEmpty; } }

        public void OnMoldFilled()
        {
            molds[0].StartCooling();
            GFMold last = molds[molds.Length - 1];
            last.Recycle();                          // retourne son lingot (s'il en a un), puis disparaît
            s = last.HadMetal ? S.Releasing : S.Shifting;
            t = 0f;
            if (s == S.Shifting) belt.Running = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (s == S.Releasing)
            {
                if (molds[molds.Length - 1].FlipDone) { s = S.Shifting; t = 0f; belt.Running = true; }
            }
            else if (s == S.Shifting)
            {
                t = Mathf.Min(1f, t + dt / 0.55f);
                float k = t * t * (3f - 2f * t);
                for (int i = 0; i < molds.Length - 1; i++)
                {
                    Vector3 p = molds[i].transform.position;
                    p.x = Mathf.Lerp(slots[i], slots[i + 1], k);
                    molds[i].transform.position = p;
                }
                if (t >= 1f)
                {
                    GFMold last = molds[molds.Length - 1];
                    for (int i = molds.Length - 1; i > 0; i--) molds[i] = molds[i - 1];
                    molds[0] = last;
                    last.Respawn(slots[0]);
                    s = S.Idle;
                    belt.Running = false;
                    GFSfx.I.PlayVar(GFSfx.I.Clack, 0.35f, 0.1f);
                }
            }
        }
    }

    // =====================================================================================
    // MOULE — se remplit, refroidit en avançant (orange → or), se retourne, lâche le lingot
    // =====================================================================================
    public class GFMold : MonoBehaviour
    {
        enum S { Empty, Filled, Flip, Shrink, Hidden, Grow }

        [System.NonSerialized] public GFLiquid liquid;
        public Transform body;
        [System.NonSerialized] public GFHalo halo;
        public float liquidBottom = -0.13f;   // bas du métal, en local du moule

        S s = S.Empty;
        float t, coolT = -1f, hot;
        bool released, respawnPending, hadMetal;
        Vector3 respawnPos;

        // couleurs de la matière en cours (or, puis platine, puis orichalque : voir GFFactory.ApplyTier)
        public static Color HotBase = new Color(1f, 0.42f, 0.05f);
        public static Color HotEmit = new Color(0.85f, 0.3f, 0f);
        public static Color GoldBase = new Color(1f, 0.74f, 0.18f);
        public static Color GoldEmit = new Color(0.22f, 0.13f, 0f);

        public bool IsEmpty { get { return s == S.Empty; } }
        public bool HadMetal { get { return hadMetal; } }
        public bool FlipDone { get { return s == S.Shrink || s == S.Hidden || s == S.Grow || s == S.Empty; } }

        public float SurfaceY { get { return body.position.y + liquidBottom + liquid.ShownHeight; } }

        public void Fill(float k)
        {
            if (s == S.Empty)
            {
                GFMat.SetBase(liquid.mat, HotBase);
                GFMat.SetEmission(liquid.mat, HotEmit);
                s = S.Filled;
                coolT = -1f;
            }
            hot = 1f;
            liquid.SetLevel(k);
        }

        public void StartCooling()
        {
            coolT = 0f;
            GFSfx.I.PlayLimited(GFSfx.I.Hiss, 0.4f, 0.2f, 0.15f);
        }

        /// <summary>Appelé quand le moule quitte la ligne à gauche.</summary>
        public void Recycle()
        {
            hadMetal = s == S.Filled;
            t = 0f;
            released = false;
            s = hadMetal ? S.Flip : S.Shrink;
        }

        public void Respawn(float x)
        {
            respawnPos = new Vector3(x, transform.position.y, transform.position.z);
            respawnPending = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            switch (s)
            {
                case S.Filled:
                    if (coolT >= 0f)
                    {
                        coolT += dt;
                        float k = Mathf.Clamp01(coolT / 3f);
                        GFMat.SetBase(liquid.mat, Color.Lerp(HotBase, GoldBase, k));
                        GFMat.SetEmission(liquid.mat, Color.Lerp(HotEmit, GoldEmit, Mathf.Sqrt(k)));
                        hot = 1f - k;
                        if (hot > 0.2f && Random.value < dt * 4f) GFFx.I.Steam(body.position + Vector3.up * 0.2f, 1);
                    }
                    break;

                case S.Flip:
                {
                    float k = Mathf.Clamp01(t / 0.4f);
                    float a = 180f * k * k * (3f - 2f * k);
                    body.localRotation = Quaternion.Euler(0f, 0f, a);
                    if (!released && a > 120f)
                    {
                        released = true;
                        liquid.Snap(0f);
                        GFFactory.I.Bars.Spawn(body.position + Vector3.down * 0.15f, hot);
                    }
                    if (k >= 1f) { s = S.Shrink; t = 0f; }
                    break;
                }

                case S.Shrink:
                {
                    float k = Mathf.Clamp01(t / 0.22f);
                    transform.localScale = Vector3.one * (1f - k);
                    if (k >= 1f) { s = S.Hidden; t = 0f; }
                    break;
                }

                case S.Hidden:
                    if (respawnPending)
                    {
                        respawnPending = false;
                        transform.position = respawnPos;
                        body.localRotation = Quaternion.identity;
                        liquid.Snap(0f);
                        hot = 0f;
                        s = S.Grow;
                        t = 0f;
                    }
                    break;

                case S.Grow:
                {
                    float k = Mathf.Clamp01(t / 0.25f);
                    transform.localScale = Vector3.one * (k * (2f - k));
                    if (k >= 1f) { s = S.Empty; transform.localScale = Vector3.one; }
                    break;
                }
            }
            liquid.Tick(dt);
            if (halo != null)
            {
                bool vis = s == S.Filled || s == S.Flip;
                halo.Set(body.position, 1.6f, vis ? hot * liquid.Level * 0.8f : 0f);
            }
        }
    }

    // =====================================================================================
    // LINGOTS — fiers et disciplinés : ils "veulent défiler" en file jusqu'à la presse
    // =====================================================================================
    public class GFBars : MonoBehaviour
    {
        class Bar
        {
            public Transform t;
            public Material m;
            public int state; // 0 chute, 1 marche, 2 sous la presse, 3 écrasé
            public Vector3 vel;
            public float phase, glow, tt;
        }

        public float beltTop = -6.75f, startX = -2.2f, pressX = 1.75f, marchSpeed = 1.5f;
        public Material goldTemplate;
        public GFBelt belt;

        readonly List<Bar> bars = new List<Bar>();
        readonly Stack<Bar> pool = new Stack<Bar>();
        static readonly Vector3 BarSize = new Vector3(0.66f, 0.22f, 0.42f);
        const float HalfH = 0.11f;

        public void Spawn(Vector3 pos, float hot)
        {
            Bar b = pool.Count > 0 ? pool.Pop() : NewBar();
            b.t.gameObject.SetActive(true);
            b.t.position = pos;
            b.t.rotation = Quaternion.Euler(0f, 0f, 180f);
            b.t.localScale = BarSize;
            b.vel = new Vector3(0f, -1f, 0f);
            b.state = 0;
            b.glow = Mathf.Clamp01(hot);
            b.phase = 0f;
            bars.Add(b);
        }

        Bar NewBar()
        {
            GameObject g = new GameObject("Gold Bar");
            g.transform.SetParent(transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = GFArt.Ingot();
            Bar b = new Bar();
            b.t = g.transform;
            b.m = new Material(goldTemplate);
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = b.m;
            return b;
        }

        /// <summary>Lingot qui attend sous la presse (ou null).</summary>
        public bool HasBarUnderPress()
        {
            foreach (Bar b in bars) if (b.state == 2) return true;
            return false;
        }

        /// <summary>La presse frappe : le lingot sous la presse est écrasé.</summary>
        public Vector3 Crush()
        {
            foreach (Bar b in bars)
                if (b.state == 2)
                {
                    b.state = 3;
                    b.tt = 0f;
                    return b.t.position;
                }
            return new Vector3(pressX, beltTop, 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool anyMarching = false;

            for (int i = bars.Count - 1; i >= 0; i--)
            {
                Bar b = bars[i];
                b.glow = Mathf.MoveTowards(b.glow, 0f, dt * 0.3f);
                GFMat.SetEmission(b.m, Color.Lerp(GFMold.GoldEmit, GFMold.HotEmit, b.glow));
                Vector3 p = b.t.position;

                switch (b.state)
                {
                    case 0: // chute depuis le moule
                        b.vel.y -= 18f * dt;
                        p += b.vel * dt;
                        b.t.rotation = Quaternion.Slerp(b.t.rotation, Quaternion.identity, dt * 6f);
                        if (p.y <= beltTop + HalfH)
                        {
                            p.y = beltTop + HalfH;
                            b.state = 1;
                            b.t.rotation = Quaternion.identity;
                            GFSfx.I.PlayVar(GFSfx.I.Clang, 0.5f, 0.08f);
                            GFFx.I.Sparks(p, 5, 1.2f);
                            GFFx.I.Shake(0.03f);
                        }
                        break;

                    case 1: // défilé en file
                    {
                        bool blocked = false;
                        foreach (Bar o in bars)
                            if (o != b && (o.state == 1 || o.state == 2) && o.t.position.x > p.x && o.t.position.x - p.x < 0.8f)
                            { blocked = true; break; }
                        if (!blocked)
                        {
                            p.x = Mathf.Min(pressX, p.x + marchSpeed * dt);
                            b.phase += dt * 10f;
                            anyMarching = true;
                        }
                        p.y = beltTop + HalfH + Mathf.Abs(Mathf.Sin(b.phase)) * 0.05f;
                        if (p.x >= pressX - 0.001f) { p.y = beltTop + HalfH; b.state = 2; }
                        break;
                    }

                    case 3: // écrasé : s'aplatit et disparaît
                        b.tt += dt / 0.15f;
                        b.t.localScale = new Vector3(BarSize.x * (1f + 0.4f * b.tt), BarSize.y * Mathf.Max(0.05f, 1f - b.tt), BarSize.z * (1f + 0.2f * b.tt));
                        p.y = beltTop + b.t.localScale.y * 0.5f;
                        if (b.tt >= 1f)
                        {
                            b.t.gameObject.SetActive(false);
                            bars.RemoveAt(i);
                            pool.Push(b);
                            continue;
                        }
                        break;
                }
                b.t.position = p;
            }
            if (belt != null) belt.Running = anyMarching;
        }
    }

    // =====================================================================================
    // PRESSE À PIÈCES — un lingot sous la presse → BAM → une gerbe de pièces
    // =====================================================================================
    public class GFPress : MonoBehaviour
    {
        enum S { Idle, Wind, Slam, Hold, Rise }

        public Transform head;
        public float restY, slamY;   // hauteurs monde de la tête (repos / bas de course)
        public int coinsPerBar = 8;
        public int level;   // amélioration « Presse » : plus de pièces, frappe plus spectaculaire
        public Material lampMat;

        S s = S.Idle;
        float t;
        bool requested;

        public void Tap() { requested = true; }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            GFFactory F = GFFactory.I;
            Vector3 hp = head.localPosition;

            switch (s)
            {
                case S.Idle:
                    hp.y = restY;
                    if (F.Bars.HasBarUnderPress() && ((F.Auto && !F.HoldPress) || requested)) { s = S.Wind; t = 0f; }
                    requested = false;
                    break;
                case S.Wind:
                {
                    float k = Mathf.Clamp01(t / 0.18f);
                    hp.y = restY + 0.15f * Mathf.Sin(k * Mathf.PI * 0.5f);
                    if (k >= 1f) { s = S.Slam; t = 0f; }
                    break;
                }
                case S.Slam:
                {
                    float k = Mathf.Clamp01(t / 0.09f);
                    hp.y = Mathf.Lerp(restY + 0.15f, slamY, k * k);
                    if (k >= 1f) { s = S.Hold; t = 0f; Strike(); }
                    break;
                }
                case S.Hold:
                    hp.y = slamY;
                    if (t >= 0.12f) { s = S.Rise; t = 0f; }
                    break;
                case S.Rise:
                {
                    float k = Mathf.Clamp01(t / 0.45f);
                    hp.y = Mathf.Lerp(slamY, restY, k * k * (3f - 2f * k));
                    if (k >= 1f) { s = S.Idle; t = 0f; }
                    break;
                }
            }
            head.localPosition = hp;
            if (lampMat != null)
                GFMat.SetEmission(lampMat, s == S.Idle ? new Color(0.05f, 0.6f, 0.1f) : new Color(1f, 0.6f, 0.05f));
        }

        void Strike()
        {
            GFFactory F = GFFactory.I;
            Vector3 p = F.Bars.Crush();
            GFSfx.I.PlayVar(GFSfx.I.Thump, 0.9f, 0.08f);
            GFSfx.I.PlayVar(GFSfx.I.Clang, 0.45f, 0.1f);
            GFFx.I.Shake(0.15f + 0.03f * level);
            GFFx.I.Sparks(p + Vector3.up * 0.1f, 14 + 4 * level, 2.4f + 0.2f * level);
            GFFx.I.Glints(p + Vector3.up * 0.2f, 8 + 4 * level);
            F.Coins.Burst(p + Vector3.up * 0.3f, coinsPerBar);
            F.FlyCoins(p + Vector3.up * 0.3f, Mathf.Min(6, 3 + level));
            GFHaptics.Impact(0);
        }
    }

    /// <summary>Son des pièces qui rebondissent.</summary>
    public class GFCoin : MonoBehaviour
    {
        void OnCollisionEnter(Collision c)
        {
            if (GFSfx.I == null) return;
            float v = c.relativeVelocity.magnitude;
            if (v > 1.2f) GFSfx.I.CoinHit(Mathf.Clamp01((v - 1.2f) / 5f));
        }
    }

    // =====================================================================================
    // PIÈCES — joueuses, dispersées, bruyantes : elles "veulent rebondir" jusqu'au coffre
    // =====================================================================================
    public class GFCoins : MonoBehaviour
    {
        class Coin
        {
            public Rigidbody rb;
            public float born;
            public bool frozen, draining;
        }

        public const int CoinLayer = 8, VaultLayer = 9;
        public int max = 240;
        public float rimY = float.MaxValue;   // au-dessus du bord du coffre, une pièce posée s'efface (pas de montagne qui déborde)
        public int vaultCap = 170;
        public Material coinMat;
        public PhysicsMaterial physMat;

        readonly List<Coin> live = new List<Coin>();
        readonly Stack<Coin> pool = new Stack<Coin>();
        float drainT = -1f, celebrateT = -1f;

        public int Frozen { get; private set; }
        public float VaultFill { get { return Mathf.Clamp01(Frozen / (float)vaultCap); } }

        public void Burst(Vector3 p, int n)
        {
            for (int i = 0; i < n; i++)
            {
                Coin c = Take();
                if (c == null) return;
                Vector3 pos = p + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0f, 0.2f), Random.Range(-0.2f, 0.2f));
                c.rb.transform.position = pos;
                c.rb.position = pos;
                c.rb.rotation = Random.rotation;
                c.rb.transform.rotation = c.rb.rotation;
                c.rb.isKinematic = false;
                c.rb.gameObject.layer = CoinLayer;   // en vol : ne se cognent pas entre elles (sinon elles explosent à l'apparition)
                c.rb.gameObject.SetActive(true);
                c.rb.linearVelocity = new Vector3(Random.Range(-0.15f, 0.85f), Random.Range(2.4f, 3.4f), Random.Range(-0.15f, 0.15f));   // arc qui retombe dans le coffre
                c.rb.angularVelocity = Random.insideUnitSphere * 14f;
                c.born = Time.time;
                c.frozen = false;
                c.draining = false;
                c.rb.transform.localScale = new Vector3(0.36f, 0.025f, 0.36f);
                live.Add(c);
            }
            GFFactory.I.AddMoney(n * GFFactory.I.ValueMult);   // une pièce de platine vaut plus qu'une pièce d'or
            GFSfx.I.PlayLimited(GFSfx.I.Ting, 0.45f, 0.15f, 0.06f);
        }

        /// <summary>Mode vidéo : pose d'un coup un tas de pièces au fond du coffre (déjà immobiles).</summary>
        public void Prefill(int n, float x0, float x1, float floorY)
        {
            for (int i = 0; i < n; i++)
            {
                Coin c = Take();
                if (c == null) return;
                int layer = i / 30;
                Vector3 pos = new Vector3(Random.Range(x0 + 0.25f, x1 - 0.25f), floorY + 0.03f + layer * 0.045f + Random.Range(0f, 0.02f), Random.Range(-0.25f, 0.25f));
                Quaternion rot = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
                c.rb.transform.SetPositionAndRotation(pos, rot);
                c.rb.transform.localScale = new Vector3(0.36f, 0.025f, 0.36f);
                c.rb.isKinematic = true;
                c.rb.gameObject.layer = VaultLayer;   // posée : les suivantes s'empilent dessus
                c.rb.gameObject.SetActive(true);
                c.rb.position = pos;
                c.rb.rotation = rot;
                c.born = Time.time;
                c.frozen = true;
                c.draining = false;
                live.Add(c);
                Frozen++;
            }
        }

        Coin Take()
        {
            if (pool.Count > 0) return pool.Pop();
            if (live.Count < max) return NewCoin();
            // plus de pièce libre : on recycle la plus ancienne déjà posée
            for (int i = 0; i < live.Count; i++)
                if (live[i].frozen && !live[i].draining)
                {
                    Coin c = live[i];
                    live.RemoveAt(i);
                    Frozen--;
                    return c;
                }
            return null;
        }

        Coin NewCoin()
        {
            GameObject g = GFArt.NewShape(PrimitiveType.Cylinder, "Coin");
            g.layer = CoinLayer;
            g.transform.SetParent(transform, false);
            BoxCollider bc = g.AddComponent<BoxCollider>();
            bc.size = new Vector3(1f, 2f, 1f);   // le cylindre fait 2 de haut en local
            bc.sharedMaterial = physMat;
            g.GetComponent<Renderer>().sharedMaterial = coinMat;
            Rigidbody rb = g.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            g.AddComponent<GFCoin>();
            Coin c = new Coin();
            c.rb = rb;
            return c;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Coin c = live[i];
                Transform tr = c.rb.transform;
                if (c.draining)
                {
                    tr.localScale = Vector3.MoveTowards(tr.localScale, Vector3.zero, dt * 0.35f);
                    tr.position += Vector3.down * dt * 0.8f;
                    if (tr.localScale.x <= 0.01f) { Release(i); }
                    continue;
                }
                if (!c.frozen && Time.time - c.born > 1.2f && c.rb.linearVelocity.sqrMagnitude < 0.05f)
                {
                    c.rb.isKinematic = true;
                    if (tr.position.y > rimY)
                    {
                        c.draining = true;   // le coffre est plein à ras bord : celle-ci fond dans le tas
                        c.rb.GetComponent<Collider>().enabled = false;
                        continue;
                    }
                    // couchée à plat : le tas reste un lit de pièces au lieu d'un hérisson
                    tr.rotation = Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
                    c.rb.rotation = tr.rotation;
                    c.frozen = true;
                    c.rb.gameObject.layer = VaultLayer;
                    Frozen++;
                }
                if (tr.position.y < -13f || Mathf.Abs(tr.position.x) > 7f) Release(i);
            }

            if (celebrateT < 0f && drainT < 0f && Frozen >= vaultCap) Celebrate();
            if (celebrateT >= 0f)
            {
                celebrateT += dt;
                if (celebrateT > 1.6f) { celebrateT = -1f; Drain(); }
            }
            if (drainT >= 0f)
            {
                drainT += dt;
                if (drainT > 3f) drainT = -1f;
            }
        }

        void Release(int i)
        {
            Coin c = live[i];
            if (c.frozen) Frozen--;
            live.RemoveAt(i);
            c.rb.isKinematic = true;
            c.rb.GetComponent<Collider>().enabled = true;
            c.rb.gameObject.SetActive(false);
            pool.Push(c);
        }

        void Celebrate()
        {
            celebrateT = 0f;
            GFSfx.I.Play(GFSfx.I.Chime, 0.8f, 1f);
            GFFx.I.Shake(0.12f);
            GFHaptics.Impact(2);
            int n = 0;
            foreach (Coin c in live)
                if (c.frozen && (n++ % 6) == 0) GFFx.I.Glints(c.rb.transform.position, 3);
        }

        void Drain()
        {
            drainT = 0f;
            foreach (Coin c in live)
                if (c.frozen)
                {
                    c.draining = true;
                    c.rb.GetComponent<Collider>().enabled = false;
                }
            GFSfx.I.PlayVar(GFSfx.I.Whoosh, 0.5f, 0.1f);
        }
    }
}
