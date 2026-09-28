using DG.Tweening;
using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// A continuous, gentle bob-and-rock -- something floating on water shifting
    /// slightly with it, whether or not anything else is happening. Runs from
    /// OnEnable and never stops on its own; Stop() is the only way to end it.
    ///
    /// Plain Transform, not RectTransform -- this is meant for a world-space rig
    /// (e.g. BhideOnBoat, filmed into the walker's texture by its own RigCamera),
    /// not a UI element, so it never competes with anything tweening the walker's
    /// own RectTransform in canvas space.
    ///
    /// Generic on purpose: nothing here knows about boats specifically, just
    /// "float in place", usable on anything sitting on water.
    /// </summary>
    public class WaterBuoyancy : MonoBehaviour
    {
        [Tooltip("How far up and down this bobs, in local units.")]
        [SerializeField] private float bobHeight = 0.06f;

        [Tooltip("Seconds for one full up-down cycle.")]
        [SerializeField] private float bobDuration = 1.4f;

        [Tooltip("How far this rocks side to side, in degrees.")]
        [SerializeField] private float rockAngle = 2.5f;

        [Tooltip("Seconds for one full rock cycle. Deliberately offset from bobDuration by default so the two never fall into lockstep.")]
        [SerializeField] private float rockDuration = 1.7f;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private Tween bobTween;
        private Tween rockTween;

        private void Awake()
        {
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
        }

        private void OnEnable() => Play();
        private void OnDisable() => Stop();

        [ContextMenu("Play")]
        public void Play()
        {
            Stop();

            bobTween = transform
                .DOLocalMoveY(restPosition.y + bobHeight, bobDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);

            rockTween = transform
                .DOLocalRotate(restRotation.eulerAngles + new Vector3(0f, 0f, rockAngle), rockDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        /// <summary>Kills both loops and snaps straight back to rest.</summary>
        public void Stop()
        {
            if (bobTween != null) { bobTween.Kill(); bobTween = null; }
            if (rockTween != null) { rockTween.Kill(); rockTween = null; }

            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
        }
    }
}
