using UnityEngine;

namespace BridgeQuest
{
    /// <summary>
    /// Simple ambient "juice" — drifts a handful of cloud images left to right at a slow,
    /// constant speed, each one wrapping back around to the left the moment it scrolls fully
    /// past the right edge of the canvas. Generic and mission-agnostic — the same component is
    /// placed under every story's scene-art parent (CrowStory_Parent, RabbitandToriiseRace_Parent,
    /// MouseAndLion_Parent), each with its own clouds[] wired to whatever cloud images live
    /// under that parent.
    /// </summary>
    public class DriftingCloudsController : MonoBehaviour
    {
        [Tooltip("Cloud images to drift — each one moves and wraps independently.")]
        [SerializeField] private RectTransform[] clouds;

        [Tooltip("Pixels per second, left to right.")]
        [SerializeField] private float driftSpeed = 25f;

        [Tooltip("Extra distance ON TOP OF each cloud's own half-width a cloud travels past the canvas edge before wrapping back around. Wrapping used to use this as the WHOLE margin regardless of cloud size — for a cloud wider than that (e.g. 463px wide against a 150px margin), its trailing edge was still visibly on-screen the moment it teleported, reading as the next cloud \"spawning\" abruptly at the edge.")]
        [SerializeField] private float wrapMargin = 40f;

        private float canvasHalfWidth = 960f;
        private Vector2[] startPositions;

        private void Awake()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                canvasHalfWidth = ((RectTransform)canvas.transform).rect.width * 0.5f;

            if (clouds != null)
            {
                startPositions = new Vector2[clouds.Length];
                for (int i = 0; i < clouds.Length; i++)
                    if (clouds[i] != null) startPositions[i] = clouds[i].anchoredPosition;
            }
        }

        private void OnEnable()
        {
            // Reset to authored positions each time this story becomes active again (retry, or
            // switching back to this mission) instead of resuming wherever drift left off.
            if (clouds == null || startPositions == null) return;
            for (int i = 0; i < clouds.Length; i++)
                if (clouds[i] != null) clouds[i].anchoredPosition = startPositions[i];
        }

        private void Update()
        {
            if (clouds == null) return;
            float delta = driftSpeed * Time.unscaledDeltaTime;

            foreach (var cloud in clouds)
            {
                if (cloud == null) continue;
                // Per-cloud half-width, not a single flat margin for every cloud — a cloud wider
                // than the old fixed wrapMargin still had its trailing edge visibly on-screen at
                // the exact moment it teleported back around.
                float halfWidth = cloud.rect.width * 0.5f + wrapMargin;
                var pos = cloud.anchoredPosition;
                pos.x -= delta; // right to left
                if (pos.x < -canvasHalfWidth - halfWidth) pos.x = canvasHalfWidth + halfWidth;
                cloud.anchoredPosition = pos;
            }
        }
    }
}
