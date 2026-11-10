using UnityEngine;

namespace GoldFoundry
{
    /// <summary>Particules (étincelles, vapeur, éclaboussures, reflets) et tremblement de caméra.</summary>
    public class GFFx : MonoBehaviour
    {
        public static GFFx I;

        ParticleSystem sparks, steam, splash, glints;
        Camera cam;
        Vector3 camBase;
        float shakeAmp;

        public void Init(Camera c)
        {
            I = this;
            cam = c;
            camBase = c.transform.position;
            sparks = MakePS("Sparks", new Color(1f, 0.62f, 0.2f, 1f), 1.2f, 600);
            splash = MakePS("Splash", new Color(1f, 0.72f, 0.25f, 1f), 1.6f, 500);
            glints = MakePS("Glints", new Color(1f, 0.93f, 0.6f, 1f), -0.05f, 300);
            steam = MakePS("Steam", new Color(0.7f, 0.7f, 0.75f, 0.35f), -0.12f, 300);
        }

        ParticleSystem MakePS(string name, Color color, float gravity, int max)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;

            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = false;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = false;

            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = GFMat.Glow(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
            return ps;
        }

        void Emit(ParticleSystem ps, Vector3 p, int n, float speed, Vector3 bias, float sizeMin, float sizeMax,
                  float lifeMin, float lifeMax, float spread)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                ep.position = p + Random.insideUnitSphere * spread;
                ep.velocity = Random.insideUnitSphere * speed + bias;
                ep.startSize = Random.Range(sizeMin, sizeMax);
                ep.startLifetime = Random.Range(lifeMin, lifeMax);
                ep.startColor = Color.white;
                ps.Emit(ep, 1);
            }
        }

        public void Sparks(Vector3 p, int n, float speed)
        {
            Emit(sparks, p, n, speed, Vector3.up * speed * 0.8f, 0.06f, 0.16f, 0.35f, 0.9f, 0.15f);
        }

        public void Splash(Vector3 p, int n)
        {
            Emit(splash, p, n, 1.4f, new Vector3(0f, 1.6f, -0.3f), 0.05f, 0.13f, 0.2f, 0.45f, 0.06f);
        }

        public void Glints(Vector3 p, int n)
        {
            Emit(glints, p, n, 1.2f, Vector3.up * 0.8f, 0.08f, 0.2f, 0.4f, 0.9f, 0.25f);
        }

        public void Steam(Vector3 p, int n)
        {
            Emit(steam, p, n, 0.25f, new Vector3(0f, 0.9f, -0.2f), 0.25f, 0.55f, 0.8f, 1.6f, 0.2f);
        }

        public void Shake(float amp)
        {
            shakeAmp = Mathf.Max(shakeAmp, amp);
        }

        void LateUpdate()
        {
            if (cam == null) return;
            shakeAmp = Mathf.MoveTowards(shakeAmp, 0f, Time.deltaTime * 1.2f);
            float t = Time.time * 32f;
            Vector3 off = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * 2f * shakeAmp;
            cam.transform.position = camBase + off;
        }
    }
}
