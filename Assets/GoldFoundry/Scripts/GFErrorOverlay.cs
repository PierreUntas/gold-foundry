using System.Collections.Generic;
using UnityEngine;

namespace GoldFoundry
{
    /// <summary>
    /// Affiche à l'écran, en rouge, les erreurs et exceptions du jeu (utile sur iPhone, où la console n'est pas visible).
    /// Créé avant le chargement de la scène, donc actif même si la fonderie plante au démarrage.
    /// </summary>
    public class GFErrorOverlay : MonoBehaviour
    {
        static readonly List<string> lines = new List<string>();
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            lines.Clear();
            GameObject go = new GameObject("Error Overlay");
            DontDestroyOnLoad(go);
            go.AddComponent<GFErrorOverlay>();
            Application.logMessageReceived += OnLog;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string first = stack;
            if (!string.IsNullOrEmpty(stack))
            {
                string[] parts = stack.Split('\n');
                first = parts.Length > 0 ? parts[0] : stack;
                if (parts.Length > 1) first += "\n" + parts[1];
            }
            lines.Add(message + "\n" + first);
            if (lines.Count > 4) lines.RemoveAt(0);
        }

        void OnGUI()
        {
            if (lines.Count == 0) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.wordWrap = true;
                style.normal.textColor = new Color(1f, 0.45f, 0.4f);
            }
            style.fontSize = Mathf.Max(14, Screen.height / 70);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            Rect r = new Rect(10, Screen.height * 0.3f, Screen.width - 20, Screen.height * 0.45f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 10, r.y + 10, r.width - 20, r.height - 20), string.Join("\n\n", lines.ToArray()), style);
        }
    }
}
