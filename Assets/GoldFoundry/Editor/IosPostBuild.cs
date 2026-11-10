using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

namespace GoldFoundry.EditorTools
{
    /// <summary>
    /// Après un build iOS, désactive les vérifications récentes de Xcode qui rejettent le projet
    /// généré par Unity (même correctif que Fronton / Une coupe).
    /// </summary>
    public static class IosPostBuild
    {
        [PostProcessBuild(1000)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string pbx = Path.Combine(path, "Unity-iPhone.xcodeproj", "project.pbxproj");
            if (!File.Exists(pbx)) return;
            string s = File.ReadAllText(pbx);
            s = s.Replace("ENABLE_USER_SCRIPT_SANDBOXING = YES;", "ENABLE_USER_SCRIPT_SANDBOXING = NO;")
                 .Replace("ENABLE_MODULE_VERIFIER = YES;", "ENABLE_MODULE_VERIFIER = NO;")
                 .Replace("CLANG_WARN_QUOTED_INCLUDE_IN_FRAMEWORK_HEADER = YES;", "CLANG_WARN_QUOTED_INCLUDE_IN_FRAMEWORK_HEADER = NO;");
            File.WriteAllText(pbx, s);
            UnityEngine.Debug.Log("[Gold Foundry] Projet Xcode corrigé pour la version actuelle de Xcode.");
        }
    }
}
