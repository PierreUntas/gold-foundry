using UnityEngine;

namespace GoldFoundry
{
    /// <summary>
    /// Démarre la fonderie dans n'importe quelle scène : appuyez sur Play, tout est construit par le code.
    /// (Si la scène contient déjà un GFFactory, rien n'est ajouté.)
    /// </summary>
    public static class GFBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<GFFactory>() != null) return;
            new GameObject("Gold Foundry").AddComponent<GFFactory>();
        }
    }
}
