using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lotba.Game.Editor
{
    /// <summary>Shared obstacle wrappers, data-driven bus liveries and per-map traffic defaults.</summary>
    public static class ObstacleAssetSetup
    {
        private const string Root = PrototypeArt.Root + "/Obstacles";
        internal static void EnsureAssets(PrototypeArt art)
        {
            PrototypeArt.Folder(Root + "/Definitions"); PrototypeArt.Folder(Root + "/Prefabs/Art"); PrototypeArt.Folder(Root + "/Prefabs/Obstacles"); PrototypeArt.Folder(Root + "/Sets");
            var cone = Definition("cone", Wrap(art.LowObstacles[0], "Cone"), 1.15f, 1.22f, 1, true, 1.25f);
            var crate = Definition("crate", Wrap(art.LowObstacles[1], "Crate"), 1.7f, .9f, .75f, true, 1.05f);
            var cars = art.Vehicles.Select((model, index) => Definition("car_" + index, Wrap(model, model.name), 1.9f, 1.5f, 3.65f, false, 0)).ToArray();
            ObstacleView bus = Wrap(BusModel(art), "Bus");
            var bus46 = Definition("bus_46", bus, 2.15f, 3.25f, 8.8f, false, 0, "46", "2D78AF", "94D7EA");
            var bus91 = Definition("bus_91", bus, 2.15f, 3.25f, 8.8f, false, 0, "91", "3B945B", "B6D9AD");
            var bus36 = Definition("bus_36", bus, 2.15f, 3.25f, 8.8f, false, 0, "36", "D9423D", "E9D1B6");
            var low = new[] { cone, crate };
            var boca = Set("La Boca", low, cars.Concat(new[] { bus46 }).ToArray());
            var obelisco = Set("Obelisco", low, cars.Concat(new[] { bus91, bus36 }).ToArray());
            var transition = Set("Transition", low, cars);
            AssignMap("LaBoca/LaBoca.asset", boca); AssignMap("Obelisco/Obelisco.asset", obelisco);
            AssignMap("Transitions/LaBocaToObelisco/Transition.asset", transition);
        }
        public static void ApplyRequestedChanges()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before editing traffic.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first.");
            var art = new PrototypeArt(); art.Create(); EnsureAssets(art);
            string transitionFolder = PrototypeArt.Root + "/Maps/Transitions/LaBocaToObelisco";
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { transitionFolder + "/Prefabs/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (Transform child in contents.transform.Cast<Transform>().ToArray())
                        if (child.name == "Parked car") UnityEngine.Object.DestroyImmediate(child.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            var transitionMap = AssetDatabase.LoadAssetAtPath<MapDefinition>(transitionFolder + "/Transition.asset");
            var modules = Enumerable.Range(0, 3).Select(i => AssetDatabase.LoadAssetAtPath<RoadModule>(transitionFolder + "/Prefabs/Modules/Module " + i + ".prefab")).ToArray();
            var order = Enumerable.Repeat(modules[0], 2).Concat(Enumerable.Repeat(modules[1], 8)).Concat(Enumerable.Repeat(modules[2], 6)).ToArray();
            var mapData = new SerializedObject(transitionMap); var variants = mapData.FindProperty("modules"); variants.arraySize = order.Length;
            for (int i = 0; i < order.Length; i++) variants.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
            mapData.ApplyModifiedPropertiesWithoutUndo();
            var route = AssetDatabase.LoadAssetAtPath<RouteDefinition>(PrototypeArt.Root + "/Maps/Routes/Definitions/LaBocaToObelisco.asset");
            var routeData = new SerializedObject(route); routeData.FindProperty("sections").GetArrayElementAtIndex(1).FindPropertyRelative("moduleCount").intValue = 16; routeData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            MapAssetSetup.Apply();
        }
        private static ObstacleView Wrap(GameObject model, string name)
        {
            string path = Root + "/Prefabs/Obstacles/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<ObstacleView>(path); if (existing != null) return existing;
            var root = new GameObject(name + " obstacle"); var view = root.AddComponent<ObstacleView>();
            Transform artRoot = new GameObject("Art").transform; artRoot.SetParent(root.transform, false);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, artRoot); visual.name = "Visual";
            GameSceneBuilder.Bind(view, "artRoot", artRoot);
            return PrototypeArt.SavePrefab(root, path).GetComponent<ObstacleView>();
        }
        private static ObstacleDefinition Definition(string id, ObstacleView prefab, float width, float height, float length, bool jumpable, float clearance, string number = "", string body = "FFFFFF", string stripe = "FFFFFF")
        {
            string path = Root + "/Definitions/" + id + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<ObstacleDefinition>(path); if (existing != null) return existing;
            var definition = ScriptableObject.CreateInstance<ObstacleDefinition>(); AssetDatabase.CreateAsset(definition, path);
            var data = new SerializedObject(definition);
            data.FindProperty("id").stringValue = id; data.FindProperty("prefab").objectReferenceValue = prefab;
            data.FindProperty("width").floatValue = width; data.FindProperty("height").floatValue = height; data.FindProperty("length").floatValue = length;
            data.FindProperty("jumpable").boolValue = jumpable; data.FindProperty("jumpClearance").floatValue = clearance;
            var livery = data.FindProperty("livery"); livery.FindPropertyRelative("lineNumber").stringValue = number;
            ColorUtility.TryParseHtmlString("#" + body, out Color bodyColour); ColorUtility.TryParseHtmlString("#" + stripe, out Color stripeColour);
            livery.FindPropertyRelative("bodyColour").colorValue = bodyColour; livery.FindPropertyRelative("stripeColour").colorValue = stripeColour;
            data.ApplyModifiedPropertiesWithoutUndo(); return definition;
        }
        private static ObstacleSet Set(string name, ObstacleDefinition[] low, ObstacleDefinition[] traffic)
        {
            string path = Root + "/Sets/" + name + ".asset";
            var set = AssetDatabase.LoadAssetAtPath<ObstacleSet>(path); if (set != null) return set;
            set = ScriptableObject.CreateInstance<ObstacleSet>(); AssetDatabase.CreateAsset(set, path);
            GameSceneBuilder.BindArray(set, "low", low); GameSceneBuilder.BindArray(set, "traffic", traffic); return set;
        }
        private static void AssignMap(string relative, ObstacleSet set)
        {
            var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(PrototypeArt.Root + "/Maps/" + relative);
            if (map != null && map.Obstacles == null) GameSceneBuilder.Bind(map, "obstacles", set);
        }
        private static GameObject BusModel(PrototypeArt art)
        {
            string path = Root + "/Prefabs/Art/Shared bus.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var root = new GameObject("Shared bus visual"); var skin = root.AddComponent<BusSkin>();
            var body = new List<Renderer>(); var stripes = new List<Renderer>(); var numbers = new List<TMPro.TMP_Text>();
            Material blue = PrototypeArt.Material("Bus base blue", "2D78AF"), paleBlue = PrototypeArt.Material("Bus stripe blue", "94D7EA");
            body.Add(art.Primitive("Bus body", PrimitiveType.Cube, root.transform, new Vector3(0, 1.65f, 0), new Vector3(2.1f, 2.45f, 8.8f), blue).GetComponent<Renderer>());
            art.Primitive("Roof", PrimitiveType.Cube, root.transform, new Vector3(0, 3.1f, 0), new Vector3(2.08f, .2f, 8.65f), art.Pavement);
            foreach (int side in new[] { -1, 1 })
            {
                stripes.Add(art.Primitive("Long colour band", PrimitiveType.Cube, root.transform, new Vector3(side * 1.06f, 1.15f, 0), new Vector3(.04f, .4f, 8.75f), paleBlue).GetComponent<Renderer>());
                for (int window = 0; window < 6; window++)
                    art.Primitive("Side window", PrimitiveType.Cube, root.transform, new Vector3(side * 1.07f, 2.15f, -3.25f + window * 1.3f), new Vector3(.035f, 1.02f, 1.12f), art.Glass);
                foreach (float z in new[] { -2.8f, 2.7f })
                {
                    var tyre = art.Primitive("Wheel", PrimitiveType.Cylinder, root.transform, new Vector3(side * 1.04f, .52f, z), new Vector3(1, .12f, 1), art.Dark); tyre.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    var hub = art.Primitive("Wheel hub", PrimitiveType.Cylinder, root.transform, new Vector3(side * 1.17f, .52f, z), new Vector3(.5f, .015f, .5f), art.Pavement); hub.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
                art.Primitive("Mirror", PrimitiveType.Cube, root.transform, new Vector3(side * 1.2f, 2.2f, -3.9f), new Vector3(.2f, .45f, .18f), art.Dark);
                art.Primitive("Side route sign", PrimitiveType.Cube, root.transform, new Vector3(side * 1.075f, 2.89f, -2.5f), new Vector3(.025f, .34f, 1.3f), art.White);
                numbers.Add(Number(root.transform, new Vector3(side * 1.095f, 2.89f, -2.5f), Quaternion.Euler(0, side < 0 ? 90 : -90, 0), "Side line", new Vector2(1.2f, .3f)));
            }
            art.Primitive("Front door", PrimitiveType.Cube, root.transform, new Vector3(1.09f, 1.55f, -2.9f), new Vector3(.045f, 2.1f, .86f), art.Glass);
            foreach (int end in new[] { -1, 1 })
            {
                art.Primitive("Windscreen", PrimitiveType.Cube, root.transform, new Vector3(0, 2.1f, end * 4.415f), new Vector3(1.85f, 1.02f, .04f), art.Glass);
                art.Primitive("Route sign", PrimitiveType.Cube, root.transform, new Vector3(0, 2.92f, end * 4.43f), new Vector3(1.96f, .44f, .08f), art.White);
                numbers.Add(Number(root.transform, new Vector3(0, 2.92f, end * 4.485f), Quaternion.Euler(0, end < 0 ? 0 : 180, 0), "Line number", new Vector2(1.8f, .42f)));
                stripes.Add(art.Primitive("End colour band", PrimitiveType.Cube, root.transform, new Vector3(0, 1.12f, end * 4.42f), new Vector3(2.06f, .38f, .05f), paleBlue).GetComponent<Renderer>());
                art.Primitive("Bumper", PrimitiveType.Cube, root.transform, new Vector3(0, .58f, end * 4.46f), new Vector3(2.06f, .22f, .12f), art.Dark);
                foreach (float x in new[] { -.78f, .78f })
                    art.Primitive("Lamp", PrimitiveType.Sphere, root.transform, new Vector3(x, .9f, end * 4.47f), new Vector3(.23f, .23f, .06f), end < 0 ? art.White : PrototypeArt.Material("Bus rear lamps", "BE4237"));
            }
            GameSceneBuilder.BindArray(skin, "bodyPanels", body.ToArray()); GameSceneBuilder.BindArray(skin, "stripePanels", stripes.ToArray()); GameSceneBuilder.BindArray(skin, "numbers", numbers.ToArray());
            return PrototypeArt.SavePrefab(root, path);
        }
        private static TMPro.TMP_Text Number(Transform parent, Vector3 position, Quaternion rotation, string name, Vector2 size)
        {
            var label = new GameObject(name, typeof(TMPro.TextMeshPro)).GetComponent<TMPro.TextMeshPro>();
            label.transform.SetParent(parent, false); label.transform.SetLocalPositionAndRotation(position, rotation);
            label.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/ThirdParty/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            label.text = "46"; label.color = new Color(.08f, .12f, .15f); label.fontStyle = TMPro.FontStyles.Bold;
            label.alignment = TMPro.TextAlignmentOptions.Center; label.rectTransform.sizeDelta = size;
            label.enableAutoSizing = true; label.fontSizeMin = 1.5f; label.fontSizeMax = 4.2f; label.fontSize = 4.2f;
            label.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; return label;
        }
    }
}
