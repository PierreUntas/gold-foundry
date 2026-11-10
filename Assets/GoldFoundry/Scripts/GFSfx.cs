using System.Collections.Generic;
using UnityEngine;

namespace GoldFoundry
{
    /// <summary>
    /// Tous les sons sont synthétisés au démarrage : aucun fichier audio.
    /// "Ore → clack, Piston → thump, Molten → glou glou, Bars → clang, Coins → ting."
    /// </summary>
    public class GFSfx : MonoBehaviour
    {
        public static GFSfx I;
        public bool Muted;

        const int SR = 44100;
        AudioSource[] pool;
        int next;
        AudioSource rumbleSrc, pourSrc;
        float rumbleTarget, pourTarget;
        readonly System.Random rng = new System.Random(1234);
        readonly Dictionary<AudioClip, float> lastPlay = new Dictionary<AudioClip, float>();
        float clackWindow, coinWindow;
        int clackCount, coinCount;

        public AudioClip Clack, Thump, Whoosh, Sizzle, Blub, Clang, Ting, Chime, Click, Hiss;

        float N() { return (float)(rng.NextDouble() * 2.0 - 1.0); }

        void Awake()
        {
            I = this;
            pool = new AudioSource[20];
            for (int i = 0; i < pool.Length; i++)
            {
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                pool[i] = s;
            }
            BuildClips();
            rumbleSrc = Loop(MakeRumble());
            pourSrc = Loop(MakePour());
        }

        AudioSource Loop(AudioClip c)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.clip = c;
            s.loop = true;
            s.volume = 0f;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.Play();
            return s;
        }

        // ------------------------------------------------------------------ lecture

        public void Play(AudioClip c, float vol, float pitch)
        {
            if (Muted || c == null || vol <= 0f) return;
            AudioSource s = pool[next];
            next = (next + 1) % pool.Length;
            s.pitch = pitch;
            s.PlayOneShot(c, vol);
        }

        public void PlayVar(AudioClip c, float vol, float pitchVar)
        {
            Play(c, vol, 1f + Random.Range(-pitchVar, pitchVar));
        }

        /// <summary>Joue au plus une fois par intervalle (évite le mur de son quand 15 minerais fondent ensemble).</summary>
        public void PlayLimited(AudioClip c, float vol, float pitchVar, float minInterval)
        {
            float last;
            if (lastPlay.TryGetValue(c, out last) && Time.time - last < minInterval) return;
            lastPlay[c] = Time.time;
            PlayVar(c, vol, pitchVar);
        }

        /// <summary>Chocs de minerai, limités à 3 par fenêtre de 45 ms.</summary>
        public void OreHit(float strength)
        {
            if (Time.time > clackWindow)
            {
                clackWindow = Time.time + 0.045f;
                clackCount = 0;
            }
            if (clackCount >= 3) return;
            clackCount++;
            Play(Clack, Mathf.Clamp(strength, 0.08f, 1f) * 0.55f, Random.Range(0.75f, 1.35f));
        }

        /// <summary>Pièces qui rebondissent : "ting ting ting", limitées à 4 par fenêtre de 50 ms.</summary>
        public void CoinHit(float strength)
        {
            if (Time.time > coinWindow)
            {
                coinWindow = Time.time + 0.05f;
                coinCount = 0;
            }
            if (coinCount >= 4) return;
            coinCount++;
            Play(Ting, Mathf.Clamp(strength, 0.1f, 1f) * 0.3f, Random.Range(1.0f, 1.6f));
        }

        public void SetRumble(float v) { rumbleTarget = v; }
        public void SetPour(float v) { pourTarget = v; }

        void Update()
        {
            float dt = Time.deltaTime;
            rumbleSrc.volume = Mathf.MoveTowards(rumbleSrc.volume, Muted ? 0f : rumbleTarget, dt * 1.5f);
            pourSrc.volume = Mathf.MoveTowards(pourSrc.volume, Muted ? 0f : pourTarget, dt * 4f);
        }

