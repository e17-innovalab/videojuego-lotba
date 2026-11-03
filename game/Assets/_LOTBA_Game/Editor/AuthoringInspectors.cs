using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lotba.Game.Editor
{
    /// <summary>Inspector guidance only: never rebuilds or saves scenes behind the artist's back.</summary>
    internal static class AuthoringInspector
    {
        internal static VisualElement Create(UnityEditor.Editor editor, string instructions)
        {
            var root = new VisualElement();
            root.Add(new HelpBox(instructions, HelpBoxMessageType.Info));
            InspectorElement.FillDefaultInspector(root, editor.serializedObject, editor);
            return root;
        }

        internal static void AddValidation(VisualElement root, SerializedObject data, System.Func<string> error)
        {
            var message = new HelpBox();
            root.Add(message);
            void Refresh()
            {
                string text = error();
                message.text = text ?? "Configuration ready.";
                message.messageType = text == null ? HelpBoxMessageType.Info : HelpBoxMessageType.Error;
            }
            root.TrackSerializedObjectValue(data, _ => Refresh());
            // Dependencies can be edited in another Inspector while this one remains locked.
            root.schedule.Execute(Refresh).Every(1000);
            Refresh();
        }
    }

    [CustomEditor(typeof(RouteDefinition))]
    public sealed class RouteDefinitionInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var route = (RouteDefinition)target;
            var root = AuthoringInspector.Create(this,
                "Sections run in order. Each module is 12 m. Use a positive Module Count for intermediate sections and 0 (infinite) for the final section. A single zone with 0 needs no transition.");
            var summary = new HelpBox(); root.Add(summary);
            void Refresh()
            {
                var text = new StringBuilder();
                if (route.Sections != null)
                    foreach (var section in route.Sections)
                    {
                        if (section == null) continue;
                        text.AppendLine($"{section.Map?.DisplayName ?? "Unassigned"}: " +
                            (section.ModuleCount == 0 ? "infinite" : $"{section.ModuleCount} modules / {section.ModuleCount * RoadStandard.ModuleLength:0} m") +
                            (section.Transition ? " (transition)" : ""));
                    }
                summary.text = text.ToString();
            }
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh()); Refresh();
            AuthoringInspector.AddValidation(root, serializedObject, () => route.ValidationError);
            return root;
        }
    }

    [CustomEditor(typeof(MapDefinition))]
    public sealed class MapDefinitionInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = AuthoringInspector.Create(this,
                "Modules repeat in list order (A, B, C, A...). Drag module prefabs here. Edit their Art children in Prefab Mode; saving is enough. Obstacles come from the assigned catalog.");
            AuthoringInspector.AddValidation(root, serializedObject, () => ((MapDefinition)target).ValidationError);
            return root;
        }
    }

    [CustomEditor(typeof(RoadModule))]
    public sealed class RoadModuleInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = AuthoringInspector.Create(this,
                "Editable module: +Z forward, length 12 m, road width 9 m, surface Y=0. Replace children of Art. Keep root scale 1, Entry at Z=0 and Exit at Z=12. No mesh combining required.");
            AuthoringInspector.AddValidation(root, serializedObject, () => ((RoadModule)target).ValidationError);
            return root;
        }
    }

    [CustomEditor(typeof(ArtPreviewController))]
    public sealed class ArtPreviewInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var preview = (ArtPreviewController)target;
            var root = new VisualElement();
            root.Add(new HelpBox("Choose Route or Module before entering Play. A single-zone route repeats one neighborhood; a multi-zone route includes its transitions. A/D or arrows: lane; W/up: jump; P: pause. Save prefab edits outside Play. Restart Play after changing the content selection.", HelpBoxMessageType.Info));
            var mode = new PropertyField(serializedObject.FindProperty("mode")); root.Add(mode);
            var zone = new PropertyField(serializedObject.FindProperty("zone"), "Module Environment"); root.Add(zone);
            var route = new PropertyField(serializedObject.FindProperty("route")); root.Add(route);
            var module = new PropertyField(serializedObject.FindProperty("module")); root.Add(module);
            foreach (string name in new[] { "speed", "showRider", "showCoins", "showObstacles" })
                root.Add(new PropertyField(serializedObject.FindProperty(name)));
            var references = new Foldout { text = "Referencias internas", value = false };
            references.Add(new HelpBox("These connections are already configured. Settings supplies shared gameplay tuning: editing its asset also affects the main game. World animates the rider, camera and encounters. Road Stream recycles road modules and applies the route environment.", HelpBoxMessageType.Info));
            foreach (string name in new[] { "settings", "world", "roadStream" })
                references.Add(new PropertyField(serializedObject.FindProperty(name)));
            root.Add(references);
            void Refresh()
            {
                zone.style.display = preview.Mode == ArtPreviewMode.Route ? DisplayStyle.None : DisplayStyle.Flex;
                route.style.display = preview.Mode == ArtPreviewMode.Route ? DisplayStyle.Flex : DisplayStyle.None;
                module.style.display = preview.Mode == ArtPreviewMode.Module ? DisplayStyle.Flex : DisplayStyle.None;
            }
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh()); Refresh();
            AuthoringInspector.AddValidation(root, serializedObject, () => preview.ValidationError);
            return root;
        }
    }
}
