using UnityEngine;

namespace GoldFoundry
{
    /// <summary>
    /// Économie et améliorations : cinq machines, cinq niveaux chacune.
    /// Règle de la vision : chaque amélioration doit se voir ET s'entendre à l'écran, pas seulement changer un chiffre.
    /// Sauvegarde automatique (PlayerPrefs) : argent et niveaux.
    /// </summary>
    public partial class GFFactory
    {
        public const int MaxLevel = 5;
        static readonly string[] UpName = { "TUYAU", "PISTON", "FOUR", "RÉSERVOIR", "PRESSE" };
        static readonly string[] UpDesc =
        {
            "Plus de minerai, des tas plus gros",
            "Pousse plus vite, frappe plus fort",
            "Plus chaud : fonte rapide, +1 pièce/lingot",
            "Coulées plus grosses et plus rapides",
            "+2 pièces par lingot, frappe plus fort",
        };
        static readonly int[] UpBase = { 30, 45, 60, 50, 40 };
        const float UpGrowth = 1.8f;

        public readonly int[] Levels = new int[5];

        // ------------------------------------------------------------ matières (la « réponse du jour 3 »)
        public int Tier;
        static readonly string[] TierName = { "OR", "PLATINE", "ORICHALQUE" };
        static readonly int[] TierMult = { 1, 4, 16 };
        static readonly Color[] THot = { new Color(1f, 0.42f, 0.05f), new Color(0.78f, 0.88f, 1f), new Color(1f, 0.3f, 0.5f) };
        static readonly Color[] THotEmit = { new Color(0.85f, 0.3f, 0f), new Color(0.55f, 0.75f, 1.2f), new Color(1f, 0.15f, 0.45f) };
        static readonly Color[] TSolid = { new Color(1f, 0.74f, 0.18f), new Color(0.84f, 0.87f, 0.93f), new Color(0.95f, 0.45f, 0.38f) };
        static readonly Color[] TSolidEmit = { new Color(0.22f, 0.13f, 0f), new Color(0.08f, 0.1f, 0.14f), new Color(0.22f, 0.05f, 0.06f) };
        static readonly Color[] TCoin = { new Color(1f, 0.76f, 0.2f), new Color(0.86f, 0.89f, 0.96f), new Color(1f, 0.55f, 0.45f) };
        static readonly Color[] TCoinEmit = { new Color(0.25f, 0.16f, 0.02f), new Color(0.1f, 0.12f, 0.16f), new Color(0.25f, 0.08f, 0.06f) };

        public int ValueMult { get { return TierMult[Mathf.Clamp(Tier, 0, TierMult.Length - 1)]; } }
        public string MaterialName { get { return TierName[Mathf.Clamp(Tier, 0, TierName.Length - 1)]; } }

        bool AllMax()
        {
            for (int i = 0; i < Levels.Length; i++) if (Levels[i] < MaxLevel) return false;
            return true;
        }

        /// <summary>Teinte toute la matière (bain, coulées, moules, lingots, pièces) selon la matière en cours.</summary>
        void ApplyTier()
        {
            int t = Mathf.Clamp(Tier, 0, TierName.Length - 1);
            GFMold.HotBase = THot[t]; GFMold.HotEmit = THotEmit[t];
            GFMold.GoldBase = TSolid[t]; GFMold.GoldEmit = TSolidEmit[t];
            Material[] hot = { mMolten, Furnace.pool != null ? Furnace.pool.mat : null, Tank.liquid != null ? Tank.liquid.mat : null };
            foreach (Material m in hot)
                if (m != null) { GFMat.SetBase(m, THot[t]); GFMat.SetEmission(m, THotEmit[t]); }
            GFMat.SetBase(mGold, TSolid[t]); GFMat.SetEmission(mGold, TSolidEmit[t]);
            GFMat.SetBase(Coins.coinMat, TCoin[t]); GFMat.SetEmission(Coins.coinMat, TCoinEmit[t]);
        }

        /// <summary>Toutes les machines au maximum : on fond une nouvelle matière. Les machines repartent à zéro, chaque pièce vaut beaucoup plus.</summary>
        void Prestige()
        {
            if (!AllMax() || Tier >= TierName.Length - 1) return;
            Tier++;
            for (int i = 0; i < Levels.Length; i++) Levels[i] = 0;
            Money = 0;
            shopOpen = false;
            ApplyUpgrades();
            ApplyTier();
            Furnace.Stoke();
            Vector3 p = new Vector3(-0.87f, 0.9f, -0.5f);
            GFFx.I.Sparks(p, 70, 4f);
            GFFx.I.Glints(p, 30);
            GFFx.I.Shake(0.4f);
            GFSfx.I.Play(GFSfx.I.Chime, 0.9f, 0.8f);
            GFSfx.I.Play(GFSfx.I.Thump, 1f, 0.7f);
            GFHaptics.Impact(2);
            toast = MaterialName + " !";
            toastT = 0f;
            SaveGame();
        }

