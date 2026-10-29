using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lotba.Game.Editor
{
    /// <summary>Creates the artist-facing module contract and preserves existing prefab/profile assets.</summary>
    public static class MapAssetSetup
    {
        private const string Root = PrototypeArt.Root + "/Maps";
        internal sealed class Profiles { internal RouteDefinition Obelisco, LaBoca, Flow; }
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before editing maps.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before updating maps.");
            MoveScenes();
            EditorSceneManager.OpenScene(GameSceneFlow.EntryPath);
            var art = new PrototypeArt(); art.Create(); Profiles profiles = EnsureProfiles(art); ObstacleAssetSetup.EnsureAssets(art);
            GameHud hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
            Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (PrefabUtility.IsPartOfPrefabInstance(hud.gameObject)) PrefabUtility.UnpackPrefabInstance(hud.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            GameUiSetup.Apply(hud, camera);
            var viewport = hud.GetComponent<PortraitViewport>();
            GameSceneBuilder.Bind(viewport, "worldCamera", null);
            PrefabUtility.SaveAsPrefabAssetAndConnect(hud.gameObject, PrototypeArt.Root + "/Prefabs/Game UI.prefab", InteractionMode.AutomatedAction);
            GameSceneBuilder.Bind(viewport, "worldCamera", camera); PrefabUtility.RecordPrefabInstancePropertyModifications(viewport);
            ConfigureWorld(UnityEngine.Object.FindFirstObjectByType<RunnerWorld>(), camera, profiles.Obelisco);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            SaveScenario(GameSceneFlow.ObeliscoPath, "Obelisco");
            SaveScenario(GameSceneFlow.LaBocaPath, "LaBoca");
            SaveScenario(GameSceneFlow.FlowPath, "LaBocaToObelisco");
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(GameSceneFlow.EntryPath, true), new EditorBuildSettingsScene(GameSceneFlow.ObeliscoPath, true),
                new EditorBuildSettingsScene(GameSceneFlow.LaBocaPath, true), new EditorBuildSettingsScene(GameSceneFlow.FlowPath, true)
            };
            AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene(GameSceneFlow.EntryPath);
            Debug.Log("Map profiles and continuous La Boca to Obelisco route saved. No Play Mode, tests or builds executed.");
        }
        internal static Profiles EnsureProfiles(PrototypeArt art)
        {
            PrototypeArt.Folder(Root + "/Routes/Definitions");
            var result = new Profiles {
                Obelisco = AssetDatabase.LoadAssetAtPath<RouteDefinition>(Root + "/Routes/Definitions/Obelisco.asset"),
                LaBoca = AssetDatabase.LoadAssetAtPath<RouteDefinition>(Root + "/Routes/Definitions/LaBoca.asset"),
                Flow = AssetDatabase.LoadAssetAtPath<RouteDefinition>(Root + "/Routes/Definitions/LaBocaToObelisco.asset")
            };
            if (result.Obelisco != null && result.LaBoca != null && result.Flow != null) return result;
            var location = new MapArtBuilder(); location.Create(art);
            GameObject road = SharedRoad(art);
            RoadModule[] boca = location.LaBocaModules.Select((p,i) => Wrap(p, road, Root + "/LaBoca/Prefabs/Modules/Module " + i + ".prefab")).ToArray();
            RoadModule[] obelisco = location.ObeliscoModules.Select((p,i) => Wrap(p, road, Root + "/Obelisco/Prefabs/Modules/Module " + i + ".prefab")).ToArray();
            GameObject[] transitionArt = TransitionArtBuilder.Create(art, location.TreeMesh());
            RoadModule[] transition = transitionArt.Select((p,i) => Wrap(p, road, Root + "/Transitions/LaBocaToObelisco/Prefabs/Modules/Module " + i + ".prefab")).ToArray();
            var bocaMap = Map(Root + "/LaBoca/LaBoca.asset", "La Boca", boca, AssetDatabase.LoadAssetAtPath<Material>(PrototypeArt.Root + "/Art/Materials/La Boca cobblestones.mat"), null, true);
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
        private static GameObject SharedRoad(PrototypeArt art)
        {
            string folder = Root + "/Shared/Prefabs"; PrototypeArt.Folder(folder); string path = folder + "/Road base.prefab";
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
            PrototypeArt.Combine(detail, "SharedRoadDetails"); return PrototypeArt.SavePrefab(root, path);
        }
        private static RoadModule Wrap(GameObject model, GameObject roadPrefab, string path)
        {
            PrototypeArt.Folder(path.Substring(0, path.LastIndexOf('/')));
            var existing = AssetDatabase.LoadAssetAtPath<RoadModule>(path); if (existing != null) return existing;
            var root = new GameObject(model.name + " standard module");
            var module = root.AddComponent<RoadModule>();
            var entry = new GameObject("Entry").transform; entry.SetParent(root.transform, false);
            var exit = new GameObject("Exit").transform; exit.SetParent(root.transform, false); exit.localPosition = Vector3.forward * RoadStandard.ModuleLength;
            var road = (GameObject)PrefabUtility.InstantiatePrefab(roadPrefab, root.transform); road.name = "Road";
            var art = new GameObject("Art").transform; art.SetParent(root.transform, false);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, art); visual.name = "Placeholder visual"; visual.transform.localPosition = Vector3.forward * 6;
            GameSceneBuilder.Bind(module, "artRoot", art); GameSceneBuilder.Bind(module, "entry", entry); GameSceneBuilder.Bind(module, "exit", exit);
            GameSceneBuilder.Bind(module, "roadSurface", road.transform.Find("Surface").GetComponent<Renderer>());
            return PrototypeArt.SavePrefab(root, path).GetComponent<RoadModule>();
        }
        private static MapDefinition Map(string path, string title, RoadModule[] modules, Material material, GameObject landmark, bool boca)
        {
            var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(path); if (map != null) return map;
            map = ScriptableObject.CreateInstance<MapDefinition>(); AssetDatabase.CreateAsset(map, path);
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
        private static RouteDefinition Route(string name, MapDefinition[] maps, int[] counts, bool[] transition)
        {
            string path = Root + "/Routes/Definitions/" + name + ".asset";
            var route = AssetDatabase.LoadAssetAtPath<RouteDefinition>(path); if (route != null) return route;
            route = ScriptableObject.CreateInstance<RouteDefinition>(); AssetDatabase.CreateAsset(route, path);
            var serialized = new SerializedObject(route); var sections = serialized.FindProperty("sections"); sections.arraySize = maps.Length;
            for (int i = 0; i < maps.Length; i++)
            {
                var section = sections.GetArrayElementAtIndex(i); section.FindPropertyRelative("map").objectReferenceValue = maps[i];
                section.FindPropertyRelative("moduleCount").intValue = counts[i]; section.FindPropertyRelative("transition").boolValue = transition[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); return route;
        }
        internal static void ConfigureWorld(RunnerWorld world, Camera camera, RouteDefinition route)
        {
            foreach (Transform child in world.transform.Cast<Transform>().ToArray())
                if (child.name.StartsWith("City block ", StringComparison.Ordinal) || child.name == "Location scenery" || child.name == "Distant skyline" || child.name == "Editor road preview") UnityEngine.Object.DestroyImmediate(child.gameObject);
            var stream = world.GetComponent<RoadStreamer>() ?? world.gameObject.AddComponent<RoadStreamer>();
            GameSceneBuilder.Bind(world, "roadStream", stream); GameSceneBuilder.Bind(stream, "route", route); GameSceneBuilder.Bind(stream, "worldCamera", camera);
            var preview = new GameObject("Editor road preview"); preview.tag = "EditorOnly"; preview.transform.SetParent(world.transform, false);
            for (int index = -2; index < 12; index++)
            {
                var prefab = route.ModuleAt(index);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, preview.transform);
                instance.transform.localPosition = Vector3.forward * (index * RoadStandard.ModuleLength);
                instance.GetComponent<RoadModule>().SetRoadMaterial(route.MapAtModule(index).RoadMaterial);
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
                start += section.ModuleCount * RoadStandard.ModuleLength;
            }
            GameSceneBuilder.Bind(stream, "editorPreview", preview);
            MapDefinition first = route.Sections[0].Map;
            RenderSettings.fogColor = first.Fog; RenderSettings.fogStartDistance = first.FogStart; RenderSettings.fogEndDistance = first.FogEnd;
            camera.backgroundColor = first.Sky; camera.farClipPlane = 185;
        }
        private static void MoveScenes()
        {
            PrototypeArt.Folder(Root + "/Obelisco/Scenes"); PrototypeArt.Folder(Root + "/LaBoca/Scenes"); PrototypeArt.Folder(Root + "/Routes/Scenes");
            Move(PrototypeArt.Root + "/Scenes/Obelisco.unity", GameSceneFlow.ObeliscoPath);
            Move(PrototypeArt.Root + "/Scenes/LaBoca.unity", GameSceneFlow.LaBocaPath);
        }
        private static void Move(string source, string target)
        {
            if (!System.IO.File.Exists(source) || System.IO.File.Exists(target)) return;
            string error = AssetDatabase.MoveAsset(source, target); if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        private static void SaveScenario(string path, string routeName)
        {
            EditorSceneManager.OpenScene(GameSceneFlow.EntryPath);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path, true); EditorSceneManager.OpenScene(path);
            // Loading a scene unloads unreferenced assets even if a managed local still points at them.
            // Carry asset paths across the scene boundary and resolve the route after opening it.
            var route = AssetDatabase.LoadAssetAtPath<RouteDefinition>(Root + "/Routes/Definitions/" + routeName + ".asset");
            ConfigureWorld(UnityEngine.Object.FindFirstObjectByType<RunnerWorld>(), UnityEngine.Object.FindFirstObjectByType<Camera>(), route);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
    }
}
