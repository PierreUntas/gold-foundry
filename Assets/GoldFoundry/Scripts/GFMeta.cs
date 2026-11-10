using System.Collections.Generic;
using UnityEngine;

namespace GoldFoundry
{
    /// <summary>
    /// Autour de la partie : écran d'accueil, gains hors ligne (« pendant ton absence… »),
    /// pièces qui volent jusqu'au compteur.
    /// </summary>
    public partial class GFFactory
    {
        const float OfflineEfficiency = 0.5f;     // l'usine tourne à moitié régime quand on n'est pas là
        const double OfflineCapSeconds = 3 * 3600; // au plus 3 heures comptées
        const double OfflineMinSeconds = 60;

        bool titleOpen = true;
        float titleT;
        long offlineGain;
        double offlineSeconds;

        struct Fly { public Vector2 from, ctrl; public float t, delay; }
        readonly List<Fly> flies = new List<Fly>();

        GUIStyle titleStyle;

        /// <summary>Production estimée (pièces/s) d'après les améliorations — sert aux gains hors ligne.</summary>
        float EstimatedRate()
        {
            return oreRate / Tank.PerMold * 0.65f * Press.coinsPerBar * ValueMult;
        }

        void SaveClock()
        {
            PlayerPrefs.SetString("gf_time", System.DateTime.UtcNow.Ticks.ToString());
        }

        /// <summary>Calcule ce que l'usine a produit depuis la dernière sauvegarde (au lancement et au retour dans l'app).</summary>
        void CheckOffline()
        {
            long ticks;
            if (!long.TryParse(PlayerPrefs.GetString("gf_time", ""), out ticks)) return;
            double s = (System.DateTime.UtcNow.Ticks - ticks) / (double)System.TimeSpan.TicksPerSecond;
            if (s < OfflineMinSeconds) return;
            offlineSeconds = System.Math.Min(s, OfflineCapSeconds);
            offlineGain = (long)(offlineSeconds * EstimatedRate() * OfflineEfficiency);
            if (offlineGain < 1) offlineGain = 0;
        }

        void CollectOffline()
        {
            if (offlineGain <= 0) return;
            AddMoney((int)Mathf.Min(offlineGain, int.MaxValue));
            float u = UiScale();
            Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.47f);
            for (int i = 0; i < 14; i++) FlyFromGui(c + Random.insideUnitCircle * 90f * u, i * 0.05f);
            GFSfx.I.Play(GFSfx.I.Chime, 0.8f, 1.05f);
            GFSfx.I.PlayLimited(GFSfx.I.Ting, 0.5f, 0.15f, 0.05f);
            GFHaptics.Impact(2);
            offlineGain = 0;
            SaveGame();
        }

        // ------------------------------------------------------------ pièces volantes

        /// <summary>Quelques pièces partent d'un point de la scène vers le compteur.</summary>
        public void FlyCoins(Vector3 world, int n)
        {
            if (videoMode || cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(world);
            Vector2 g = new Vector2(sp.x, Screen.height - sp.y);
            for (int i = 0; i < n; i++) FlyFromGui(g + Random.insideUnitCircle * 30f * UiScale(), i * 0.06f);
        }

        void FlyFromGui(Vector2 from, float delay)
        {
            Fly f = new Fly();
            f.from = from;
            f.delay = delay;
            Vector2 to = MoneyIconCenter();
            f.ctrl = new Vector2(Mathf.Lerp(from.x, to.x, 0.2f) + Random.Range(-160f, 160f) * UiScale(), Mathf.Min(from.y, to.y) + Random.Range(-60f, 120f) * UiScale());
            flies.Add(f);
        }

        Vector2 MoneyIconCenter()
        {
            float u = UiScale();
            float top = Screen.height - Screen.safeArea.yMax + 40f * u;
            return new Vector2(36f * u + 48f * u, top + 46f * u);
        }

        static float UiScale() { return Mathf.Min(Screen.width / 1080f, Screen.height / 1920f); }

        void TickMeta(float dt)
        {
            titleT += dt;
            for (int i = flies.Count - 1; i >= 0; i--)
            {
                Fly f = flies[i];
                if (f.delay > 0f) f.delay -= dt;
                else f.t += dt / 0.55f;
                if (f.t >= 1f)
                {
                    flies.RemoveAt(i);
                    moneyPop = 0f;
                    GFSfx.I.PlayLimited(GFSfx.I.Ting, 0.18f, 0.2f, 0.04f);
                    continue;
                }
                flies[i] = f;
            }
        }

        /// <summary>Renvoie true si le toucher a été pris par l'écran d'accueil ou la fenêtre « pendant ton absence ».</summary>
        bool MetaInput()
        {
            if (titleOpen)
            {
                if (titleT < 0.4f) return true;   // évite qu'un toucher resté du lancement ferme l'accueil
                titleOpen = false;
                GFSfx.I.Play(GFSfx.I.Click, 0.7f, 1f);
                GFHaptics.Impact(0);
                hintTimer = InTutorial ? 0f : 7f;
                return true;
            }
            if (offlineGain > 0) { CollectOffline(); return true; }
            return false;
        }

        void DrawMeta(float u)
        {
            // pièces volantes (sous les fenêtres)
            float sz = 46f * u;
            Vector2 to = MoneyIconCenter();
            foreach (Fly f in flies)
            {
                if (f.delay > 0f) continue;
                float k = f.t * f.t * (3f - 2f * f.t);
                Vector2 a = Vector2.Lerp(f.from, f.ctrl, k), b = Vector2.Lerp(f.ctrl, to, k);
                Vector2 p = Vector2.Lerp(a, b, k);
                float s = sz * (1.2f - 0.4f * k);
                GUI.DrawTexture(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), GFArt.CoinIcon);
            }

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.wordWrap = true;
            }