        bool shopOpen;
        Rect shopBtnRect, shopRect, prestigeRect;
        int ExtraRow { get { return AllMax() ? 1 : 0; } }
        readonly Rect[] buyRects = new Rect[5];
        float saveTimer, toastT = 1f;
        string toast = "";
        float baseFurnaceStreamW = -1f, baseTankStreamW = -1f;

        public long Cost(int i)
        {
            return (long)Mathf.Round(UpBase[i] * Mathf.Pow(UpGrowth, Levels[i]) * Mathf.Pow(3f, Tier));
        }

        bool AnyAffordable()
        {
            for (int i = 0; i < Levels.Length; i++)
                if (Levels[i] < MaxLevel && Money >= Cost(i)) return true;
            return false;
        }

        /// <summary>Applique les niveaux aux machines (au chargement, après un achat, en sortant du mode vidéo).</summary>
        void ApplyUpgrades()
        {
            int pipe = Levels[0], piston = Levels[1], furnace = Levels[2], tank = Levels[3], press = Levels[4];

            oreRate = 2.6f + 0.6f * pipe;
            pileTrigger = 14 + 3 * pipe + 2 * piston;
            Piston.level = piston;
            Furnace.level = furnace;
            Tank.Capacity = 15f + 4f * tank;
            Tank.Batch = 6f + 2.5f * tank;
            Tank.Flow = 1f + 0.22f * tank;
            if (baseFurnaceStreamW < 0f) { baseFurnaceStreamW = Furnace.stream.width; baseTankStreamW = Tank.stream.width; }
            Furnace.stream.width = baseFurnaceStreamW * (1f + 0.12f * tank);
            Tank.stream.width = baseTankStreamW * (1f + 0.1f * tank);
            Press.level = press;
            Press.coinsPerBar = 8 + 2 * press + furnace;
        }

        bool TryBuy(int i)
        {
            if (Levels[i] >= MaxLevel) return false;
            long c = Cost(i);
            if (Money < c)
            {
                GFSfx.I.Play(GFSfx.I.Click, 0.4f, 0.6f);
                return false;
            }
            Money -= c;
            Levels[i]++;
            GFHaptics.Impact(1);
            ApplyUpgrades();
            UpgradeShow(i);
            SaveGame();
            return true;
        }

        /// <summary>Le moment de l'achat : la machine réagit tout de suite (secousse, étincelles, carillon).</summary>
        void UpgradeShow(int i)
        {
            Vector3 p;
            switch (i)
            {
                case 0:
                    p = new Vector3(PipeX, 8.6f, -0.5f);
                    pipePunch.Punch();
                    for (int k = 0; k < 10 + 2 * Levels[0]; k++) SpawnOre();   // une averse de minerai pour fêter ça
                    GFSfx.I.PlayVar(GFSfx.I.Whoosh, 0.6f, 0.15f);
                    break;
                case 1:
                    p = Piston.transform.position + new Vector3(-0.3f, 0f, -0.5f);
                    Piston.Fire(true);
                    break;
                case 2:
                    p = new Vector3(-0.87f, 0.9f, -0.5f);
                    furnacePunch.Punch();
                    Furnace.Stoke();
                    break;
                case 3:
                    p = new Vector3(1.46f, -2.4f, -0.5f);
                    tankPunch.Punch();
                    Tank.Tap();
                    break;
                default:
                    p = new Vector3(PressX, -6.2f, -0.5f);
                    Press.Tap();
                    break;
            }
            GFFx.I.Sparks(p, 30, 3.2f);
            GFFx.I.Glints(p, 14);
            GFFx.I.Shake(0.12f);
            GFSfx.I.Play(GFSfx.I.Chime, 0.75f, 1f + 0.06f * Levels[i]);   // le carillon monte d'un cran à chaque niveau
            toast = UpName[i] + "  NIV. " + (Levels[i] + 1);
            toastT = 0f;
        }

        // ------------------------------------------------------------ sauvegarde

