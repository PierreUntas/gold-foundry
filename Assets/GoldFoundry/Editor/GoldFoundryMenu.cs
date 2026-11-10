using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GoldFoundry.EditorTools
{
    /// <summary>
    /// Au premier chargement du projet : règle les Player Settings (nom, identifiant iOS, portrait)
    /// et crée la scène Assets/GoldFoundry/Main.unity, ajoutée aux Build Profiles.
    /// </summary>
    [InitializeOnLoad]
    public static class GoldFoundryMenu
    {
        const string ScenePath = "Assets/GoldFoundry/Main.unity";

        static GoldFoundryMenu()
        {
            EditorApplication.delayCall += FirstOpen;
        }

        static void FirstOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (PlayerSettings.productName != "Gold Foundry")
            {
                PlayerSettings.productName = "Gold Foundry";
                PlayerSettings.companyName = "Pierre Untas";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.pierre.goldfoundry");
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            }
            ApplyIcon();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                var active = EditorSceneManager.GetActiveScene();
                if (active.isDirty || !string.IsNullOrEmpty(active.path)) return; // ne touche pas une scène en cours
                CreateScene();
            }
        }

        const string IconPath = "Assets/GoldFoundry/Icon/icon.png";

        /// <summary>Si Assets/GoldFoundry/Icon/icon.png existe (1024×1024), il devient l'icône de l'app.</summary>
        [MenuItem("Gold Foundry/Appliquer l'icône")]
        public static void ApplyIcon()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null) return;
            Texture2D[] current = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (current != null && current.Length > 0 && current[0] == icon) return;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            Debug.Log("[Gold Foundry] Icône de l'app appliquée : " + IconPath);
        }

        [MenuItem("Gold Foundry/Effacer la sauvegarde")]
        public static void ClearSave()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[Gold Foundry] Sauvegarde effacée : argent et améliorations remis à zéro.");
        }

        [MenuItem("Gold Foundry/Recréer la scène")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Gold Foundry").AddComponent<GFFactory>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[Gold Foundry] Scène prête : " + ScenePath + " — appuyez sur Play.");
        }
    }
}
