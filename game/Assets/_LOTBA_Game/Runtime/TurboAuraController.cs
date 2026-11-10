using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Visual only. The run session owns the impulse and its duration.</summary>
    public sealed class TurboAuraController : MonoBehaviour
    {
        private static readonly int PhaseId = Shader.PropertyToID("_Phase");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private MeshRenderer auraRenderer;
        private Material material;
        private Mesh mesh;
        private float intensity;
        private float phase;

        public void Initialize(Shader shader, Texture2D texture)
        {
            material = new Material(shader) { mainTexture = texture };
            mesh = CreateMesh();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            auraRenderer = gameObject.AddComponent<MeshRenderer>();
            auraRenderer.sharedMaterial = material;
            auraRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            auraRenderer.receiveShadows = false;
            auraRenderer.enabled = false;
        }

        public void Advance(bool active, bool reducedMotion, float dt, GameSettings settings)
        {
            if (!active && intensity == 0f) return;
            if (!reducedMotion) phase += dt;
            float target = active ? settings.TurboAuraIntensity * (reducedMotion ? .36f : 1f) : 0f;
            intensity = Mathf.MoveTowards(intensity, target, dt * settings.TurboAuraFadeRate);
            material.SetFloat(PhaseId, phase);
            material.SetFloat(IntensityId, intensity);
            auraRenderer.enabled = intensity > 0f;
        }

        public void Clear()
        {
            intensity = phase = 0f;
            auraRenderer.enabled = false;
        }

        private static Mesh CreateMesh()
        {
            const int sides = 32;
            const int rings = 8;
            const float roundedFront = .375f;
            const int verticesPerLayer = 1 + sides * rings;
            var vertices = new Vector3[verticesPerLayer * 2];
            var uv = new Vector2[vertices.Length];
            var layerUv = new Vector2[vertices.Length];
            var triangles = new int[2 * sides * (3 + (rings - 1) * 6)];
            var tip = new Vector3(0f, 1.25f, 2.9f);
            var center = new Vector2(.5f, .5f);
            int index = 0;
            for (int layer = 0; layer < 2; layer++)
            {
                int first = layer * verticesPerLayer;
                vertices[first] = tip;
                uv[first] = center;
                layerUv[first] = new Vector2(layer, 0f);
                for (int ring = 0; ring < rings; ring++)
                {
                    float radius = (ring + 1f) / rings;
                    float nose = Mathf.Min(radius / roundedFront, 1f);
                    // Start with a rounded cap and join the straight cone with a matching slope.
                    float depth = radius - roundedFront * nose * (1f - nose) * (1f - nose);
                    for (int side = 0; side < sides; side++)
                    {
                        float angle = side * Mathf.PI * 2f / sides;
                        var radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        int vertex = first + 1 + ring * sides + side;
                        int next = vertex - side + (side + 1) % sides;
                        vertices[vertex] = tip + new Vector3(radial.x * 2.31f * radius, radial.y * 1.652f * radius, -3.85f * depth);
                        uv[vertex] = center + radial * (.5f * radius);
                        layerUv[vertex] = new Vector2(layer, 0f);
                        triangles[index++] = ring == 0 ? first : vertex - sides;
                        triangles[index++] = vertex;
                        triangles[index++] = next;
                        if (ring == 0) continue;
                        triangles[index++] = vertex - sides;
                        triangles[index++] = next;
                        triangles[index++] = next - sides;
                    }
                }
            }

            var result = new Mesh { name = "Turbo aura", vertices = vertices, uv = uv, uv2 = layerUv, triangles = triangles };
            // Leave room for the shell scale and vertex displacement.
            var bounds = result.bounds;
            bounds.Expand(.65f);
            result.bounds = bounds;
            result.UploadMeshData(true);
            return result;
        }

        private void OnDestroy()
        {
            Destroy(mesh);
            Destroy(material);
        }
    }
}