        void LoadGame()
        {
            long m;
            if (long.TryParse(PlayerPrefs.GetString("gf_money", "0"), out m)) Money = m;
            for (int i = 0; i < Levels.Length; i++)
                Levels[i] = Mathf.Clamp(PlayerPrefs.GetInt("gf_lvl_" + i, 0), 0, MaxLevel);
            Tier = Mathf.Clamp(PlayerPrefs.GetInt("gf_tier", 0), 0, TierName.Length - 1);
            ApplyUpgrades();
            ApplyTier();
            CheckOffline();
            StartTutorialIfNew();
        }

        void SaveGame()
        {
            SaveClock();
            if (videoMode) { PlayerPrefs.Save(); return; }   // le mode vidéo gonfle les gains : on ne les enregistre pas
            PlayerPrefs.SetString("gf_money", Money.ToString());
            for (int i = 0; i < Levels.Length; i++) PlayerPrefs.SetInt("gf_lvl_" + i, Levels[i]);
            PlayerPrefs.SetInt("gf_tier", Tier);
            PlayerPrefs.Save();
        }

        void TickSave(float dt)
        {
            saveTimer += dt;
            if (saveTimer > 5f) { saveTimer = 0f; SaveGame(); }
            if (toastT < 1f) toastT = Mathf.Min(1f, toastT + dt / 1.6f);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveGame();
            else if (!titleOpen && offlineGain <= 0) CheckOffline();   // retour dans l'app : « pendant ton absence… »
        }
        void OnApplicationQuit() { SaveGame(); }

        // ------------------------------------------------------------ boutique (interface)

        void LayoutShop(float u, float bw, float bh, float m)
        {
            float gap = 20f * u;
            shopBtnRect = new Rect(m + bw + gap, Screen.height - bh - m, Screen.width - 2f * (m + bw + gap), bh);
            float rowH = 150f * u, headH = 90f * u;
            float h = headH + rowH * (Levels.Length + ExtraRow) + 20f * u;
            shopRect = new Rect(36f * u, shopBtnRect.y - 24f * u - h, Screen.width - 72f * u, h);
            prestigeRect = new Rect(shopRect.xMax - 24f * u - 250f * u, shopRect.y + headH + 22f * u, 250f * u, rowH - 44f * u);
            for (int i = 0; i < Levels.Length; i++)
            {
                float y = shopRect.y + headH + rowH * (i + ExtraRow);
                buyRects[i] = new Rect(shopRect.xMax - 24f * u - 250f * u, y + 22f * u, 250f * u, rowH - 44f * u);
            }
        }

        /// <summary>Renvoie true si le toucher concerne la boutique (et ne doit pas aller aux machines).</summary>
        bool ShopInput(Vector2 gui)
        {
            if (shopBtnRect.Contains(gui))
            {
                shopOpen = !shopOpen;
                GFSfx.I.Play(GFSfx.I.Click, 0.6f, shopOpen ? 1.1f : 0.9f);
                hintTimer = 0f;
                return true;
            }
            if (!shopOpen) return false;
            if (shopRect.Contains(gui))
            {
                if (ExtraRow == 1 && prestigeRect.Contains(gui)) { Prestige(); return true; }
                for (int i = 0; i < buyRects.Length; i++)
                    if (buyRects[i].Contains(gui)) { TryBuy(i); break; }
                return true;
            }
            shopOpen = false;   // toucher à côté ferme la boutique
            return true;
        }

