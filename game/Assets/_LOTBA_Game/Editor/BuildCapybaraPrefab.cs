using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lotba.Game.Editor
{
    /// <summary>Authors the editable low-poly capybara prefab as persistent Unity meshes.</summary>
    public static class BuildCapybaraPrefab
    {
        private const string Root = "Assets/_LOTBA_Game/Prefabs/Characters";
        private const string MeshRoot = "Assets/_LOTBA_Game/Art/Characters/Capybara";
        private const string PrefabPath = Root + "/Carpincho Tutorial.prefab";

        [MenuItem("LOTBA/Authoring/Rebuild Capybara Prefab")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(MeshRoot);
            AssetDatabase.Refresh();

            GameObject root = new GameObject("Carpincho Tutorial");
            Part(root.transform, "Cuerpo facetado", MeshRoot + "/Cuerpo.asset",
                Ellipsoid(0.51f, 0.56f, 0.78f, 2), new Vector3(0, 0.67f, 0),
                new Vector3(0.84f, 0.64f, 0.56f), Mat("Autumn orange"));
            Part(root.transform, "Pecho", MeshRoot + "/Pecho.asset", Ellipsoid(0.51f, 0.56f, 0.78f, 1),
                new Vector3(0, 0.49f, 0.37f), new Vector3(0.62f, 0.53f, 0.26f), Mat("Centro ivory"));

            GameObject head = new GameObject("Cabeza");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0, 1.14f, 0.42f);
            Part(head.transform, "Cabeza", MeshRoot + "/Cabeza.asset", Ellipsoid(0.51f, 0.56f, 0.78f, 1),
                Vector3.zero, new Vector3(0.52f, 0.56f, 0.49f), Mat("Autumn orange"));
            Part(head.transform, "Hocico", MeshRoot + "/Hocico.asset", Ellipsoid(0.51f, 0.56f, 0.78f, 1),
                new Vector3(0, -0.13f, 0.39f), new Vector3(0.35f, 0.28f, 0.43f), Mat("Facade sand"));
            Part(head.transform, "Nariz", MeshRoot + "/Nariz.asset", Ellipsoid(0.51f, 0.56f, 0.78f, 0),
                new Vector3(0, -0.005f, 0.73f), new Vector3(0.14f, 0.11f, 0.10f), Mat("Centro slate"));

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.255f;
                Part(head.transform, side < 0 ? "Oreja izquierda" : "Oreja derecha",
                    MeshRoot + (side < 0 ? "/Oreja izquierda.asset" : "/Oreja derecha.asset"),
                    Ellipsoid(0.51f, 0.56f, 0.78f, 0), new Vector3(x, 0.37f, -0.015f),
                    new Vector3(0.18f, 0.20f, 0.14f), Mat("Autumn orange"));
                Part(head.transform, side < 0 ? "Ojo izquierdo" : "Ojo derecho",
                    MeshRoot + (side < 0 ? "/Ojo izquierdo.asset" : "/Ojo derecho.asset"), Ellipsoid(1, 1, 1, 1),
                    new Vector3(x * 0.63f, 0.12f, 0.403f), new Vector3(0.105f, 0.12f, 0.065f), Mat("Centro ivory"));
                Part(head.transform, side < 0 ? "Pupila izquierda" : "Pupila derecha",
                    MeshRoot + (side < 0 ? "/Pupila izquierda.asset" : "/Pupila derecha.asset"), Ellipsoid(1, 1, 1, 0),
                    new Vector3(x * 0.63f, 0.105f, 0.459f), new Vector3(0.047f, 0.066f, 0.027f), Mat("Centro slate"));
            }

            Part(root.transform, "Pata delantera izquierda", MeshRoot + "/Pata delantera izquierda.asset",
                Ellipsoid(0.51f, 0.56f, 0.78f, 1), new Vector3(-0.34f, 0.17f, 0.32f), new Vector3(0.19f, 0.26f, 0.19f), Mat("Autumn orange"));
            Part(root.transform, "Pata delantera derecha", MeshRoot + "/Pata delantera derecha.asset",
                Ellipsoid(0.51f, 0.56f, 0.78f, 1), new Vector3(0.34f, 0.17f, 0.32f), new Vector3(0.19f, 0.26f, 0.19f), Mat("Autumn orange"));
            Part(root.transform, "Pata trasera izquierda", MeshRoot + "/Pata trasera izquierda.asset",
                Ellipsoid(0.51f, 0.56f, 0.78f, 1), new Vector3(-0.31f, 0.15f, -0.44f), new Vector3(0.24f, 0.23f, 0.25f), Mat("Autumn orange"));
            Part(root.transform, "Pata trasera derecha", MeshRoot + "/Pata trasera derecha.asset",
                Ellipsoid(0.51f, 0.56f, 0.78f, 1), new Vector3(0.31f, 0.15f, -0.44f), new Vector3(0.24f, 0.23f, 0.25f), Mat("Autumn orange"));

            GameObject helmet = new GameObject("Casco (opcional)");
            helmet.transform.SetParent(root.transform, false);
            helmet.transform.localPosition = new Vector3(0, 1.40f, 0.42f);
            helmet.SetActive(false);
            Part(helmet.transform, "Calota", MeshRoot + "/Casco.asset", HelmetMesh(), Vector3.zero,
                new Vector3(0.60f, 0.30f, 0.56f), Mat("Boca blue"));
            Part(helmet.transform, "Visera", MeshRoot + "/Visera.asset", Ellipsoid(1, 1, 1, 0),
                new Vector3(0, -0.09f, 0.31f), new Vector3(0.46f, 0.07f, 0.24f), Mat("Boca blue"));
            for (int i = -1; i <= 1; i++)
                Part(helmet.transform, "Ventilación " + (i + 2), MeshRoot + "/Ventilacion " + (i + 2) + ".asset",
                    Ellipsoid(1, 1, 1, 0), new Vector3(i * 0.17f, 0.17f, 0.12f),
                    new Vector3(0.055f, 0.018f, 0.16f), Mat("Centro slate"));

            CapybaraHelmetView view = root.AddComponent<CapybaraHelmetView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("helmetRoot").objectReferenceValue = helmet;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Carpincho low-poly prefab rebuilt at " + PrefabPath);
        }

        private static GameObject Part(Transform parent, string name, string path, Mesh mesh,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            MeshFilter filter = go.GetComponent<MeshFilter>();
            filter.sharedMesh = SaveMesh(mesh, path);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); }
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/_LOTBA_Game/Art/Materials/" + name + ".mat");

        private static Mesh Ellipsoid(float rx, float ry, float rz, int detail)
        {
            int rings = detail == 0 ? 5 : detail == 1 ? 7 : 9;
            int segments = detail == 0 ? 6 : detail == 1 ? 8 : 10;
            var vertices = new Vector3[2 + (rings - 1) * segments];
            var triangles = new int[rings * segments * 6];
            vertices[0] = new Vector3(0, ry, 0);
            vertices[1] = new Vector3(0, -ry, 0);
            for (int r = 1; r < rings; r++)
            {
                float phi = Mathf.PI * r / rings;
                for (int s = 0; s < segments; s++)
                {
                    float theta = 2 * Mathf.PI * s / segments;
                    vertices[2 + (r - 1) * segments + s] = new Vector3(
                        rx * Mathf.Sin(phi) * Mathf.Cos(theta), ry * Mathf.Cos(phi),
                        rz * Mathf.Sin(phi) * Mathf.Sin(theta));
                }
            }
            int t = 0;
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                triangles[t++] = 0; triangles[t++] = 2 + s; triangles[t++] = 2 + next;
                int lastRing = 2 + (rings - 2) * segments;
                triangles[t++] = 1; triangles[t++] = lastRing + next; triangles[t++] = lastRing + s;
            }
            for (int r = 0; r < rings - 2; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = 2 + r * segments + s, b = 2 + r * segments + (s + 1) % segments;
                    int c = a + segments, d = b + segments;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            var mesh = new Mesh { name = "Malla low-poly" };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh HelmetMesh()
        {
            Mesh mesh = Ellipsoid(1, 1, 1, 1);
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i].y = Mathf.Max(-0.12f, vertices[i].y);
            mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}

