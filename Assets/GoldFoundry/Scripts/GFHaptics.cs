using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace GoldFoundry
{
    /// <summary>
    /// Vibrations courtes de l'iPhone (Taptic Engine), via Plugins/iOS/GFHaptics.mm.
    /// 0 = léger (toucher), 1 = moyen (presse, achat), 2 = fort (piston, coffre plein).
    /// Dans l'éditeur et sur les autres plateformes, ne fait rien.
    /// </summary>
    public static class GFHaptics
    {
        public static bool Enabled = true;
        static readonly float[] last = { -1f, -1f, -1f };

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void GFHapticImpact(int style);
#endif

        public static void Impact(int style)
        {
            if (!Enabled) return;
            style = Mathf.Clamp(style, 0, 2);
            if (Time.unscaledTime - last[style] < 0.07f) return;   // pas de mitraillage
            last[style] = Time.unscaledTime;
#if UNITY_IOS && !UNITY_EDITOR
            GFHapticImpact(style);
#endif
        }
    }
}
