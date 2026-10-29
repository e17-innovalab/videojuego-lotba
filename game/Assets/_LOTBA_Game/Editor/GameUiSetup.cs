using System;
using UnityEditor;
using UnityEngine;
using static Lotba.Game.Editor.GameSceneBuilder;

namespace Lotba.Game.Editor
{
    /// <summary>Edits the existing HUD in place and adds the dedicated configuration panel.</summary>
    internal static class GameUiSetup
    {
        private static readonly Color Ink = new Color32(17, 43, 61, 250);
        private static readonly Color White = new Color32(249, 250, 242, 255);
        private static readonly Color Blue = new Color32(37, 76, 98, 255);
        internal static void Apply(GameHud hud, Camera camera)
        {
            PrepareUiAssets();
            Transform safe = hud.transform.Find("Portrait safe area");
            foreach (string path in new[] { "Race HUD/Lane hint", "Race HUD/Jump hint", "Touch controls/Left", "Touch controls/Right", "Touch controls/Jump", "Ready/Menu sound", "Ready/Menu effects", "Pause panel/Pause sound", "Pause panel/Pause effects" })
            { Transform item = safe.Find(path); if (item != null) UnityEngine.Object.DestroyImmediate(item.gameObject); }
            Place((RectTransform)safe.Find("Race HUD/Pickup feedback"), .18f, .53f, .82f, .65f);
            var viewport = hud.GetComponent<PortraitViewport>() ?? hud.gameObject.AddComponent<PortraitViewport>();
            Bind(viewport, "worldCamera", camera); Bind(viewport, "safeArea", safe);
            var ready = (RectTransform)safe.Find("Ready"); Place(ready, .065f, .18f, .935f, .86f);
            var title = ready.Find("Title").GetComponent<TMPro.TMP_Text>(); Place(title.rectTransform, .07f, .67f, .93f, .97f);
            Bind(hud, "gameTitle", title);
            var edition = ready.Find("Edition").GetComponent<TMPro.TMP_Text>(); Bind(hud, "developmentLabel", edition); Place(edition.rectTransform, .06f, .605f, .94f, .665f);
            var help = ready.Find("Instructions").GetComponent<TMPro.TMP_Text>();
            help.text = "CELULAR\nDeslizá a los lados para cambiar de carril.\nDeslizá hacia arriba para saltar.\n\nTECLADO\nA/D o ←/→ · W o ↑ para saltar.\nEspacio: frenar · P: pausa.";
            help.fontSize = 20; Place(help.rectTransform, .07f, .30f, .93f, .60f);
            var start = ready.Find("Start").GetComponent<UnityEngine.UI.Button>(); Caption(start, "EMPEZAR"); Place((RectTransform)start.transform, .07f, .17f, .93f, .285f);
            var mainConfig = EnsureButton("Configuration", ready, "CONFIGURACIÓN", .07f, .035f, .93f, .13f, 23);
            var pausePanel = safe.Find("Pause panel");
            var resume = (RectTransform)pausePanel.Find("Resume"); Place(resume, .08f, .40f, .92f, .59f);
            var pauseConfig = EnsureButton("Configuration", pausePanel, "CONFIGURACIÓN", .08f, .205f, .92f, .375f, 21);
            var pauseMenu = EnsureButton("Return to start", pausePanel, "VOLVER AL INICIO", .08f, .025f, .92f, .185f, 21);
            Bind(hud, "pauseMenu", pauseMenu);
            BindArray(hud, "configurationButtons", new[] { mainConfig, pauseConfig });
            Caption(safe.Find("Results/Retry").GetComponent<UnityEngine.UI.Button>(), "REINTENTAR");
            var resultText = safe.Find("Results/Result stats").GetComponent<TMPro.TMP_Text>(); resultText.enableAutoSizing = true; resultText.fontSizeMin = 20; resultText.fontSizeMax = 25;
            var settings = safe.Find("Settings") as RectTransform;
            if (settings == null) settings = Panel("Settings", safe, .055f, .17f, .945f, .91f, Ink);
            Bind(hud, "settingsPanel", settings.gameObject);
            EnsureLabel("Heading", settings, "CONFIGURACIÓN", .06f, .91f, .94f, .98f, 30);
            var sound = EnsureButton("Sound", settings, "SONIDO: SÍ", .07f, .81f, .93f, .89f, 22);
            var vibration = EnsureButton("Vibration", settings, "VIBRACIÓN", .07f, .71f, .93f, .79f, 20);
            var tilt = EnsureButton("Tilt", settings, "INCLINACIÓN", .07f, .61f, .93f, .69f, 20);
            Transform choices = settings.Find("Location options");
            if (choices == null) choices = Rect("Location options", settings, 0, 0, 1, 1);
            foreach (string old in new[] { "Location", "Obelisco", "La Boca" })
            {
                Transform item = settings.Find(old); if (item != null) item.SetParent(choices, false);
            }
            var location = EnsureLabel("Location", choices, "ESCENARIO: OBELISCO", .07f, .54f, .93f, .59f, 18);
            var obelisco = EnsureButton("Obelisco", choices, "OBELISCO", .07f, .44f, .93f, .52f, 20);
            var boca = EnsureButton("La Boca", choices, "LA BOCA", .07f, .34f, .93f, .42f, 20);
            var flow = EnsureButton("Flow", choices, "LA BOCA → OBELISCO", .07f, .24f, .93f, .32f, 19);
            var notice = EnsureLabel("Notice", settings, "", .08f, .11f, .92f, .22f, 17);
            var close = EnsureButton("Close", settings, "VOLVER", .07f, .02f, .93f, .10f, 23);
            Bind(hud, "soundButton", sound); Bind(hud, "soundLabel", sound.GetComponentInChildren<TMPro.TMP_Text>());
            Bind(hud, "vibrationButton", vibration); Bind(hud, "vibrationLabel", vibration.GetComponentInChildren<TMPro.TMP_Text>());
            Bind(hud, "tiltButton", tilt); Bind(hud, "tiltLabel", tilt.GetComponentInChildren<TMPro.TMP_Text>());
            Bind(hud, "obeliscoButton", obelisco); Bind(hud, "laBocaButton", boca); Bind(hud, "flowButton", flow);
            Bind(hud, "locationOptions", choices.gameObject); Bind(hud, "locationLabel", location); Bind(hud, "settingsNotice", notice); Bind(hud, "closeSettings", close);
            settings.gameObject.SetActive(false); ready.gameObject.SetActive(true);
            if (safe.Find("Edition label") != null) Bind(hud, "identityFooter", safe.Find("Edition label").GetComponent<TMPro.TMP_Text>());
            hud.ApplyIdentity();
        }
        private static void Caption(UnityEngine.UI.Button button, string text) => button.GetComponentInChildren<TMPro.TMP_Text>().text = text;
        private static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
        { rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1); rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static UnityEngine.UI.Button EnsureButton(string name, Transform parent, string caption, float x0, float y0, float x1, float y1, int size)
        {
            Transform found = parent.Find(name);
            if (found == null) return Button(name, parent, caption, x0, y0, x1, y1, Blue, size);
            var button = found.GetComponent<UnityEngine.UI.Button>(); Place((RectTransform)found, x0, y0, x1, y1); Caption(button, caption); return button;
        }
        private static TMPro.TMP_Text EnsureLabel(string name, Transform parent, string caption, float x0, float y0, float x1, float y1, int size)
        {
            Transform found = parent.Find(name);
            if (found == null) return Label(name, parent, caption, x0, y0, x1, y1, size, White);
            var label = found.GetComponent<TMPro.TMP_Text>(); label.text = caption; Place(label.rectTransform, x0, y0, x1, y1); return label;
        }
    }
}