        // ------------------------------------------------------------------ synthèse

        void BuildClips()
        {
            float TWO_PI = Mathf.PI * 2f;

            // Clack : petit choc de caillou
            {
                float[] d = Buf(0.09f); float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / SR;
                    lp += (N() - lp) * 0.35f;
                    d[i] = (lp * 0.7f + Mathf.Sin(TWO_PI * 1900f * t) * 0.3f) * Mathf.Exp(-t * 65f)
                         + Mathf.Sin(TWO_PI * 260f * t) * Mathf.Exp(-t * 45f) * 0.45f;
                }
                Clack = Clip("clack", d, 0.9f);
            }
            // Thump : coup de piston grave, avec chute de hauteur
            {
                float[] d = Buf(0.6f); float ph = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / SR;
                    float f = 42f + 110f * Mathf.Exp(-t * 28f);
                    ph += TWO_PI * f / SR;
                    d[i] = Mathf.Sin(ph) * Mathf.Exp(-t * 6f)
                         + N() * Mathf.Exp(-t * 55f) * 0.45f
                         + Mathf.Sin(TWO_PI * 900f * t) * Mathf.Exp(-t * 60f) * 0.2f;
                }
                Thump = Clip("thump", d, 1f);
            }
            // Whoosh : souffle
            {
                float dur = 0.32f; float[] d = Buf(dur); float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / SR; float k = t / dur;
                    float a = 0.04f + 0.25f * Mathf.Sin(Mathf.PI * k);
                    lp += (N() - lp) * a;
                    d[i] = lp * Mathf.Pow(Mathf.Sin(Mathf.PI * k), 1.5f);
                }
                Whoosh = Clip("whoosh", d, 0.6f);
            }
            // Sizzle : le minerai qui fond
            {
                float[] d = Buf(0.7f); float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / SR;
                    float n = N(); lp += (n - lp) * 0.2f;
                    float crackle = rng.NextDouble() < 0.002 ? N() * 3f : 0f;
                    d[i] = ((n - lp) + crackle) * (1f - Mathf.Exp(-t * 40f)) * Mathf.Exp(-t * 4.5f);
                }
                Sizzle = Clip("sizzle", d, 0.5f);
            }
            // Blub : bulle de métal en fusion (glou)
            {
                float dur = 0.16f; float[] d = Buf(dur); float ph = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / SR; float k = t / dur;
                    float f = 240f * (1f + 2.5f * k);
                    ph += TWO_PI * f / SR;
                    d[i] = Mathf.Sin(ph) * Mathf.Sin(Mathf.PI * k) * Mathf.Exp(-t * 8f);
                }
                Blub = Clip("blub", d, 0.7f);
            }
            // Clang : lingot qui tombe (partiels inharmoniques)
            {
                float[] d = Buf(1.3f);
                float[] fr = { 380f, 1047f, 1601f, 2263f, 3089f };
                float[] am = { 1f, 0.6f, 0.45f, 0.3f, 0.2f };
                float[] dc = { 3f, 4f, 5.5f, 7f, 9f };
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / SR; float s = 0f;
                    for (int p = 0; p < fr.Length; p++) s += Mathf.Sin(TWO_PI * fr[p] * t) * am[p] * Mathf.Exp(-t * dc[p]);
                    d[i] = s + N() * Mathf.Exp(-t * 80f) * 0.6f;
                }
                Clang = Clip("clang", d, 0.75f);
            }
            // Ting : lingot rangé dans le coffre
            {
                float[] d = Buf(0.7f);
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / SR;
                    d[i] = Mathf.Sin(TWO_PI * 2637f * t) * Mathf.Exp(-t * 8f)
                         + Mathf.Sin(TWO_PI * 5274f * t) * Mathf.Exp(-t * 12f) * 0.45f
                         + Mathf.Sin(TWO_PI * 3951f * t) * Mathf.Exp(-t * 10f) * 0.3f;
                }
                Ting = Clip("ting", d, 0.55f);
            }
            // Chime : coffre complet (arpège)
            {
                float[] d = Buf(2.2f);
                float[] fr = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / SR; float s = 0f;
                    for (int p = 0; p < fr.Length; p++)
                    {
                        float tt = t - p * 0.09f;
                        if (tt < 0f) continue;
                        s += (Mathf.Sin(TWO_PI * fr[p] * tt) + 0.3f * Mathf.Sin(TWO_PI * fr[p] * 2f * tt)) * Mathf.Exp(-tt * 2.2f);
                    }
                    d[i] = s;
                }
                Chime = Clip("chime", d, 0.8f);
            }
            // Click : retour d'interface
            {
                float[] d = Buf(0.03f);
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / SR;
                    d[i] = Mathf.Sin(TWO_PI * 1400f * t) * Mathf.Exp(-t * 200f);
                }
                Click = Clip("click", d, 0.4f);
            }
            // Hiss : vapeur du moule qui refroidit
            {
                float dur = 0.9f; float[] d = Buf(dur); float lp = 0f;
                for (int i = 0; i < d.Length; i++)
                {
                    float t = (float)i / SR;
                    float n = N(); lp += (n - lp) * 0.3f;
                    d[i] = (n - lp) * (1f - Mathf.Exp(-t * 12f)) * Mathf.Exp(-t * 3.5f);
                }
                Hiss = Clip("hiss", d, 0.35f);
            }
        }

        AudioClip MakeRumble()
        {
            int n = (int)(3f * SR), k = (int)(0.25f * SR);
            float[] b = new float[n + k];
            float brown = 0f, lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / SR;
                brown = (brown + N() * 0.02f) * 0.997f;
                lp += (brown - lp) * 0.08f;
                b[i] = lp * 6f + Mathf.Sin(Mathf.PI * 2f * 38f * t) * 0.15f;
            }
            return LoopClip("rumble", b, n, k, 0.8f);
        }

        AudioClip MakePour()
        {
            int n = (int)(2f * SR), k = (int)(0.2f * SR);
            float[] b = new float[n + k];
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                lp += (N() - lp) * 0.06f;
                b[i] = lp * 0.8f;
            }
            // bulles aléatoires : "glou glou"
            int pos = 0;
            while (pos < b.Length)
            {
                int len = (int)(0.07f * SR);
                float f0 = 280f + (float)rng.NextDouble() * 420f, ph = 0f;
                for (int j = 0; j < len && pos + j < b.Length; j++)
                {
                    float k2 = (float)j / len;
                    ph += Mathf.PI * 2f * f0 * (1f + 1.8f * k2) / SR;
                    b[pos + j] += Mathf.Sin(ph) * Mathf.Sin(Mathf.PI * k2) * 0.5f;
                }
                pos += (int)((0.05f + rng.NextDouble() * 0.12f) * SR);
            }
            return LoopClip("pour", b, n, k, 0.7f);
        }

        float[] Buf(float seconds) { return new float[Mathf.Max(1, (int)(seconds * SR))]; }

        AudioClip Clip(string name, float[] d, float peak)
        {
            Normalize(d, peak);
            int fade = Mathf.Min(d.Length, 200);
            for (int i = 0; i < fade; i++) d[d.Length - 1 - i] *= (float)i / fade;
            AudioClip c = AudioClip.Create(name, d.Length, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        AudioClip LoopClip(string name, float[] b, int n, int k, float peak)
        {
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (i < k)
                {
                    float w = (float)i / k;
                    d[i] = b[i] * w + b[n + i] * (1f - w);
                }
                else d[i] = b[i];
            }
            Normalize(d, peak);
            AudioClip c = AudioClip.Create(name, n, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 0.0001f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            float s = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= s;
        }
    }
}