        void DrawShop(float u, Color panel, Color gold)
        {
            // bouton AMÉLIORER : il pulse quand un achat est possible
            bool can = AnyAffordable() || (AllMax() && Tier < TierName.Length - 1);
            float pulse = can ? 0.5f + 0.5f * Mathf.Sin(Time.time * 5f) : 0f;
            Color btn = shopOpen ? new Color(0.25f, 0.2f, 0.06f, 0.95f) : Color.Lerp(panel, new Color(0.55f, 0.38f, 0.05f, 0.95f), pulse * 0.8f);
            Panel(shopBtnRect, btn);
            buttonStyle.fontSize = Mathf.RoundToInt(36f * u);
            buttonStyle.normal.textColor = gold;
            GUI.Label(shopBtnRect, shopOpen ? "FERMER" : "AMÉLIORER", buttonStyle);
            if (can && !shopOpen)
                Panel(new Rect(shopBtnRect.xMax - 34f * u, shopBtnRect.y + 12f * u, 22f * u, 22f * u), new Color(0.3f, 1f, 0.35f));

            // annonce après un achat
            if (toastT < 1f)
            {
                float a = toastT < 0.7f ? 1f : 1f - (toastT - 0.7f) / 0.3f;
                float s = 1f + 0.25f * Mathf.Sin(Mathf.Clamp01(toastT * 4f) * Mathf.PI);
                hintStyle.fontSize = Mathf.RoundToInt(64f * u * s);
                hintStyle.normal.textColor = new Color(1f, 0.82f, 0.3f, a);
                GUI.Label(new Rect(0f, Screen.height * 0.36f - 60f * u * (toastT * 0.6f), Screen.width, 120f * u), toast, hintStyle);
            }

            if (!shopOpen) return;

            Panel(shopRect, new Color(0.05f, 0.08f, 0.15f, 0.95f));
            buttonStyle.fontSize = Mathf.RoundToInt(40f * u);
            buttonStyle.normal.textColor = gold;
            GUI.Label(new Rect(shopRect.x, shopRect.y + 14f * u, shopRect.width, 64f * u), "AMÉLIORATIONS", buttonStyle);

            float rowH = 150f * u, headH = 90f * u;
            if (ExtraRow == 1)
            {
                // nouvelle matière : toutes les machines sont au maximum
                float y0 = shopRect.y + headH;
                bool last = Tier >= TierName.Length - 1;
                bigStyle.fontSize = Mathf.RoundToInt(36f * u);
                bigStyle.normal.textColor = gold;
                GUI.Label(new Rect(shopRect.x + 30f * u, y0 + 14f * u, 560f * u, 52f * u), last ? "TOUT DÉCOUVERT" : "NOUVELLE MATIÈRE", bigStyle);
                smallStyle.fontSize = Mathf.RoundToInt(26f * u);
                smallStyle.normal.textColor = new Color(0.85f, 0.88f, 0.95f);
                GUI.Label(new Rect(shopRect.x + 30f * u, y0 + 70f * u, prestigeRect.x - shopRect.x - 40f * u, 56f * u),
                    last ? "Ta fonderie maîtrise toutes les matières !" : TierName[Tier + 1] + " : pièces ×" + (TierMult[Tier + 1] / TierMult[Tier]) + ", machines à zéro", smallStyle);
                if (!last)
                {
                    float glow = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
                    Panel(prestigeRect, Color.Lerp(new Color(0.75f, 0.52f, 0.08f, 0.97f), new Color(1f, 0.75f, 0.2f, 0.97f), glow));
                    buttonStyle.fontSize = Mathf.RoundToInt(38f * u);
                    buttonStyle.normal.textColor = Color.white;
                    GUI.Label(prestigeRect, "FONDRE", buttonStyle);
                }
            }
            for (int i = 0; i < Levels.Length; i++)
            {
                float y = shopRect.y + headH + rowH * (i + ExtraRow);
                if (i + ExtraRow > 0) Panel(new Rect(shopRect.x + 24f * u, y, shopRect.width - 48f * u, 2f * u), new Color(1f, 1f, 1f, 0.08f));

                bigStyle.fontSize = Mathf.RoundToInt(38f * u);
                bigStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(shopRect.x + 30f * u, y + 14f * u, 420f * u, 52f * u), UpName[i], bigStyle);

                // niveau : cinq crans
                for (int k = 0; k < MaxLevel; k++)
                    Panel(new Rect(shopRect.x + 300f * u + k * 34f * u, y + 30f * u, 26f * u, 22f * u),
                        k < Levels[i] ? gold : new Color(1f, 1f, 1f, 0.14f));

                smallStyle.fontSize = Mathf.RoundToInt(26f * u);
                smallStyle.normal.textColor = new Color(0.72f, 0.78f, 0.9f);
                GUI.Label(new Rect(shopRect.x + 30f * u, y + 70f * u, buyRects[i].x - shopRect.x - 40f * u, 56f * u), UpDesc[i], smallStyle);

                Rect b = buyRects[i];
                bool max = Levels[i] >= MaxLevel;
                bool ok = !max && Money >= Cost(i);
                Panel(b, max ? new Color(0.25f, 0.25f, 0.3f, 0.9f) : ok ? new Color(0.16f, 0.62f, 0.25f, 0.95f) : new Color(0.22f, 0.24f, 0.32f, 0.95f));
                if (max)
                {
                    buttonStyle.fontSize = Mathf.RoundToInt(36f * u);
                    buttonStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
                    GUI.Label(b, "MAX", buttonStyle);
                }
                else
                {
                    GUI.DrawTexture(new Rect(b.x + 22f * u, b.y + (b.height - 44f * u) * 0.5f, 44f * u, 44f * u), GFArt.CoinIcon);
                    bigStyle.fontSize = Mathf.RoundToInt(36f * u);
                    bigStyle.normal.textColor = ok ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                    GUI.Label(new Rect(b.x + 76f * u, b.y, b.width - 86f * u, b.height), Short(Cost(i)), bigStyle);
                }
            }
        }
    }
}
