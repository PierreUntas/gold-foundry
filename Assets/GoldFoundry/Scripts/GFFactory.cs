using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GoldFoundry
{
    /// <summary>
    /// GOLD FOUNDRY — Prototype 1, habillé d'après les images du document de vision.
    /// Tour en pierre, une machine par étagère, la matière tombe d'étage en étage :
    /// tuyau → tapis → piston → four → réservoir → carrousel de moules → tapis à lingots → presse → coffre à pièces.
    /// Tout est construit par le code ; Resources/Art/*.png remplace les machines dessinées si présent.
    /// (Les positions passées aux helpers sont locales au parent ; les groupes sont à l'origine.)
    /// </summary>
    public partial class GFFactory : MonoBehaviour
    {
        // ------------------------------------------------------------ disposition (unités monde)
        public const float SLAB = 0.6f;
        public const float BeltY = 7.41f, BeltX0 = -3.1f, BeltX1 = 1.3f, PipeX = -0.95f;
        public const float PlatY = 4.0f, PlatX0 = -1.8f, PlatX1 = 3.4f;
        public const float FurnX0 = -2.75f, FurnX1 = 0.0f, FurnY0 = 0.3f, FurnTop = 2.2f;   // fond du four au niveau du bas de la fenêtre
        public const float TankX0 = 0.2f, TankX1 = 1.8f, TankY0 = -3.45f, TankY1 = -1.95f;
        public const float MoldY = -4.94f;   // moules posés sur le tapis dessiné
        public static readonly float[] Slots = { 2.7f, 1.7f, 0.7f, -0.3f, -1.3f };
        public const float BarBeltY = -6.91f, BarBeltX0 = -1.6f, BarBeltX1 = 1.85f, PressX = 1.48f;   // surface du tapis à -6,81
        public const float VaultX0 = 0.71f, VaultX1 = 2.87f, VaultFloor = -9.15f;

        public static GFFactory I;

        [Header("Réglages")]
        [Tooltip("Automatique par défaut ; désactivez pour tester les micro-gestes seuls.")]
        public bool Auto = true;
        [Tooltip("Morceaux de minerai par seconde.")]
        public float oreRate = 2.6f;
        public int maxOre = 110;
        [Tooltip("Taille de la pile qui déclenche le piston en mode auto.")]
        public int pileTrigger = 14;

        [HideInInspector] public GFBelt Belt;
        [HideInInspector] public GFPiston Piston;
        [HideInInspector] public GFFurnace Furnace;
        [HideInInspector] public GFTank Tank;
        [HideInInspector] public GFCarousel Carousel;
        [HideInInspector] public GFBars Bars;
        [HideInInspector] public GFPress Press;
        [HideInInspector] public GFCoins Coins;
        [HideInInspector] public long Money;

        Camera cam;
        Transform root, arch;
        GFPunch pipePunch, furnacePunch, tankPunch;
        float spawnAcc;
        readonly List<GFOre> orePool = new List<GFOre>();
        readonly List<GFOre> activeOre = new List<GFOre>();
        readonly Queue<KeyValuePair<float, int>> income = new Queue<KeyValuePair<float, int>>();
        int incomeWindow;

        Material mSteel, mChrome, mAccent, mMolten, mGold, mNavyPanel, mDarkPanel;

        // interface
        float hintTimer = 9f, moneyPop = 1f;
        Rect autoRect, soundRect, facRect;
        bool videoMode;
        float fps = 60f;
        GUIStyle panelStyle, bigStyle, smallStyle, hintStyle, buttonStyle;

        // ================================================================== construction

        void Awake()
        {
            I = this;
            transform.position = Vector3.zero;
            root = transform;
            Application.targetFrameRate = 60;
            Time.fixedDeltaTime = 0.01f; // piston rapide : pas de physique fin pour éviter que le minerai traverse

            Step("Matériaux", BuildMaterials);
            Step("Caméra et lumières", BuildEnvironment);
            Step("Son", delegate { gameObject.AddComponent<GFSfx>(); });
            Step("Effets", delegate { gameObject.AddComponent<GFFx>().Init(cam); });

            // chaque étape est isolée : une erreur s'affiche à l'écran (GFErrorOverlay) sans bloquer le reste
            Step("Architecture", BuildArchitecture);
            Step("Tuyau et tapis", BuildPipeAndBelt);
            Step("Piston", BuildPlatformAndPiston);
            Step("Four", BuildFurnace);
            Step("Réservoir", BuildTank);
            Step("Moules", BuildCarousel);
            Step("Lingots et presse", BuildBarsAndPress);
            Step("Coffre et pièces", BuildVaultAndCoins);
            Step("Minerai", BuildOrePool);
            // le fond peint en dernier : il masque aussi les éléments d'architecture ajoutés par les autres étapes
            Step("Fond", delegate { GFArt.Skin("background", arch, new Vector2(0.25f, -0.6f), ViewSize(SLAB + 0.1f), SLAB + 0.1f); });
            Step("Sauvegarde", LoadGame);
        }

        static void Step(string name, System.Action build)
        {
            try { build(); }
            catch (System.Exception e) { Debug.LogError("[Gold Foundry] " + name + " : " + e); }
        }

        void BuildMaterials()
        {
            mSteel = GFArt.PanelMat(new Color(0.55f, 0.58f, 0.64f));
            mChrome = GFMat.Lit(GFArt.Chrome, 0.85f, 0.85f);
            mAccent = GFMat.Lit(new Color(1f, 0.72f, 0.12f), 0.2f, 0.5f);
            mMolten = GFMat.Lit(GFMold.HotBase, 0f, 0.9f, GFMold.HotEmit);
            mGold = GFMat.Lit(GFMold.GoldBase, 0.55f, 0.85f, GFMold.GoldEmit);
            mNavyPanel = GFArt.PanelMat(GFArt.Navy);
            mDarkPanel = GFArt.PanelMat(GFArt.NavyDark);
        }

        void BuildEnvironment()
        {
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            // caméra orthographique : scène plate et peinte, aucune parallaxe entre le fond, les images et la matière.
            // taille réglée pour que les images gardent l'échelle du gabarit (art_layout_guide.png).
            cam.orthographic = true;
            cam.orthographicSize = 10.06f;
            cam.transform.rotation = Quaternion.identity;
            cam.transform.position = new Vector3(0.25f, -0.6f, -27f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.07f, 0.13f);
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 60f;
            camGo.AddComponent<AudioListener>();

            Light key = new GameObject("Key Light").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 0.75f;
            key.color = new Color(1f, 0.9f, 0.78f);
            key.transform.rotation = Quaternion.Euler(32f, -22f, 0f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.6f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.27f, 0.31f, 0.43f);

            // flaques de lumière chaude, comme les lampes des maquettes
            Vector3[] lamps = { new Vector3(-2.6f, 8.6f, -1.6f), new Vector3(2.7f, 5.9f, -1.6f), new Vector3(-2.7f, -1.9f, -1.6f), new Vector3(2.7f, -4.0f, -1.6f), new Vector3(-0.8f, -8.2f, -1.6f) };
            foreach (Vector3 lp in lamps)
            {
                Light pl = new GameObject("Warm Lamp").AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = new Color(1f, 0.74f, 0.42f);
                pl.intensity = 1.5f;
                pl.range = 5.5f;
                pl.shadows = LightShadows.None;
                pl.transform.position = lp;
            }
        }

        /// <summary>La tour : mur du fond en pierre, deux piliers, une étagère de pierre sous chaque machine.</summary>
        void BuildArchitecture()
        {
            arch = Group("Architecture");

            BoxIn("Back Wall", arch, new Vector3(0.25f, -0.6f, SLAB + 0.15f), new Vector3(12f, 24f, 0.3f), GFArt.StoneMat(GFArt.StoneDark, 12f, 24f), true);
            BoxIn("Front Glass", arch, new Vector3(0.25f, -0.6f, -SLAB - 0.15f), new Vector3(12f, 24f, 0.3f), null, true);
            GameObject lw = BoxIn("Left Wall", arch, new Vector3(-2.95f, 4.4f, 0f), new Vector3(0.3f, 9.6f, SLAB * 2f), null, true);
            PhysicsMaterial slick = new PhysicsMaterial("Slick");
            slick.dynamicFriction = 0f; slick.staticFriction = 0f;
            slick.frictionCombine = PhysicsMaterialCombine.Minimum;
            lw.GetComponent<Collider>().sharedMaterial = slick;   // le minerai glisse le long du mur au lieu de s'y coincer
            BoxIn("Right Wall", arch, new Vector3(3.55f, 6.2f, 0f), new Vector3(0.3f, 4.1f, SLAB * 2f), null, true);

            Color pillar = GFArt.Pillar;
            BoxIn("Pillar L", arch, new Vector3(-3.85f, -0.6f, 0f), new Vector3(1.3f, 24f, 1.7f), GFArt.StoneMat(pillar, 1.3f, 24f), false);
            BoxIn("Pillar R", arch, new Vector3(4.2f, -0.6f, 0f), new Vector3(1.3f, 24f, 1.7f), GFArt.StoneMat(pillar, 1.3f, 24f), false);

            Shelf("Shelf Belt", BeltX0 - 0.1f, BeltX1, 6.85f);
            Shelf("Shelf Furnace", FurnX0, 0.6f, -0.7f);
            Shelf("Shelf Tank", 0.0f, 2.0f, TankY0);
            Shelf("Shelf Molds", -1.15f, 2.95f, -5.67f);
            Shelf("Shelf Bars", BarBeltX0 - 0.1f, BarBeltX1 + 0.05f, -7.43f);

            // quelques tuyaux au fond, pour la profondeur
            Material pipeBack = GFMat.Lit(new Color(0.16f, 0.19f, 0.26f), 0.5f, 0.5f);
            for (int i = 0; i < 2; i++)
            {
                GameObject p = Cyl("Back Pipe", arch, new Vector3(-2.75f + i * 6.0f, -0.6f, SLAB - 0.05f), new Vector3(0.22f, 12f, 0.22f), pipeBack);
                p.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

        }

        /// <summary>Étagère : dalle de pierre claire dont le dessus est à <paramref name="topY"/>.</summary>
        void Shelf(string name, float x0, float x1, float topY)
        {
            const float T = 0.4f;
            float w = x1 - x0;
            BoxIn(name, arch, new Vector3((x0 + x1) * 0.5f, topY - T * 0.5f, 0.05f), new Vector3(w, T, 1.7f), GFArt.SlabMat(GFArt.StoneLight, w), false);
            BoxIn(name + " Lip", arch, new Vector3((x0 + x1) * 0.5f, topY - T - 0.04f, -0.82f), new Vector3(w - 0.1f, 0.08f, 0.1f), mDarkPanel, false);
        }

        void BuildPipeAndBelt()
        {
            Transform pipe = Group("Ore Pipe");
            Material green = GFMat.Lit(GFArt.PipeGreen, 0.25f, 0.85f);
            Color gd = GFArt.PipeGreen * 0.6f;
            gd.a = 1f;
            Material greenDark = GFMat.Lit(gd, 0.25f, 0.7f);
            Cyl("Pipe", pipe, new Vector3(PipeX, 9.6f, 0f), new Vector3(0.95f, 1.35f, 0.95f), green);
            Cyl("Pipe Rim", pipe, new Vector3(PipeX, 8.3f, 0f), new Vector3(1.12f, 0.1f, 1.12f), greenDark);
            Cyl("Pipe Band", pipe, new Vector3(PipeX, 9.3f, 0f), new Vector3(1.04f, 0.08f, 1.04f), greenDark);
            Tap("Tap: Pipe", new Vector3(PipeX, 9.0f, 0f), new Vector3(1.9f, 2.6f, 3f), OnPipeTap);
            GFArt.Skin("pipe", pipe, new Vector2(PipeX, 9.71f), new Vector2(1.1f, 3.02f), -0.8f);   // bouche en bas à y = 8,2, le haut sort de l'écran
            pipePunch = pipe.gameObject.AddComponent<GFPunch>();

            Transform beltRoot = Group("Ore Conveyor");
            float len = BeltX1 - BeltX0;
            Material chain = GFArt.Textured(GFArt.Chain, new Color(0.55f, 0.6f, 0.72f), new Vector2(len / 0.5f, 1f), 0.4f, 0.4f);
            GameObject belt = BoxIn("Ore Belt", beltRoot, new Vector3((BeltX0 + BeltX1) * 0.5f, BeltY, 0f), new Vector3(len, 0.3f, SLAB * 2f), chain, true);
            Belt = belt.AddComponent<GFBelt>();
            Belt.mat = chain;
            Belt.Speed = 1.3f;
            Belt.rollers = new[] { Roller(beltRoot, new Vector3(BeltX0, BeltY, 0f)), Roller(beltRoot, new Vector3(BeltX1, BeltY, 0f)) };
            PhysicsMaterial grip = new PhysicsMaterial("Belt Grip");
            grip.dynamicFriction = 0.9f;
            grip.staticFriction = 0.9f;
            belt.GetComponent<Collider>().sharedMaterial = grip;
            ConveyorFrame(beltRoot, BeltX0, BeltX1, BeltY, 0.3f);
            GFArt.Skin("ore_conveyor", beltRoot, new Vector2(-0.9f, 7.245f), new Vector2(5.06f, 0.79f), -0.8f);   // posé sur l'étagère (6,85), surface du tapis à 7,56
        }

        /// <summary>Longeron bleu devant un tapis + embouts rayés jaune/noir.</summary>
        void ConveyorFrame(Transform parent, float x0, float x1, float y, float h)
        {
            float len = x1 - x0;
            BoxIn("Rail Front", parent, new Vector3((x0 + x1) * 0.5f, y - h * 0.3f, -SLAB - 0.04f), new Vector3(len + 0.2f, h * 0.7f, 0.08f), mNavyPanel, false);
            BoxIn("Cap L", parent, new Vector3(x0 - 0.12f, y, -SLAB - 0.06f), new Vector3(0.24f, h + 0.12f, 0.1f), GFArt.HazardMat(0.24f, h + 0.12f), false);
            BoxIn("Cap R", parent, new Vector3(x1 + 0.12f, y, -SLAB - 0.06f), new Vector3(0.24f, h + 0.12f, 0.1f), GFArt.HazardMat(0.24f, h + 0.12f), false);
        }

        Transform Roller(Transform parent, Vector3 p)
        {
            GameObject r = Cyl("Roller", parent, p, new Vector3(0.36f, SLAB, 0.36f), mChrome);
            r.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return r.transform;
        }

        void BuildPlatformAndPiston()
        {
            BoxIn("Piston Platform", arch, new Vector3((PlatX0 + PlatX1) * 0.5f, PlatY, 0f), new Vector3(PlatX1 - PlatX0, 0.3f, 1.5f),
                GFArt.SlabMat(GFArt.StoneLight, PlatX1 - PlatX0), true);
            BoxIn("Platform Lip", arch, new Vector3((PlatX0 + PlatX1) * 0.5f, PlatY - 0.19f, -0.7f), new Vector3(PlatX1 - PlatX0 - 0.1f, 0.08f, 0.1f), mDarkPanel, false);

            GameObject p = new GameObject("Piston");
            p.transform.SetParent(root, false);
            float py = PlatY + 0.15f + 0.65f + 0.02f;
            p.transform.position = new Vector3(3.1f, py, 0f);
            Rigidbody rb = p.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Material faceMat = GFArt.HazardMat(0.06f, 1.2f);
            BoxIn("Head", p.transform, Vector3.zero, new Vector3(0.5f, 1.3f, SLAB * 2f - 0.05f), mNavyPanel, true);
            BoxIn("Face", p.transform, new Vector3(-0.27f, 0f, 0f), new Vector3(0.06f, 1.2f, SLAB * 2f - 0.1f), faceMat, true);
            BoxIn("Collar", p.transform, new Vector3(0f, 0.45f, -0.02f), new Vector3(0.54f, 0.14f, SLAB * 2f), GFArt.HazardMat(0.54f, 0.14f), false);
            GameObject cap = BoxIn("Cap", p.transform, new Vector3(0.08f, 0.95f, 0f), new Vector3(0.9f, 0.06f, SLAB * 2f - 0.05f), mDarkPanel, true);
            cap.transform.localRotation = Quaternion.Euler(0f, 0f, 40f);
            GameObject rod = Cyl("Rod", p.transform, new Vector3(0.95f, 0f, 0f), new Vector3(0.3f, 0.75f, 0.3f), mChrome);
            rod.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            BoxIn("Piston Housing", arch, new Vector3(3.45f, py, -0.25f), new Vector3(0.35f, 1.1f, 1.0f), mDarkPanel, false);

            Piston = p.AddComponent<GFPiston>();
            Piston.y = py;
            Piston.retractX = 3.1f;
            Piston.extendX = PlatX0 + 0.3f;
            Piston.faceMat = faceMat;
            Tap("Tap: Piston", new Vector3(2.3f, PlatY + 1f, 0f), new Vector3(2.8f, 2.4f, 3f), delegate { Piston.Fire(true); });
            GFArt.Skin("piston", p.transform, new Vector2(3.316f, py), new Vector2(0.932f, 1.3f), -0.8f);   // plaque rayée = face de poussée (x 2,85 au repos)
        }

        void BuildFurnace()
        {
            GameObject f = new GameObject("Furnace");
            f.transform.SetParent(root, false);
            Furnace = f.AddComponent<GFFurnace>();
            Furnace.x0 = FurnX0; Furnace.x1 = FurnX1; Furnace.y0 = FurnY0; Furnace.top = FurnTop;
            Transform shell = Group("Furnace Shell");
            Transform inside = Group("Furnace Inside");

            Furnace.wallMat = GFArt.PanelMat(GFArt.NavyDark);
            Furnace.glowMat = GFMat.Lit(new Color(0.3f, 0.1f, 0.05f), 0f, 0.2f, Color.black);
            Furnace.windowMat = GFMat.Lit(new Color(1f, 0.55f, 0.15f), 0.2f, 0.6f, Color.black);
            float cx = (FurnX0 + FurnX1) * 0.5f, w = FurnX1 - FurnX0, h = FurnTop - FurnY0;

            // cuve (collisions) + fond rougeoyant
            BoxIn("Furnace Floor", shell, new Vector3(cx, FurnY0 - 0.15f, 0f), new Vector3(w + 0.6f, 0.3f, SLAB * 2f), Furnace.wallMat, true);
            // pente invisible : le minerai glisse vers le milieu, là où on le voit fondre par la fenêtre
            GameObject ramp = BoxIn("Furnace Ramp", shell, new Vector3(-2.45f, FurnY0 + 0.5f, 0f), new Vector3(1.9f, 0.1f, SLAB * 2f), null, true);
            ramp.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
            BoxIn("Furnace Right Wall", shell, new Vector3(FurnX1 + 0.15f, FurnY0 + h * 0.5f, 0f), new Vector3(0.3f, h, SLAB * 2f), Furnace.wallMat, true);
            GameObject glowPanel = BoxIn("Furnace Back Glow", inside, new Vector3(cx, FurnY0 + h * 0.5f, SLAB - 0.04f), new Vector3(w, h, 0.04f), Furnace.glowMat, false);

            // façade blindée avec une fenêtre (on voit le bain et le minerai fondre)
            const float WX0 = -2.6f, WX1 = -0.6f, WY0 = -0.05f, WY1 = 1.4f;
            float fz = -SLAB - 0.12f, fx0 = FurnX0, fx1 = FurnX1 + 0.3f, fy0 = -0.7f, fy1 = FurnTop;
            BoxIn("Facade L", shell, new Vector3((fx0 + WX0) * 0.5f, (fy0 + fy1) * 0.5f, fz), new Vector3(WX0 - fx0, fy1 - fy0, 0.14f), mNavyPanel, false);
            BoxIn("Facade R", shell, new Vector3((WX1 + fx1) * 0.5f, (fy0 + fy1) * 0.5f, fz), new Vector3(fx1 - WX1, fy1 - fy0, 0.14f), mNavyPanel, false);
            BoxIn("Facade B", shell, new Vector3((WX0 + WX1) * 0.5f, (fy0 + WY0) * 0.5f, fz), new Vector3(WX1 - WX0, WY0 - fy0, 0.14f), mNavyPanel, false);
            BoxIn("Facade T", shell, new Vector3((WX0 + WX1) * 0.5f, (WY1 + fy1) * 0.5f, fz), new Vector3(WX1 - WX0, fy1 - WY1, 0.14f), mNavyPanel, false);
            BoxIn("Window Frame T", shell, new Vector3((WX0 + WX1) * 0.5f, WY1, fz - 0.06f), new Vector3(WX1 - WX0 + 0.12f, 0.08f, 0.05f), Furnace.windowMat, false);
            BoxIn("Window Frame B", shell, new Vector3((WX0 + WX1) * 0.5f, WY0, fz - 0.06f), new Vector3(WX1 - WX0 + 0.12f, 0.08f, 0.05f), Furnace.windowMat, false);
            BoxIn("Window Frame L", shell, new Vector3(WX0, (WY0 + WY1) * 0.5f, fz - 0.06f), new Vector3(0.08f, WY1 - WY0, 0.05f), Furnace.windowMat, false);
            BoxIn("Window Frame R", shell, new Vector3(WX1, (WY0 + WY1) * 0.5f, fz - 0.06f), new Vector3(0.08f, WY1 - WY0, 0.05f), Furnace.windowMat, false);
            for (int i = 1; i <= 3; i++)
                BoxIn("Grate Bar", shell, new Vector3(Mathf.Lerp(WX0, WX1, i / 4f), (WY0 + WY1) * 0.5f, fz - 0.04f), new Vector3(0.05f, WY1 - WY0, 0.04f), mDarkPanel, false);
            BoxIn("Top Rim", shell, new Vector3((fx0 + fx1) * 0.5f, fy1 + 0.06f, fz - 0.02f), new Vector3(fx1 - fx0, 0.14f, 0.18f), GFArt.HazardMat(fx1 - fx0, 0.14f), false);
            GameObject plate = BoxIn("Furnace Plate", shell, new Vector3(cx, fy0 + 0.12f, fz - 0.08f), new Vector3(w * 0.5f, 0.12f, 0.05f), mChrome, false);
            furnacePunch = plate.AddComponent<GFPunch>();

            // voyants rouges + manomètre
            Furnace.indicatorMats = new Material[3];
            for (int i = 0; i < 3; i++)
            {
                Furnace.indicatorMats[i] = GFMat.Lit(new Color(0.5f, 0.05f, 0.05f), 0.1f, 0.8f, Color.black);
                GameObject led = GFArt.NewShape(PrimitiveType.Sphere, "Indicator");
                led.name = "Indicator";
                led.transform.SetParent(shell, false);
                led.transform.localPosition = new Vector3(-0.3f, 1.85f - i * 0.28f, fz - 0.1f);
                led.transform.localScale = Vector3.one * 0.16f;
                led.GetComponent<Renderer>().sharedMaterial = Furnace.indicatorMats[i];
            }
            Gauge(shell, new Vector3(-0.15f, 0.45f, fz - 0.09f), 0.38f, null);

            Material poolMat = new Material(mMolten);
            Furnace.pool = new GFLiquid(inside, new Vector3(cx, FurnY0, 0f), w - 0.05f, 1.2f, SLAB * 2f - 0.1f, poolMat);
            Furnace.pool.SetWobble(0.6f);

            // goulotte vers le réservoir
            BoxIn("Trough", shell, new Vector3(0.75f, -0.05f, 0f), new Vector3(0.9f, 0.1f, 0.5f), mDarkPanel, false);
            BoxIn("Trough Lip", shell, new Vector3(0.75f, 0.05f, -0.24f), new Vector3(0.9f, 0.12f, 0.04f), mDarkPanel, false);
            Furnace.troughMolten = BoxIn("Trough Molten", inside, new Vector3(0.75f, 0.02f, 0f), new Vector3(0.85f, 0.05f, 0.4f), mMolten, false);
            Furnace.troughMolten.AddComponent<GFMatter>();
            Furnace.troughMolten.SetActive(false);
            Furnace.spoutPos = new Vector3(0.95f, -0.2f, 0f);   // bouche du bec verseur
            Furnace.stream = new GFStream(inside, mMolten, 0.16f);
            Furnace.halo = new GFHalo(inside, new Color(1f, 0.4f, 0.05f, 0.5f));

            Light l = new GameObject("Furnace Light").AddComponent<Light>();
            l.transform.SetParent(inside, false);
            l.transform.localPosition = new Vector3(cx, 0.9f, -1.8f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.55f, 0.2f);
            l.range = 7f;
            l.shadows = LightShadows.None;
            Furnace.glowLight = l;

            Tap("Tap: Furnace", new Vector3(cx, 0.8f, 0f), new Vector3(w + 0.4f, 2.8f, 3f), OnFurnaceTap);
            // image : 4,0 × 3,21 unités, posée sur l'étagère (y = -0,7), fenêtre transparente sur l'intérieur du four
            if (GFArt.Skin("furnace", shell, new Vector2(-0.87f, 0.905f), new Vector2(4.0f, 3.21f), -0.85f))
            {
                Destroy(Furnace.troughMolten);
                Furnace.troughMolten = null;
                // la lueur intérieure et le bain se limitent à la fenêtre de l'image
                const float WinX0 = -1.68f, WinX1 = -0.29f, WinY1 = 1.16f;
                glowPanel.transform.localPosition = new Vector3((WinX0 + WinX1) * 0.5f, (FurnY0 + WinY1) * 0.5f, SLAB - 0.04f);
                glowPanel.transform.localScale = new Vector3(WinX1 - WinX0, WinY1 - FurnY0, 0.04f);
                Furnace.pool = new GFLiquid(inside, new Vector3((WinX0 + WinX1) * 0.5f, FurnY0, 0f), WinX1 - WinX0, 0.8f, SLAB * 2f - 0.1f, poolMat);
                Furnace.pool.SetWobble(0.6f);
                Furnace.haloCenterX = (WinX0 + WinX1) * 0.5f;
            }
        }

        /// <summary>Manomètre : cadran + aiguille (le pivot de l'aiguille est renvoyé dans needleOut[0]).</summary>
        void Gauge(Transform parent, Vector3 pos, float size, Transform[] needleOut)
        {
            Transform face = Glow("Gauge", parent, GFArt.GaugeFace, Color.white, pos, size);
            Transform pivot = new GameObject("Needle Pivot").transform;
            pivot.SetParent(face, false);
            pivot.localPosition = new Vector3(0f, 0f, -0.01f);
            pivot.localRotation = Quaternion.Euler(0f, 0f, 40f);
            BoxIn("Needle", pivot, new Vector3(0f, 0.18f, 0f), new Vector3(0.06f, 0.36f, 0.01f), GFMat.Lit(new Color(0.85f, 0.1f, 0.1f), 0f, 0.4f), false);
            if (needleOut != null && needleOut.Length > 0) needleOut[0] = pivot;
        }

        void BuildTank()
        {
            GameObject tg = new GameObject("Reservoir");
            tg.transform.SetParent(root, false);
            float cx = (TankX0 + TankX1) * 0.5f, cy = (TankY0 + TankY1) * 0.5f, w = TankX1 - TankX0, h = TankY1 - TankY0;
            tg.transform.localPosition = new Vector3(cx, cy, 0f);
            Tank = tg.AddComponent<GFTank>();
            Transform shell = Group("Reservoir Shell");
            Transform inside = Group("Reservoir Inside");

            BoxIn("Tank Base", shell, new Vector3(cx, TankY0 + 0.09f, 0f), new Vector3(w + 0.1f, 0.18f, 1.0f), mNavyPanel, false);
            BoxIn("Tank Cap L", shell, new Vector3(0.4f, TankY1, 0f), new Vector3(0.5f, 0.16f, 1.0f), mNavyPanel, false);
            BoxIn("Tank Cap R", shell, new Vector3(1.45f, TankY1, 0f), new Vector3(0.8f, 0.16f, 1.0f), mNavyPanel, false);   // ouverture 0,65–1,05 sous le bec du four
            BoxIn("Tank Post L", shell, new Vector3(TankX0, cy, -0.45f), new Vector3(0.14f, h, 0.14f), mDarkPanel, false);
            BoxIn("Tank Post R", shell, new Vector3(TankX1, cy, -0.45f), new Vector3(0.14f, h, 0.14f), mDarkPanel, false);
            BoxIn("Tank Back", shell, new Vector3(cx, cy, 0.45f), new Vector3(w, h, 0.05f), mDarkPanel, false);
            BoxIn("Tank Band", shell, new Vector3(cx, TankY0 + 0.25f, -0.47f), new Vector3(w, 0.08f, 0.03f), GFArt.HazardMat(w, 0.08f), false);
            // vitre légèrement bleutée + reflet
            Transform glass = Glow("Glass", shell, null, new Color(0.6f, 0.8f, 1f, 0.1f), new Vector3(cx, cy, -0.46f), 1f);
            glass.localScale = new Vector3(w - 0.1f, h - 0.2f, 1f);
            Transform shine = Glow("Glass Shine", shell, null, new Color(1f, 1f, 1f, 0.18f), new Vector3(cx - 0.35f, cy + 0.1f, -0.47f), 1f);
            shine.localScale = new Vector3(0.12f, h - 0.4f, 1f);
            shine.localRotation = Quaternion.Euler(0f, 0f, -12f);

            Tank.bottomY = TankY0 + 0.18f;
            Tank.maxH = 1.15f;
            Tank.liquid = new GFLiquid(inside, new Vector3(cx, Tank.bottomY, 0f), w - 0.2f, Tank.maxH, 0.8f, new Material(mMolten));
            Tank.liquid.SetWobble(0.8f);

            // tuyau coudé vers la buse au-dessus des moules
            float px = Slots[0], py = -3.1f;
            GameObject hp = Cyl("Pipe H", shell, new Vector3((TankX1 + px) * 0.5f, py, 0f), new Vector3(0.24f, (px - TankX1) * 0.5f + 0.05f, 0.24f), mChrome);
            hp.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            GameObject elbow = GFArt.NewShape(PrimitiveType.Sphere, "Elbow");
            elbow.name = "Elbow";
            elbow.transform.SetParent(shell, false);
            elbow.transform.localPosition = new Vector3(px, py, 0f);
            elbow.transform.localScale = Vector3.one * 0.3f;
            elbow.GetComponent<Renderer>().sharedMaterial = mChrome;
            Cyl("Pipe V", shell, new Vector3(px, py - 0.4f, 0f), new Vector3(0.24f, 0.4f, 0.24f), mChrome);
            Cyl("Nozzle", shell, new Vector3(px, py - 0.82f, 0f), new Vector3(0.32f, 0.06f, 0.32f), mAccent);
            Tank.nozzlePos = new Vector3(px, py - 0.88f, 0f);
            Tank.valveMat = GFMat.Lit(new Color(0.1f, 0.4f, 0.1f), 0.1f, 0.8f, Color.black);
            GameObject valve = Cyl("Valve", shell, new Vector3((TankX1 + px) * 0.5f, py + 0.2f, -0.05f), new Vector3(0.22f, 0.05f, 0.22f), Tank.valveMat);
            valve.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Transform[] needle = new Transform[1];
            Gauge(shell, new Vector3(TankX0 - 0.05f, TankY1 - 0.35f, -0.56f), 0.42f, needle);
            Tank.gaugeNeedle = needle[0];

            Tank.stream = new GFStream(inside, mMolten, 0.13f);
            Tank.halo = new GFHalo(inside, new Color(1f, 0.55f, 0.15f, 0.45f));
            Tap("Tap: Reservoir", new Vector3(cx + 0.3f, cy, 0f), new Vector3(w + 0.9f, 1.9f, 3f), delegate { tankPunch.Punch(); Tank.Tap(); });
            // image : 2,71 × 2,29 unités posée sur l'étagère ; ouverture ronde sous le bec du four (x 0,95),
            // buse jaune au-dessus du premier moule (x 2,7) ; la vitre est semi-transparente, l'or est derrière
            if (GFArt.Skin("reservoir", shell, new Vector2(1.465f, -2.425f), new Vector2(2.71f, 2.29f), -0.6f))
            {
                tg.transform.localPosition = new Vector3(1.2f, -2.52f, 0f);
                Tank.bottomY = -3.02f;
                Tank.maxH = 0.97f;
                Tank.liquid = new GFLiquid(inside, new Vector3(1.2f, Tank.bottomY, 0f), 1.34f, Tank.maxH, 0.8f, new Material(mMolten));
                Tank.liquid.SetWobble(0.8f);
                Tank.nozzlePos = new Vector3(Slots[0], -2.52f, 0f);
            }
            tankPunch = shell.gameObject.AddComponent<GFPunch>();
        }

        void BuildCarousel()
        {
            GameObject cg = new GameObject("Mold Carousel");
            cg.transform.SetParent(root, false);
            Carousel = cg.AddComponent<GFCarousel>();
            Carousel.slots = Slots;
            Carousel.molds = new GFMold[Slots.Length];

            Transform beltRoot = Group("Mold Conveyor");
            float bx0 = Slots[Slots.Length - 1] + 0.5f, bx1 = Slots[0] + 0.5f, by = MoldY - 0.24f, len = bx1 - bx0;
            Material chain = GFArt.Textured(GFArt.Chain, new Color(0.55f, 0.6f, 0.72f), new Vector2(len / 0.5f, 1f), 0.4f, 0.4f);
            BoxIn("Mold Belt", beltRoot, new Vector3((bx0 + bx1) * 0.5f, by, 0f), new Vector3(len, 0.12f, 0.8f), chain, false);
            GFBelt mb = beltRoot.gameObject.AddComponent<GFBelt>();
            mb.mat = chain;
            mb.Speed = -1.8f;
            mb.Running = false;
            mb.rollers = new[] { Roller(beltRoot, new Vector3(bx0, by, 0f)), Roller(beltRoot, new Vector3(bx1, by, 0f)) };
            ConveyorFrame(beltRoot, bx0, bx1, by, 0.14f);
            Carousel.belt = mb;
            BoxIn("Flip Arm", beltRoot, new Vector3(Slots[Slots.Length - 1], by - 0.02f, 0.32f), new Vector3(0.12f, 0.12f, 0.12f), mAccent, false);
                        GFArt.Skin("mold_conveyor", beltRoot, new Vector2(0.8f, -5.38f), new Vector2(4.8f, 0.573f), -0.8f);   // posé sur l'étagère (-5,67), surface à -5,15

            for (int i = 0; i < Slots.Length; i++)
            {
                GameObject m = new GameObject("Mold " + (i + 1));
                m.transform.SetParent(root, false);
                m.transform.localPosition = new Vector3(Slots[i], MoldY, 0f);
                Transform body = new GameObject("Body").transform;
                body.SetParent(m.transform, false);
                BoxIn("Mold Bottom", body, new Vector3(0f, -0.17f, 0f), new Vector3(0.9f, 0.08f, 0.6f), mDarkPanel, false);
                BoxIn("Mold L", body, new Vector3(-0.41f, 0f, 0f), new Vector3(0.08f, 0.34f, 0.6f), mDarkPanel, false);
                BoxIn("Mold R", body, new Vector3(0.41f, 0f, 0f), new Vector3(0.08f, 0.34f, 0.6f), mDarkPanel, false);
                BoxIn("Mold Back", body, new Vector3(0f, 0f, 0.28f), new Vector3(0.9f, 0.34f, 0.04f), mDarkPanel, false);
                BoxIn("Mold Front", body, new Vector3(0f, -0.1f, -0.29f), new Vector3(0.9f, 0.14f, 0.03f), mNavyPanel, false);

                GFMold mold = m.AddComponent<GFMold>();
                mold.body = body;
                mold.liquid = new GFLiquid(body, new Vector3(0f, -0.13f, 0f), 0.74f, 0.24f, 0.5f, new Material(mMolten));
                mold.halo = new GFHalo(root, new Color(1f, 0.55f, 0.15f, 0.5f));
                Carousel.molds[i] = mold;
                // image en deux calques : la cavité derrière l'or, le rebord avant devant
                Vector2 mc = new Vector2(Slots[i], MoldY - 0.035f), ms = new Vector2(0.92f, 0.35f);
                if (GFArt.Skin("mold", body, mc, ms, 0.3f))
                {
                    Transform front = new GameObject("Mold Front").transform;
                    front.SetParent(body, false);
                    GFArt.Skin("mold_front", front, mc, ms, -0.4f);
                    mold.liquid = new GFLiquid(body, new Vector3(0f, -0.06f, 0f), 0.66f, 0.16f, 0.5f, new Material(mMolten));
                    mold.liquidBottom = -0.06f;
                }
            }
        }

        void BuildBarsAndPress()
        {
            Transform beltRoot = Group("Bar Conveyor");
            float len = BarBeltX1 - BarBeltX0;
            Material chain = GFArt.Textured(GFArt.Chain, new Color(0.55f, 0.6f, 0.72f), new Vector2(len / 0.5f, 1f), 0.4f, 0.4f);
            BoxIn("Bar Belt", beltRoot, new Vector3((BarBeltX0 + BarBeltX1) * 0.5f, BarBeltY, 0f), new Vector3(len, 0.2f, 0.8f), chain, false);
            GFBelt bb = beltRoot.gameObject.AddComponent<GFBelt>();
            bb.mat = chain;
            bb.Speed = 1.5f;
            bb.rollers = new[] { Roller(beltRoot, new Vector3(BarBeltX0, BarBeltY, 0f)), Roller(beltRoot, new Vector3(BarBeltX1, BarBeltY, 0f)) };
            ConveyorFrame(beltRoot, BarBeltX0, BarBeltX1, BarBeltY, 0.2f);
            BoxIn("Anvil", beltRoot, new Vector3(PressX, BarBeltY, 0f), new Vector3(0.75f, 0.2f, 0.8f), mSteel, false);
            BoxIn("Anvil Stripe", beltRoot, new Vector3(PressX, BarBeltY - 0.04f, -0.42f), new Vector3(0.75f, 0.1f, 0.03f), GFArt.HazardMat(0.75f, 0.1f), false);
            GFArt.Skin("bar_conveyor", beltRoot, new Vector2(0.38f, -7.01f), new Vector2(4.5f, 0.84f), -0.8f);   // posé sur l'étagère (-7,43) ; le bloc gris sert de butée juste après la presse

            GameObject b = new GameObject("Bars");
            b.transform.SetParent(root, false);
            Bars = b.AddComponent<GFBars>();
            Bars.goldTemplate = mGold;
            Bars.beltTop = BarBeltY + 0.1f;
            Bars.pressX = PressX;
            Bars.belt = bb;

            // presse suspendue sous l'étagère des moules
            GameObject pg = new GameObject("Coin Press");
            pg.transform.SetParent(root, false);
            float housingY = -6.0f;   // fixé sur la face avant de l'étagère des moules   // sous l'étagère (épaisseur 0,4)
            pg.transform.localPosition = new Vector3(PressX, housingY, 0f);
            Press = pg.AddComponent<GFPress>();
            BoxIn("Press Housing", pg.transform, Vector3.zero, new Vector3(0.9f, 0.36f, 0.95f), mNavyPanel, false);
            BoxIn("Press Band", pg.transform, new Vector3(0f, -0.12f, -0.49f), new Vector3(0.9f, 0.1f, 0.03f), GFArt.HazardMat(0.9f, 0.1f), false);
            Press.lampMat = GFMat.Lit(new Color(0.1f, 0.5f, 0.1f), 0.1f, 0.8f, Color.black);
            GameObject lamp = GFArt.NewShape(PrimitiveType.Sphere, "Lamp");
            lamp.name = "Lamp";
            lamp.transform.SetParent(pg.transform, false);
            lamp.transform.localPosition = new Vector3(0.32f, 0.05f, -0.5f);
            lamp.transform.localScale = Vector3.one * 0.13f;
            lamp.GetComponent<Renderer>().sharedMaterial = Press.lampMat;

            // la tête est un objet à part (pas enfant du bâti) pour pouvoir avoir sa propre image
            Transform head = new GameObject("Press Head").transform;
            head.SetParent(root, false);
            Press.head = head;
            Press.restY = housingY;
            // en bas de course, la matrice (bas à -0,23 sous le centre de la tête) écrase le lingot posé sur l'enclume
            float barTop = BarBeltY + 0.1f + 0.22f;
            Press.slamY = barTop + 0.2f;
            head.localPosition = new Vector3(PressX, Press.restY, 0f);
            Cyl("Press Rod", head, new Vector3(0f, 0.35f, 0f), new Vector3(0.18f, 0.3f, 0.18f), mChrome);
            BoxIn("Press Block", head, Vector3.zero, new Vector3(0.78f, 0.36f, 0.85f), mNavyPanel, false);
            BoxIn("Press Collar", head, new Vector3(0f, 0.15f, -0.02f), new Vector3(0.8f, 0.08f, 0.88f), GFArt.HazardMat(0.8f, 0.08f), false);
            BoxIn("Press Die", head, new Vector3(0f, -0.2f, 0f), new Vector3(0.6f, 0.06f, 0.6f), mChrome, false);

            Tap("Tap: Press", new Vector3(PressX, BarBeltY + 0.5f, 0f), new Vector3(1.4f, 1.6f, 3f), delegate { Press.Tap(); });
            // bâti : 1,0 × 0,43 unité sous l'étagère ; il passe DEVANT la tête, qui sort de son anneau en frappant
            GFArt.Skin("press", pg.transform, new Vector2(PressX, housingY - 0.015f), new Vector2(1.0f, 0.43f), -0.85f);
            // tête : 0,6 de haut ; au repos elle est presque rentrée dans le bâti, en bas de course sa matrice touche le lingot
            if (GFArt.Skin("press_head", head, new Vector2(PressX, Press.restY), new Vector2(0.55f * 0.906f, 0.55f), -0.7f))
                Press.slamY = barTop - 0.03f + 0.275f;
        }

        void BuildVaultAndCoins()
        {
            Transform v = Group("Coin Vault");
            float cx = (VaultX0 + VaultX1) * 0.5f, w = VaultX1 - VaultX0, top = VaultFloor + 2.2f;   // parois invisibles hautes : les pièces restent dans le coffre
            BoxIn("Vault Floor", v, new Vector3(cx, VaultFloor - 0.1f, 0f), new Vector3(w + 0.3f, 0.2f, SLAB * 2f), mDarkPanel, true);
            BoxIn("Vault Wall L", v, new Vector3(VaultX0, (VaultFloor + top) * 0.5f, 0f), new Vector3(0.15f, top - VaultFloor, SLAB * 2f), mNavyPanel, true);
            BoxIn("Vault Wall R", v, new Vector3(VaultX1, (VaultFloor + top) * 0.5f, 0f), new Vector3(0.15f, top - VaultFloor, SLAB * 2f), mNavyPanel, true);
            BoxIn("Vault Back", v, new Vector3(cx, (VaultFloor + top) * 0.5f, SLAB - 0.03f), new Vector3(w, top - VaultFloor, 0.05f), mDarkPanel, false);
            BoxIn("Vault Front", v, new Vector3(cx, VaultFloor + 0.18f, -SLAB - 0.08f), new Vector3(w + 0.15f, 0.36f, 0.1f), mNavyPanel, false);
            BoxIn("Vault Stripe L", v, new Vector3(VaultX0 + 0.12f, VaultFloor + 0.18f, -SLAB - 0.14f), new Vector3(0.24f, 0.36f, 0.03f), GFArt.HazardMat(0.24f, 0.36f), false);
            BoxIn("Vault Stripe R", v, new Vector3(VaultX1 - 0.12f, VaultFloor + 0.18f, -SLAB - 0.14f), new Vector3(0.24f, 0.36f, 0.03f), GFArt.HazardMat(0.24f, 0.36f), false);
            // Les pièces (couche 8) ne touchent que le coffre (couche 9) et entre elles : elles ne s'accrochent plus aux tapis.
            BoxIn("Vault Glass F", v, new Vector3(cx, (VaultFloor + top) * 0.5f, -0.5f), new Vector3(w, top - VaultFloor, 0.1f), null, true);
            BoxIn("Vault Glass B", v, new Vector3(cx, (VaultFloor + top) * 0.5f, 0.5f), new Vector3(w, top - VaultFloor, 0.1f), null, true);
            foreach (Collider vc0 in v.GetComponentsInChildren<Collider>()) vc0.gameObject.layer = GFCoins.VaultLayer;
            for (int l = 0; l < 32; l++)
                if (l != GFCoins.VaultLayer) Physics.IgnoreLayerCollision(GFCoins.CoinLayer, l, true);
            Material green = GFMat.Lit(new Color(0.1f, 0.9f, 0.2f), 0.1f, 0.8f, new Color(0.1f, 0.9f, 0.2f));
            BoxIn("Vault Lamp", v, new Vector3(cx, VaultFloor + 0.18f, -SLAB - 0.15f), new Vector3(0.22f, 0.18f, 0.04f), green, false);
            // image en deux calques : le fond derrière les pièces, la face avant et les montants devant
            Vector2 vc = new Vector2(1.8f, -9.065f), vs = new Vector2(3.6f, 1.47f);
            if (GFArt.Skin("vault", v, vc, vs, 0.55f))
            {
                Transform front = new GameObject("Vault Front").transform;
                front.SetParent(v, false);
                GFArt.Skin("vault_front", front, vc, vs, -0.75f);
            }

            GameObject c = new GameObject("Coins");
            c.transform.SetParent(root, false);
            Coins = c.AddComponent<GFCoins>();
            Coins.rimY = VaultFloor + 0.75f;
            Coins.coinMat = GFMat.Lit(new Color(1f, 0.76f, 0.2f), 0.6f, 0.85f, new Color(0.25f, 0.16f, 0.02f));
            PhysicsMaterial pm = new PhysicsMaterial("Coin");
            pm.bounciness = 0.2f;
            pm.dynamicFriction = 0.4f;
            pm.staticFriction = 0.5f;
            pm.bounceCombine = PhysicsMaterialCombine.Maximum;
            Coins.physMat = pm;
        }

        void BuildOrePool()
        {
            PhysicsMaterial pm = new PhysicsMaterial("Ore");
            pm.dynamicFriction = 0.6f;
            pm.staticFriction = 0.7f;
            pm.bounciness = 0.15f;
            pm.bounceCombine = PhysicsMaterialCombine.Average;

            Transform holder = Group("Ore Pool");
            for (int i = 0; i < maxOre; i++)
            {
                GameObject g = new GameObject("Ore");
                g.transform.SetParent(holder, false);
                g.AddComponent<MeshFilter>().sharedMesh = GFArt.Rock(i);
                MeshRenderer mr = g.AddComponent<MeshRenderer>();
                BoxCollider bc = g.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.82f, 0.7f, 0.82f);
                bc.sharedMaterial = pm;
                Rigidbody rb = g.AddComponent<Rigidbody>();
                rb.mass = 1f;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.5f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                GFOre o = g.AddComponent<GFOre>();
                o.rb = rb;
                float tone = Random.value;
                Color c = Random.value < 0.15f
                    ? new Color(0.68f, 0.5f, 0.2f)                       // quelques pépites qui brillent
                    : Color.Lerp(GFArt.OreBrown * 0.75f, GFArt.OreBrown * 1.15f, tone);
                c.a = 1f;
                o.baseCol = c;
                o.mat = GFMat.Lit(o.baseCol, 0.1f, 0.3f);
                mr.sharedMaterial = o.mat;
                float s = Random.Range(0.3f, 0.4f);
                o.baseScale = new Vector3(s * Random.Range(0.9f, 1.15f), s * Random.Range(0.85f, 1.1f), s * Random.Range(0.9f, 1.15f));
                g.SetActive(false);
                orePool.Add(o);
            }
        }

        // ================================================================== boucle

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateOre(dt);

            if (Auto)
            {
                if (!Piston.Busy && Furnace.molten < 26f)
                {
                    int pile = PileCount();
                    if (pile >= pileTrigger || (pile >= 4 && Piston.idleTime > 9f)) Piston.Fire(false);
                }
                Furnace.TryPour(false);
            }

            GFSfx.I.SetPour(Furnace.Pouring || Tank.Pouring ? 0.45f : 0f);
            HandleInput();
            UpdateUI(dt);
            TickSave(dt);
            TickMeta(dt);
            TickTutorial(dt);
        }

        void UpdateOre(float dt)
        {
            // le tuyau attend quand le tapis est arrêté ou déjà chargé (sinon le minerai s'entasse sous le tuyau)
            int underPipe = 0;
            foreach (GFOre o in activeOre)
            {
                Vector3 q = o.transform.position;
                if (q.y > BeltY && Mathf.Abs(q.x - PipeX) < 1.1f) underPipe++;
                // un morceau endormi sur le tapis ne reçoit plus la poussée du tapis : on le réveille quand le tapis repart,
                // sinon le tas reste figé sous le tuyau et le tuyau, qui attend que la place se libère, ne verse plus rien
                if (Belt.Running && q.y > BeltY - 0.3f && q.x > BeltX0 - 0.3f && q.x < BeltX1 + 0.5f && o.rb.IsSleeping()) o.rb.WakeUp();
            }
            if (Belt.Running && underPipe < 8 && !TutoBlocksPipe) spawnAcc += oreRate * dt;
            while (spawnAcc >= 1f)
            {
                spawnAcc -= 1f;
                SpawnOre();
            }

            for (int i = activeOre.Count - 1; i >= 0; i--)
            {
                GFOre o = activeOre[i];
                Vector3 p = o.transform.position;
                if (!o.melting && Furnace.Contains(p))
                {
                    o.StartMelt(Furnace.MeltDuration);
                    GFSfx.I.PlayLimited(GFSfx.I.Sizzle, 0.35f, 0.25f, 0.08f);
                    GFFx.I.Sparks(p, 3, 1.5f);
                }
                if (o.melting && o.TickMelt(dt))
                {
                    Furnace.AddMolten(1f);
                    Recycle(o, i);
                    continue;
                }
                // goulotte invisible entre le bout de la plateforme et le mur : rien ne s'y coince, tout tombe dans le four
                if (!o.melting && p.x < PlatX0 + 0.1f && p.y > FurnTop && p.y < PlatY + 1.5f)
                {
                    Vector3 v = o.rb.linearVelocity;
                    v.y = Mathf.Min(v.y, -3f);
                    v.x = Mathf.Clamp(v.x, -0.6f, 0.6f);
                    o.rb.linearVelocity = v;
                }
                if (p.y < -12f || Mathf.Abs(p.x) > 7f) Recycle(o, i);
            }
        }

        void SpawnOre()
        {
            SpawnOreAt(new Vector3(PipeX + Random.Range(-0.15f, 0.15f), 8.4f, Random.Range(-0.25f, 0.25f)));
        }

        void SpawnOreAt(Vector3 pos)
        {
            if (orePool.Count == 0) return;
            GFOre o = orePool[orePool.Count - 1];
            orePool.RemoveAt(orePool.Count - 1);
            o.Spawn(pos);
            activeOre.Add(o);
        }

        // ================================================================== mode vidéo (pub)

        /// <summary>
        /// Mode vidéo : l'interface disparaît et la fonderie est mise d'un coup dans son état le plus spectaculaire
        /// (pile de minerai prête, four chaud, réservoir plein, lingots en route, coffre déjà garni, pluie de pièces doublée).
        /// On l'active en touchant le panneau « USINE 1 », on en sort en touchant le coin en haut à droite.
        /// </summary>
        void EnterVideo()
        {
            videoMode = true;
            titleOpen = false;
            StopTutorialForVideo();
            Auto = true;
            hintTimer = 0f;
            shopOpen = false;
            Press.coinsPerBar = Mathf.Max(14, Press.coinsPerBar);
            for (int i = 0; i < 18; i++)
                SpawnOreAt(new Vector3(Random.Range(0.5f, 2.6f), PlatY + 0.4f + Random.Range(0f, 0.8f), Random.Range(-0.25f, 0.25f)));
            Furnace.molten = Mathf.Max(Furnace.molten, 12f);
            Furnace.heat = 1.2f;
            Tank.Units = Tank.Capacity;
            float[] xs = { -1.2f, -0.2f, 0.8f };
            foreach (float x in xs) Bars.Spawn(new Vector3(x, Bars.beltTop + 0.6f, 0f), 0.6f);
            if (Coins.Frozen < 60) Coins.Prefill(90, VaultX0, VaultX1, VaultFloor);
            GFSfx.I.Play(GFSfx.I.Click, 0.6f, 1f);
        }

        void ExitVideo()
        {
            videoMode = false;
            ApplyUpgrades();
            GFSfx.I.Play(GFSfx.I.Click, 0.6f, 1f);
        }

        void Recycle(GFOre o, int index)
        {
            o.gameObject.SetActive(false);
            activeOre.RemoveAt(index);
            orePool.Add(o);
        }

        int PileCount()
        {
            int n = 0;
            foreach (GFOre o in activeOre)
            {
                Vector3 p = o.transform.position;
                if (!o.melting && p.y > PlatY && p.y < PlatY + 2.2f && p.x > PlatX0 && p.x < PlatX1) n++;
            }
            return n;
        }

        public void AddMoney(int n)
        {
            Money += n;
            moneyPop = 0f;
            income.Enqueue(new KeyValuePair<float, int>(Time.time, n));
            incomeWindow += n;
        }

        // ================================================================== micro-gestes

        void OnPipeTap()
        {
            pipePunch.Punch();
            OnTutoPipeTapped();
            GFSfx.I.PlayVar(GFSfx.I.Whoosh, 0.3f, 0.2f);
            for (int i = 0; i < 8; i++) SpawnOre();
        }

        void OnFurnaceTap()
        {
            furnacePunch.Punch();
            Furnace.Stoke();
        }

        void HandleInput()
        {
            Vector2 sp;
            if (!PointerDown(out sp)) return;

            Vector2 gui = new Vector2(sp.x, Screen.height - sp.y);   // IMGUI : origine en haut à gauche
            if (!videoMode && MetaInput()) return;
            if (videoMode)
            {
                if (gui.x > Screen.width * 0.75f && gui.y < Screen.height * 0.15f) { ExitVideo(); return; }
            }
            else if (!InTutorial && ShopInput(gui)) return;
            else if (facRect.Contains(gui))
            {
                EnterVideo();
                return;
            }
            else if (!InTutorial && autoRect.Contains(gui))
            {
                Auto = !Auto;
                GFSfx.I.Play(GFSfx.I.Click, 0.6f, 1f);
                return;
            }
            else if (soundRect.Contains(gui))
            {
                GFSfx.I.Muted = !GFSfx.I.Muted;
                GFSfx.I.Play(GFSfx.I.Click, 0.6f, 1f);
                return;
            }

            Ray ray = cam.ScreenPointToRay(sp);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
            GFTap best = null;
            float bestD = float.MaxValue;
            foreach (RaycastHit h in hits)
            {
                GFTap t = h.collider.GetComponent<GFTap>();
                if (t != null && h.distance < bestD) { best = t; bestD = h.distance; }
            }
            if (best != null && best.OnTap != null)
            {
                GFHaptics.Impact(0);
                GFSfx.I.Play(GFSfx.I.Click, 0.35f, 1f);
                best.OnTap();
                hintTimer = Mathf.Min(hintTimer, 1f);
            }
        }

        static bool PointerDown(out Vector2 pos)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            Pointer p = Pointer.current;
            if (p != null && p.press.wasPressedThisFrame)
            {
                pos = p.position.ReadValue();
                return true;
            }
            pos = Vector2.zero;
            return false;
#else
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began)
                {
                    pos = t.position;
                    return true;
                }
            }
            if (Input.touchCount == 0 && Input.GetMouseButtonDown(0))
            {
                pos = Input.mousePosition;
                return true;
            }
            pos = Vector2.zero;
            return false;
