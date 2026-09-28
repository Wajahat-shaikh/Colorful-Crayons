using DG.Tweening;
using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Gentle back-and-forth drift on a RectTransform's anchored position -- a calm
    /// water surface, a drifting cloud, anything that should breathe in place rather
    /// than sit dead still. Position equivalent of SwayingFoliage's rotation sway,
    /// same DOTween convention as every other ambient effect in this game (unscaled
    /// time, so it keeps going while the world freezes behind a card).
    /// </summary>
    public class SwayPosition : MonoBehaviour
    {
        [Tooltip("Pixels either side of the resting X position.")]
        [SerializeField] private float swayX = 6f;

        [Tooltip("Pixels either side of the resting Y position.")]
        [SerializeField] private float swayY = 0f;

        [Tooltip("Seconds for one full swing (rest -> one side -> rest -> other side).")]
        [SerializeField] private float swayDuration = 3f;

        [Tooltip("Seconds before this instance starts swaying -- staggering a few of these keeps them from moving in lockstep.")]
        [SerializeField] private float startDelay = 0f;

        private RectTransform rt;
        private Vector2 restingPosition;
        private Tween tween;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
            if (rt != null) restingPosition = rt.anchoredPosition;
        }

        private void OnEnable()
        {
            if (rt == null) return;

            tween = rt
                .DOAnchorPos(restingPosition + new Vector2(swayX, swayY), swayDuration)
                .SetDelay(startDelay)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private void OnDisable()
        {
            if (tween != null)
            {
                tween.Kill();
                tween = null;
            }

            if (rt != null) rt.anchoredPosition = restingPosition;
        }
    }
}