            if (titleOpen)
            {
                Panel(new Rect(-20f, -20f, Screen.width + 40f, Screen.height + 40f), new Color(0.02f, 0.03f, 0.08f, 0.55f));
                float bob = Mathf.Sin(titleT * 2f) * 8f * u;
                titleStyle.fontSize = Mathf.RoundToInt(132f * u);
                titleStyle.normal.textColor = new Color(0.25f, 0.13f, 0.02f, 0.9f);   // ombre
                GUI.Label(new Rect(0f, Screen.height * 0.24f + bob + 8f * u, Screen.width + 8f * u, 340f * u), "GOLD\nFOUNDRY", titleStyle);
                titleStyle.normal.textColor = new Color(1f, 0.8f, 0.25f);
                GUI.Label(new Rect(0f, Screen.height * 0.24f + bob, Screen.width, 340f * u), "GOLD\nFOUNDRY", titleStyle);
                titleStyle.fontSize = Mathf.RoundToInt(36f * u);
                titleStyle.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
                GUI.Label(new Rect(0f, Screen.height * 0.24f + 330f * u, Screen.width, 60f * u), "Regarde la matière prendre vie", titleStyle);
                float blink = 0.55f + 0.45f * Mathf.Sin(titleT * 3.5f);
                titleStyle.fontSize = Mathf.RoundToInt(44f * u);
                titleStyle.normal.textColor = new Color(1f, 1f, 1f, blink);
                GUI.Label(new Rect(0f, Screen.height * 0.78f, Screen.width, 80f * u), "TOUCHE POUR JOUER", titleStyle);
                return;
            }

            if (offlineGain > 0)
            {
                Panel(new Rect(-20f, -20f, Screen.width + 40f, Screen.height + 40f), new Color(0f, 0f, 0f, 0.5f));
                Rect r = new Rect(80f * u, Screen.height * 0.32f, Screen.width - 160f * u, 520f * u);
                Panel(r, new Color(0.06f, 0.09f, 0.17f, 0.97f));
                titleStyle.fontSize = Mathf.RoundToInt(40f * u);
                titleStyle.normal.textColor = new Color(1f, 0.8f, 0.3f);
                GUI.Label(new Rect(r.x, r.y + 30f * u, r.width, 60f * u), "PENDANT TON ABSENCE", titleStyle);
                titleStyle.fontSize = Mathf.RoundToInt(30f * u);
                titleStyle.normal.textColor = new Color(0.75f, 0.8f, 0.92f);
                GUI.Label(new Rect(r.x + 30f * u, r.y + 96f * u, r.width - 60f * u, 60f * u), "ta fonderie a tourné " + Duration(offlineSeconds), titleStyle);

                float pop = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
                float ic = 90f * u * pop;
                string txt = "+" + Short(offlineGain);
                titleStyle.fontSize = Mathf.RoundToInt(92f * u * pop);
                titleStyle.normal.textColor = Color.white;
                GUI.DrawTexture(new Rect(r.center.x - 210f * u - ic * 0.5f, r.y + 230f * u - ic * 0.5f, ic, ic), GFArt.CoinIcon);
                GUI.Label(new Rect(r.x + 60f * u, r.y + 170f * u, r.width - 60f * u, 120f * u), txt, titleStyle);

                Rect b = new Rect(r.center.x - 230f * u, r.yMax - 150f * u, 460f * u, 110f * u);
                Panel(b, new Color(0.16f, 0.62f, 0.25f, 0.97f));
                titleStyle.fontSize = Mathf.RoundToInt(42f * u);
                titleStyle.normal.textColor = Color.white;
                GUI.Label(b, "RÉCUPÉRER", titleStyle);
            }
        }

        static string Duration(double s)
        {
            int m = (int)(s / 60);
            if (m < 60) return m + " min";
            return (m / 60) + " h " + (m % 60).ToString("00");
        }
    }
}
