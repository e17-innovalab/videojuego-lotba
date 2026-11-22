using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Cosmetic impact and prefab scatter only. Driven independently of simulation pauses.</summary>
    public sealed class ImpactPresentation
    {
        private Transform impactVisualRoot;
        private float impactReactionStarted = float.NegativeInfinity;
        private float impactReactionSide;
        private float recoveryPresentationStarted = float.NegativeInfinity;
        private float presentationDuration;
        private const float ImpactReactionSeconds = .55f;
        public bool IsComplete(float now) => now - recoveryPresentationStarted >= presentationDuration;
        public void UpdatePresentation(float now)
        {
            UpdateGemScatter(now);
            UpdateImpactReaction(now);
        }
        private const int Capacity = 12;
        private readonly Transform[] scatteredGems = new Transform[Capacity];
        private readonly Vector3[] scatterTrajectories = new Vector3[Capacity];
        private readonly Vector3[] scatterSpins = new Vector3[Capacity];
        private readonly System.Random scatterRandom = new System.Random();
        private Vector3 gemScatterOrigin;
        private float gemScatterStarted;
        private int scatteredGemCount;
        public void ClearGemScatter()
        {
            for (int i = 0; i < scatteredGemCount; i++)
                scatteredGems[i].gameObject.SetActive(false);
            scatteredGemCount = 0;
        }

        public void ScatterGems(int count, GameObject gemPrefab, Transform parent, Vector3 origin, float now)
        {
            ClearGemScatter();
            if (gemPrefab == null) return;
            count = Mathf.Clamp(count, 0, Capacity);
            gemScatterOrigin = origin;
            gemScatterStarted = now;
            for (int i = 0; i < count; i++)
            {
                // Jitter each sector of the burst so trajectories spread without forming rows.
                float angle = 2f * Mathf.PI * (i + (float)scatterRandom.NextDouble()) / count;
                float reach = Mathf.Lerp(1.4f, 4f, (float)scatterRandom.NextDouble());
                scatterTrajectories[i] = new Vector3(Mathf.Cos(angle) * reach,
                    Mathf.Lerp(.8f, 1.8f, (float)scatterRandom.NextDouble()), Mathf.Sin(angle) * reach - .8f);
                scatterSpins[i] = new Vector3(scatterRandom.Next(-300, 301),
                    scatterRandom.Next(-540, 541), scatterRandom.Next(-180, 181));
                if (scatteredGems[i] == null) scatteredGems[i] = Object.Instantiate(gemPrefab, parent).transform;
                scatteredGems[i].SetLocalPositionAndRotation(gemScatterOrigin, Quaternion.identity);
                scatteredGems[i].gameObject.SetActive(true);
            }
            scatteredGemCount = count;
        }

        private void UpdateGemScatter(float now)
        {
            if (scatteredGemCount == 0) return;
            float gemScatterTime = now - gemScatterStarted;
            if (gemScatterTime >= .8f)
            {
                ClearGemScatter();
                return;
            }

            float t = gemScatterTime / .8f;
            float hop = Mathf.Abs(Mathf.Sin(t * 2f * Mathf.PI)) * (1f - t);
            for (int i = 0; i < scatteredGemCount; i++)
            {
                Vector3 trajectory = scatterTrajectories[i];
                Vector3 offset = new Vector3(trajectory.x * t, trajectory.y * hop - .7f * t, trajectory.z * t);
                scatteredGems[i].SetLocalPositionAndRotation(gemScatterOrigin + offset, Quaternion.Euler(scatterSpins[i] * t));
            }
        }

        public void StartImpactReaction(Transform target, bool reducedMotion, float side, float duration, float now)
        {
            ResetImpactReaction();
            impactVisualRoot = target;
            presentationDuration = duration;
            recoveryPresentationStarted = now;
            if (reducedMotion) { ResetImpactReaction(); return; }
            impactReactionStarted = now;
            impactReactionSide = side;
        }

        public void ResetImpactReaction()
        {
            impactReactionStarted = float.NegativeInfinity;
            if (impactVisualRoot != null)
                impactVisualRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private void UpdateImpactReaction(float now)
        {
            if (impactVisualRoot == null || float.IsNegativeInfinity(impactReactionStarted)) return;
            float t = Mathf.Clamp01((now - impactReactionStarted) / ImpactReactionSeconds);
            if (t >= 1f) { ResetImpactReaction(); return; }
            // Cosmetic recoil on its own pivot; the collider, track and gameplay clock stay still.
            float kick = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .28f)) * (1f - t);
            float settle = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
            impactVisualRoot.SetLocalPositionAndRotation(new Vector3(
                .035f * impactReactionSide * settle, -.035f * kick, -.14f * kick),
                Quaternion.Euler(-11f * kick, 3f * impactReactionSide * settle, 7f * impactReactionSide * settle));
        }
    }
}
