using UnityEngine;

namespace GoldFoundry
{
    /// <summary>
    /// Premier lancement : « le niveau lui-même devient le tutoriel ».
    /// Le joueur lance lui-même chaque machine une fois (tuyau → piston → four → réservoir → presse),
    /// puis la fonderie passe en automatique et la boutique apparaît, avec une petite prime pour le premier achat.
    /// </summary>
    public partial class GFFactory
    {
        enum Tuto { Pipe, Piston, Furnace, Tank, Press, Done, Off }

        Tuto tuto = Tuto.Off;
        float tutoT, tutoDoneT;
        long tutoMoney;
        const int TutoBonus = 40;

        /// <summary>Pendant le tutoriel, la presse attend que le joueur la touche (le reste tourne déjà seul).</summary>
        public bool HoldPress { get { return tuto == Tuto.Press || tuto == Tuto.Tank; } }
        bool InTutorial { get { return tuto != Tuto.Off && tuto != Tuto.Done; } }

        void StartTutorialIfNew()
        {
            bool veteran = PlayerPrefs.GetInt("gf_tuto", 0) == 1 || Money > 0;
            for (int i = 0; i < Levels.Length; i++) if (Levels[i] > 0) veteran = true;
            if (veteran) return;
            tuto = Tuto.Pipe;
            tutoT = 0f;
            Auto = false;
        }

        void SetTuto(Tuto t)
        {
            tuto = t;
            tutoT = 0f;
            GFSfx.I.Play(GFSfx.I.Chime, 0.35f, 1.3f + 0.08f * (int)t);
            GFHaptics.Impact(0);
        }

        /// <summary>Le tuyau ne verse rien tant que le joueur ne l'a pas touché la première fois.</summary>
        bool TutoBlocksPipe { get { return tuto == Tuto.Pipe; } }

        void OnTutoPipeTapped() { if (tuto == Tuto.Pipe) SetTuto(Tuto.Piston); }

        bool TutoReady()
        {
            switch (tuto)
            {
                case Tuto.Pipe: return true;
                case Tuto.Piston: return PileCount() >= 5 || Piston.Busy;
                case Tuto.Furnace: return Furnace.molten >= 3f && !Piston.Busy;
                case Tuto.Tank: return Tank.Units >= Tank.PerMold - 0.01f && Carousel.ReadyForPour;
                case Tuto.Press: return Bars.HasBarUnderPress();
            }
            return false;
        }

        void TickTutorial(float dt)
        {
            if (tuto == Tuto.Off || titleOpen) return;
            tutoT += dt;
            switch (tuto)
            {
                case Tuto.Piston:
                    if (Piston.Busy) SetTuto(Tuto.Furnace);
                    break;
                case Tuto.Furnace:
                    if (Furnace.Pouring) SetTuto(Tuto.Tank);
                    break;
                case Tuto.Tank:
                    if (Tank.Pouring)
                    {
                        // première coulée lancée à la main : le reste de la chaîne tourne seul,
                        // seule la presse attend le joueur pour la première pièce
                        Auto = true;
                        SetTuto(Tuto.Press);
                        tutoMoney = Money;
                    }
                    break;
                case Tuto.Press:
                    if (Money > tutoMoney) FinishTutorial(true);
                    break;
                case Tuto.Done:
                    tutoDoneT += dt;
                    if (tutoDoneT > 4.5f) tuto = Tuto.Off;
                    return;
            }
            // filet de sécurité : si une étape n'arrive jamais (blocage physique), on passe en automatique
            if (InTutorial && !TutoReady() && tutoT > 30f) FinishTutorial(false);
        }

        void FinishTutorial(bool reward)
        {
            Auto = true;
            tuto = Tuto.Done;
            tutoDoneT = 0f;
            PlayerPrefs.SetInt("gf_tuto", 1);
            if (reward)
            {
                AddMoney(TutoBonus);
                for (int i = 0; i < 10; i++) FlyFromGui(new Vector2(Screen.width * 0.5f, Screen.height * 0.45f) + Random.insideUnitCircle * 80f * UiScale(), 0.3f + i * 0.05f);
                GFSfx.I.Play(GFSfx.I.Chime, 0.8f, 1f);
                GFHaptics.Impact(2);
            }
            SaveGame();
        }

        void StopTutorialForVideo()
        {
            if (InTutorial) tuto = Tuto.Off;   // pas enregistré : le tutoriel reviendra au prochain lancement
        }

        void DrawTutorial(float u)
        {
            if (tuto == Tuto.Off) return;

            if (tuto == Tuto.Done)
            {
                float a = tutoDoneT < 3.5f ? 1f : 1f - (tutoDoneT - 3.5f);
                Rect r = new Rect(70f * u, Screen.height * 0.4f, Screen.width - 140f * u, 230f * u);
                Panel(r, new Color(0.06f, 0.09f, 0.17f, 0.93f * a));
                titleStyle.fontSize = Mathf.RoundToInt(44f * u);
                titleStyle.normal.textColor = new Color(1f, 0.8f, 0.3f, a);
                GUI.Label(new Rect(r.x, r.y + 24f * u, r.width, 70f * u), "TA FONDERIE TOURNE !", titleStyle);
                titleStyle.fontSize = Mathf.RoundToInt(30f * u);
                titleStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f * a);
                GUI.Label(new Rect(r.x + 30f * u, r.y + 100f * u, r.width - 60f * u, 110f * u),
                    "Elle travaille seule maintenant.\nTouche AMÉLIORER pour la faire grandir.", titleStyle);
                return;
            }

            if (!TutoReady()) return;

            Vector3 world; string text; bool below = true;
            switch (tuto)
            {
                case Tuto.Pipe: world = new Vector3(PipeX, 8.9f, 0f); text = "Touche le tuyau\npour verser le minerai"; break;
                case Tuto.Piston: world = new Vector3(3.3f, 4.8f, 0f); text = "Touche le piston :\nil pousse tout dans le four"; below = true; break;
                case Tuto.Furnace: world = new Vector3(-0.87f, 0.9f, 0f); text = "Touche le four\npour faire couler l'or"; break;
                case Tuto.Tank: world = new Vector3(1.46f, -2.4f, 0f); text = "Touche le réservoir\npour remplir un moule"; below = false; break;
                default: world = new Vector3(PressX, -6.1f, 0f); text = "Touche la presse :\nta première pièce !"; below = false; break;
            }
            Vector3 sp = cam.WorldToScreenPoint(world);
            Vector2 g = new Vector2(sp.x, Screen.height - sp.y);

            // cercle lumineux qui respire sur la machine à toucher
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
            float size = (230f + 60f * pulse) * u;
            Color old = GUI.color;
            GUI.color = new Color(1f, 0.8f, 0.25f, 0.35f + 0.35f * pulse);
            GUI.DrawTexture(new Rect(g.x - size * 0.5f, g.y - size * 0.5f, size, size), GFMat.SoftDot());
            GUI.color = old;

            // bulle de texte
            float bw = 560f * u, bh = 130f * u;
            float by = below ? g.y + size * 0.42f : g.y - size * 0.42f - bh;
            float bx = Mathf.Clamp(g.x - bw * 0.5f, 30f * u, Screen.width - bw - 30f * u);
            Rect b = new Rect(bx, by, bw, bh);
            Panel(b, new Color(0.06f, 0.09f, 0.17f, 0.94f));
            titleStyle.fontSize = Mathf.RoundToInt(32f * u);
            titleStyle.normal.textColor = Color.white;
            GUI.Label(b, text, titleStyle);
        }
    }
}
