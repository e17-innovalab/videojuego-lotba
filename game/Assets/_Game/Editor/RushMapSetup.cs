using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lotba.CityRush.Editor
{
    /// <summary>Creates the artist-facing module contract and preserves existing prefab/profile assets.</summary>
    public static class RushMapSetup
    {
        private const string Root = RushArt.Root + "/Maps";
        internal sealed class Profiles { internal RushRouteDefinition Obelisco, LaBoca, Flow; }
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before editing maps.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before updating maps.");
            MoveScenes();
            EditorSceneManager.OpenScene(RushSceneFlow.EntryPath);
            var art = new RushArt(); art.Create(); Profiles profiles = EnsureProfiles(art); RushTransportSetup.EnsureAssets(art);
            RushHud hud = UnityEngine.Object.FindFirstObjectByType<RushHud>();
            Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (PrefabUtility.IsPartOfPrefabInstance(hud.gameObject)) PrefabUtility.UnpackPrefabInstance(hud.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            RushUiUpgrade.Apply(hud, camera);
            var viewport = hud.GetComponent<PortraitViewport>();
            RushSceneBuilder.Bind(viewport, "worldCamera", null);
            PrefabUtility.SaveAsPrefabAssetAndConnect(hud.gameObject, RushArt.Root + "/Prefabs/City Rush UI.prefab", InteractionMode.AutomatedAction);
            RushSceneBuilder.Bind(viewport, "worldCamera", camera); PrefabUtility.RecordPrefabInstancePropertyModifications(viewport);
            ConfigureWorld(UnityEngine.Object.FindFirstObjectByType<RushWorld>(), camera, profiles.Obelisco);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            SaveScenario(RushSceneFlow.ObeliscoPath, "Obelisco");
            SaveScenario(RushSceneFlow.LaBocaPath, "LaBoca");
            SaveScenario(RushSceneFlow.FlowPath, "LaBocaToObelisco");
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(RushSceneFlow.EntryPath, true), new EditorBuildSettingsScene(RushSceneFlow.ObeliscoPath, true),
                new EditorBuildSettingsScene(RushSceneFlow.LaBocaPath, true), new EditorBuildSettingsScene(RushSceneFlow.FlowPath, true)
            };
            AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene(RushSceneFlow.EntryPath);
            Debug.Log("City Rush map profiles and continuous La Boca to Obelisco route saved. No Play Mode, tests or builds executed.");
        }
        internal static Profiles EnsureProfiles(RushArt art)
        {
            RushArt.Folder(Root + "/Routes/Definitions");
            var result = new Profiles {
                Obelisco = AssetDatabase.LoadAssetAtPath<RushRouteDefinition>(Root + "/Routes/Definitions/Obelisco.asset"),
                LaBoca = AssetDatabase.LoadAssetAtPath<RushRouteDefinition>(Root + "/Routes/Definitions/LaBoca.asset"),
                Flow = AssetDatabase.LoadAssetAtPath<RushRouteDefinition>(Root + "/Routes/Definitions/LaBocaToObelisco.asset")
            };
            if (result.Obelisco != null && result.LaBoca != null && result.Flow != null) return result;
            var location = new RushLocationArt(); location.Create(art);
            GameObject road = SharedRoad(art);
            RushRoadModule[] boca = location.LaBocaModules.Select((p,i) => Wrap(p, road, Root + "/LaBoca/Prefabs/Modules/Module " + i + ".prefab")).ToArray();
            RushRoadModule[] obelisco = location.ObeliscoModules.Select((p,i) => Wrap(p, road, Root + "/Obelisco/Prefabs/Modules/Module " + i + ".prefab")).ToArray();
            GameObject[] transitionArt = RushTransitionArt.Create(art, location.TreeMesh());
            RushRoadModule[] transition = transitionArt.Select((p,i) => Wrap(p, road, Root + "/Transitions/LaBocaToObelisco/Prefabs/Modules/Module " + i + ".prefab")).ToArray();
            var bocaMap = Map(Root + "/LaBoca/LaBoca.asset", "La Boca", boca, AssetDatabase.LoadAssetAtPath<Material>(RushArt.Root + "/Art/Materials/La Boca cobblestones.mat"), null, true);
            var obeliscoMap = Map(Root + "/Obelisco/Obelisco.asset", "Obelisco", obelisco, art.Road, location.Obelisco, false);
            var transitionMap = Map(Root + "/Transitions/LaBocaToObelisco/Transition.asset", "Calle arbolada", Enumerable.Repeat(transition[0], 2).Concat(Enumerable.Repeat(transition[1], 8)).Concat(Enumerable.Repeat(transition[2], 6)).ToArray(), art.Road, null, true);
            result.Obelisco = Route("Obelisco", new[] { obeliscoMap }, new[] { 0 }, new[] { false });
            result.LaBoca = Route("LaBoca", new[] { bocaMap }, new[] { 0 }, new[] { false });
            result.Flow = Route("LaBocaToObelisco", new[] { bocaMap, transitionMap, obeliscoMap }, new[] { 30, 16, 0 }, new[] { false, true, false });
            AssetDatabase.SaveAssets();
            // Finish nested prefab imports before carrying component references across scene loads.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return result;
        }
        private static GameObject SharedRoad(RushArt art)
        {
            string folder = Root + "/Shared/Prefabs"; RushArt.Folder(folder); string path = folder + "/Road base.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var root = new GameObject("Road base");
            art.Primitive("Surface", PrimitiveType.Cube, root.transform, new Vector3(0, -.07f, 6), new Vector3(9, .14f, 12.04f), art.Road);
            var detail = new GameObject("Road details"); detail.transform.SetParent(root.transform, false);
            foreach (int side in new[] { -1, 1 })
            {
                art.Primitive("Sidewalk", PrimitiveType.Cube, detail.transform, new Vector3(side * 5.7f, -.005f, 6), new Vector3(2.4f, .28f, 12), art.Pavement);
                art.Primitive("Curb", PrimitiveType.Cube, detail.transform, new Vector3(side * 4.55f, .08f, 6), new Vector3(.12f, .24f, 12), art.White);
                foreach (float z in new[] { 3f, 9f }) art.Primitive("Central divider", PrimitiveType.Cube, detail.transform, new Vector3(side * 1.35f, .012f, z), new Vector3(.12f, .014f, 3.7f), art.White);
            }
            RushArt.Combine(detail, "SharedRoadDetails"); return RushArt.SavePrefab(root, path);
        }
        private static RushRoadModule Wrap(GameObject model, GameObject roadPrefab, string path)
        {
            RushArt.Folder(path.Substring(0, path.LastIndexOf('/')));
            var existing = AssetDatabase.LoadAssetAtPath<RushRoadModule>(path); if (existing != null) return existing;
            var root = new GameObject(model.name + " standard module");
            var module = root.AddComponent<RushRoadModule>();
            var entry = new GameObject("Entry").transform; entry.SetParent(root.transform, false);
            var exit = new GameObject("Exit").transform; exit.SetParent(root.transform, false); exit.localPosition = Vector3.forward * RushRoadStandard.ModuleLength;
            var road = (GameObject)PrefabUtility.InstantiatePrefab(roadPrefab, root.transform); road.name = "Road";
            var art = new GameObject("Art").transform; art.SetParent(root.transform, false);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, art); visual.name = "Placeholder visual"; visual.transform.localPosition = Vector3.forward * 6;
            RushSceneBuilder.Bind(module, "artRoot", art); RushSceneBuilder.Bind(module, "entry", entry); RushSceneBuilder.Bind(module, "exit", exit);
            RushSceneBuilder.Bind(module, "roadSurface", road.transform.Find("Surface").GetComponent<Renderer>());
            return RushArt.SavePrefab(root, path).GetComponent<RushRoadModule>();
        }
        private static RushMapDefinition Map(string path, string title, RushRoadModule[] modules, Material material, GameObject landmark, bool boca)
        {
            var map = AssetDatabase.LoadAssetAtPath<RushMapDefinition>(path); if (map != null) return map;
            map = ScriptableObject.CreateInstance<RushMapDefinition>(); AssetDatabase.CreateAsset(map, path);
            var serialized = new SerializedObject(map);
            serialized.FindProperty("displayName").stringValue = title;
            var variants = serialized.FindProperty("modules"); variants.arraySize = modules.Length;
            for (int i = 0; i < modules.Length; i++) variants.GetArrayElementAtIndex(i).objectReferenceValue = modules[i];
            serialized.FindProperty("roadMaterial").objectReferenceValue = material;
            serialized.FindProperty("landmark").objectReferenceValue = landmark;
            serialized.FindProperty("fog").colorValue = boca ? new Color(.66f,.78f,.79f) : new Color(.57f,.77f,.84f);
            serialized.FindProperty("fogStart").floatValue = boca ? 65 : 80; serialized.FindProperty("fogEnd").floatValue = boca ? 135 : 170;
            serialized.ApplyModifiedPropertiesWithoutUndo(); return map;
        }
        private static RushRouteDefinition Route(string name, RushMapDefinition[] maps, int[] counts, bool[] transition)
        {
            string path = Root + "/Routes/Definitions/" + name + ".asset";
            var route = AssetDatabase.LoadAssetAtPath<RushRouteDefinition>(path); if (route != null) return route;
            route = ScriptableObject.CreateInstance<RushRouteDefinition>(); AssetDatabase.CreateAsset(route, path);
            var serialized = new SerializedObject(route); var sections = serialized.FindProperty("sections"); sections.arraySize = maps.Length;
            for (int i = 0; i < maps.Length; i++)
            {
                var section = sections.GetArrayElementAtIndex(i); section.FindPropertyRelative("map").objectReferenceValue = maps[i];
                section.FindPropertyRelative("moduleCount").intValue = counts[i]; section.FindPropertyRelative("transition").boolValue = transition[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); return route;
        }
        internal static void ConfigureWorld(RushWorld world, Camera camera, RushRouteDefinition route)
        {
            foreach (Transform child in world.transform.Cast<Transform>().ToArray())
                if (child.name.StartsWith("City block ", StringComparison.Ordinal) || child.name == "Location scenery" || child.name == "Distant skyline" || child.name == "Editor road preview") UnityEngine.Object.DestroyImmediate(child.gameObject);
            var stream = world.GetComponent<RushRoadStream>() ?? world.gameObject.AddComponent<RushRoadStream>();
            RushSceneBuilder.Bind(world, "roadStream", stream); RushSceneBuilder.Bind(stream, "route", route); RushSceneBuilder.Bind(stream, "worldCamera", camera);
            var preview = new GameObject("Editor road preview"); preview.tag = "EditorOnly"; preview.transform.SetParent(world.transform, false);
            for (int index = -2; index < 12; index++)
            {
                var prefab = route.ModuleAt(index);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, preview.transform);
                instance.transform.localPosition = Vector3.forward * (index * RushRoadStandard.ModuleLength);
                instance.GetComponent<RushRoadModule>().SetRoadMaterial(route.MapAtModule(index).RoadMaterial);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform.Find("Road/Surface").GetComponent<Renderer>());
            }
            float start = 0;
            foreach (var section in route.Sections)
            {
                if (section.Map.Landmark != null)
                {
                    var landmark = (GameObject)PrefabUtility.InstantiatePrefab(section.Map.Landmark, preview.transform);
                    landmark.transform.localPosition = Vector3.forward * (start + section.Map.LandmarkDistance);
                }
                start += section.ModuleCount * RushRoadStandard.ModuleLength;
            }
            RushSceneBuilder.Bind(stream, "editorPreview", preview);
            RushMapDefinition first = route.Sections[0].Map;
            RenderSettings.fogColor = first.Fog; RenderSettings.fogStartDistance = first.FogStart; RenderSettings.fogEndDistance = first.FogEnd;
            camera.backgroundColor = first.Sky; camera.farClipPlane = 185;
        }
        private static void MoveScenes()
        {
            RushArt.Folder(Root + "/Obelisco/Scenes"); RushArt.Folder(Root + "/LaBoca/Scenes"); RushArt.Folder(Root + "/Routes/Scenes");
            Move(RushArt.Root + "/Scenes/Obelisco.unity", RushSceneFlow.ObeliscoPath);
            Move(RushArt.Root + "/Scenes/LaBoca.unity", RushSceneFlow.LaBocaPath);
        }
        private static void Move(string source, string target)
        {
            if (!System.IO.File.Exists(source) || System.IO.File.Exists(target)) return;
            string error = AssetDatabase.MoveAsset(source, target); if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        private static void SaveScenario(string path, string routeName)
        {
            EditorSceneManager.OpenScene(RushSceneFlow.EntryPath);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path, true); EditorSceneManager.OpenScene(path);
            // Loading a scene unloads unreferenced assets even if a managed local still points at them.
            // Carry asset paths across the scene boundary and resolve the route after opening it.
            var route = AssetDatabase.LoadAssetAtPath<RushRouteDefinition>(Root + "/Routes/Definitions/" + routeName + ".asset");
            ConfigureWorld(UnityEngine.Object.FindFirstObjectByType<RushWorld>(), UnityEngine.Object.FindFirstObjectByType<Camera>(), route);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
    }
}