#endif
        }

        // ================================================================== interface (IMGUI)

        void UpdateUI(float dt)
        {
            hintTimer -= dt;
            if (moneyPop < 1f) moneyPop = Mathf.Min(1f, moneyPop + dt / 0.25f);
            while (income.Count > 0 && Time.time - income.Peek().Key > 10f) incomeWindow -= income.Dequeue().Value;

            float u = Mathf.Min(Screen.width / 1080f, Screen.height / 1920f);
            float bw = 300f * u, bh = 96f * u, m = 40f * u;
            autoRect = new Rect(m, Screen.height - bh - m, bw, bh);
            soundRect = new Rect(Screen.width - bw - m, Screen.height - bh - m, bw, bh);
            float safeTop = Screen.height - Screen.safeArea.yMax;
            facRect = new Rect(Screen.width - 36f * u - 260f * u, safeTop + 40f * u, 260f * u, 130f * u);
            LayoutShop(u, bw, bh, m);
            if (dt > 0f) fps = Mathf.Lerp(fps, 1f / dt, 0.05f);
        }

        static string Short(long n)
        {
            if (n >= 1000000) return (n / 1000000f).ToString("0.0") + "M";
            if (n >= 1000) return (n / 1000f).ToString("0.0") + "K";
            return n.ToString();
        }

        void OnGUI()
        {
            if (videoMode) return;   // mode vidéo : aucune interface à l'écran
            float u = Mathf.Min(Screen.width / 1080f, Screen.height / 1920f);
            if (panelStyle == null)
            {
                panelStyle = new GUIStyle(GUI.skin.box);
                panelStyle.normal.background = GFArt.RoundRect;
                panelStyle.border = new RectOffset(16, 16, 16, 16);
                bigStyle = new GUIStyle(GUI.skin.label);
                bigStyle.fontStyle = FontStyle.Bold;
                bigStyle.alignment = TextAnchor.MiddleLeft;
                smallStyle = new GUIStyle(bigStyle);
                hintStyle = new GUIStyle(GUI.skin.label);
                hintStyle.alignment = TextAnchor.MiddleCenter;
                hintStyle.wordWrap = true;
                buttonStyle = new GUIStyle(bigStyle);
                buttonStyle.alignment = TextAnchor.MiddleCenter;
            }

            if (titleOpen) { DrawMeta(u); return; }   // écran d'accueil : rien d'autre à l'écran

            Color panel = new Color(0.07f, 0.1f, 0.18f, 0.88f);
            Color gold = new Color(1f, 0.8f, 0.3f);
            float safeTop = Screen.height - Screen.safeArea.yMax;
            float top = safeTop + 40f * u;

            // argent (en haut à gauche)
            Rect money = new Rect(36f * u, top, 300f * u, 130f * u);
            Panel(money, panel);
            GUI.DrawTexture(new Rect(money.x + 20f * u, money.y + 18f * u, 56f * u, 56f * u), GFArt.CoinIcon);
            float pop = 1f + 0.15f * Mathf.Sin(moneyPop * Mathf.PI);
            bigStyle.fontSize = Mathf.RoundToInt(48f * u * pop);
            bigStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(money.x + 88f * u, money.y + 8f * u, 200f * u, 70f * u), Short(Money), bigStyle);
            smallStyle.fontSize = Mathf.RoundToInt(28f * u);
            smallStyle.normal.textColor = new Color(0.45f, 1f, 0.45f);
            GUI.Label(new Rect(money.x + 88f * u, money.y + 74f * u, 200f * u, 44f * u), "+" + (incomeWindow / 10f).ToString("0.0") + "/s", smallStyle);

            // usine + remplissage du coffre (en haut à droite)
            Rect fac = new Rect(Screen.width - 36f * u - 260f * u, top, 260f * u, 130f * u);
            Panel(fac, panel);
            buttonStyle.fontSize = Mathf.RoundToInt(36f * u);
            buttonStyle.normal.textColor = gold;
            GUI.Label(new Rect(fac.x, fac.y + 10f * u, fac.width, 56f * u), MaterialName, buttonStyle);
            Rect bar = new Rect(fac.x + 26f * u, fac.y + 80f * u, fac.width - 52f * u, 24f * u);
            Panel(bar, new Color(0f, 0f, 0f, 0.6f));
            Rect fill = bar;
            fill.width = Mathf.Max(bar.height, bar.width * Coins.VaultFill);
            Panel(fill, gold);

            // indice de départ
            float a = Mathf.Clamp01(hintTimer);
            if (a > 0f)
            {
                hintStyle.fontSize = Mathf.RoundToInt(34f * u);
                hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f * a);
                GUI.Label(new Rect(60f * u, top + 140f * u, Screen.width - 120f * u, 80f * u),
                    "Touchez les machines pour les lancer — AMÉLIORER pour les faire grandir", hintStyle);
            }

            // images par seconde (pour vérifier les performances sur iPhone)
            smallStyle.fontSize = Mathf.RoundToInt(26f * u);
            smallStyle.normal.textColor = fps >= 55f ? new Color(0.45f, 1f, 0.45f) : fps >= 40f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.35f);
            GUI.Label(new Rect(Screen.width * 0.5f - 60f * u, top + 4f * u, 160f * u, 40f * u), Mathf.RoundToInt(fps) + " FPS", smallStyle);

            // boutons
            buttonStyle.fontSize = Mathf.RoundToInt(38f * u);
            if (!InTutorial)
            {
                Panel(autoRect, panel);
                GUI.Label(autoRect, Auto ? "AUTO : ON" : "AUTO : OFF", buttonStyle);
            }
            Panel(soundRect, panel);
            GUI.Label(soundRect, GFSfx.I != null && GFSfx.I.Muted ? "SON : OFF" : "SON : ON", buttonStyle);

            if (!InTutorial) DrawShop(u, panel, gold);
            DrawMeta(u);
            DrawTutorial(u);
        }

        void Panel(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.Box(r, GUIContent.none, panelStyle);
            GUI.color = old;
        }

        // ================================================================== helpers de construction

        Transform Group(string name)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(root, false);
            return t;
        }

        /// <summary>Taille visible (largeur, hauteur) du plan z donné, pour un fond plein écran.</summary>
        Vector2 ViewSize(float z)
        {
            float h = cam.orthographic ? 2f * cam.orthographicSize
                : 2f * (z - cam.transform.position.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return new Vector2(h * Mathf.Max(cam.aspect, 0.4f), h);
        }

        static GameObject BoxIn(string name, Transform parent, Vector3 localPos, Vector3 size, Material m, bool collider)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = size;
            Renderer r = g.GetComponent<Renderer>();
            if (m != null) r.sharedMaterial = m;
            else r.enabled = false;
            if (!collider) Destroy(g.GetComponent<Collider>());
            return g;
        }

        static GameObject Cyl(string name, Transform parent, Vector3 localPos, Vector3 scale, Material m)
        {
            GameObject g = GFArt.NewShape(PrimitiveType.Cylinder, name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        /// <summary>Quad non éclairé et transparent (vitre, manomètre, reflet).</summary>
        static Transform Glow(string name, Transform parent, Texture2D tex, Color c, Vector3 localPos, float size)
        {
            GameObject q = GFArt.NewQuad(name);
            q.transform.SetParent(parent, false);
            q.transform.localPosition = localPos;
            q.transform.localScale = new Vector3(size, size, 1f);
            Material m = GFMat.Glow(c);
            m.mainTexture = tex;
            Renderer r = q.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return q.transform;
        }

        GameObject Tap(string name, Vector3 pos, Vector3 size, System.Action action)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(root, false);
            g.transform.localPosition = pos;
            BoxCollider bc = g.AddComponent<BoxCollider>();
            bc.size = size;
            bc.isTrigger = true;
            GFTap t = g.AddComponent<GFTap>();
            t.OnTap = action;
            return g;
        }
    }
}
