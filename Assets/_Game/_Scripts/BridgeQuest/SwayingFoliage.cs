using DG.Tweening;
using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Subtle idle sway for a foreground tree or bush -- a small back-and-forth
    /// rotation around the object's base, looping forever. Runs on unscaled time so
    /// the foliage keeps breathing even while Bridge Quest freezes the world behind
    /// a card or storyboard, the same convention as every other ambient tween in
    /// this game (see AlphaUtility).
    ///
    /// Expects the RectTransform's pivot to sit at the base of the art (e.g. (0.5, 0))
    /// so the rotation reads as the trunk swaying, not the whole sprite spinning
    /// around its centre.
    /// </summary>
    public class SwayingFoliage : MonoBehaviour
    {
        [Tooltip("Degrees to either side of resting rotation.")]
        [SerializeField] private float swayAngle = 3f;

        [Tooltip("Seconds for one full swing (rest -> one side -> rest -> other side).")]
        [SerializeField] private float swayDuration = 2.2f;

        [Tooltip("Seconds before this instance starts swaying -- staggering a tree and a bush a little keeps them from moving in lockstep.")]
        [SerializeField] private float startDelay = 0f;

        private RectTransform rt;
        private Tween tween;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if (rt == null) return;

            tween = rt
                .DORotate(new Vector3(0f, 0f, swayAngle), swayDuration)
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

            if (rt != null) rt.localRotation = Quaternion.identity;
        }
    }
}
