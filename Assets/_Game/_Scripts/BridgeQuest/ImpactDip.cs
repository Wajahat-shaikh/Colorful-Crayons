using DG.Tweening;
using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// A short downward dip and settle -- something being stepped on giving a little
    /// under the weight, then bouncing back to rest. Captures its resting anchored
    /// position once in Awake so Play() always returns to wherever this object was
    /// actually authored, and can be called again mid-dip -- it just restarts.
    ///
    /// Generic on purpose: nothing here knows about stones or steps -- this is just
    /// "dip down, settle back", usable on anything that should react to being landed
    /// on (a stone, a plank, a button).
    /// </summary>
    public class ImpactDip : MonoBehaviour
    {
        [Tooltip("How far down this dips, in local canvas units.")]
        [SerializeField] private float dipDistance = 12f;

        [Tooltip("Seconds to dip down.")]
        [SerializeField] private float downDuration = 0.08f;

        [Tooltip("Seconds to settle back up, with a little overshoot.")]
        [SerializeField] private float upDuration = 0.22f;

        private RectTransform rt;
        private Vector2 restingPosition;
        private Sequence sequence;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
            restingPosition = rt.anchoredPosition;
        }

        private void OnDisable()
        {
            Stop();
        }

        /// <summary>Plays the dip from rest back to rest. Safe to call again mid-dip.</summary>
        [ContextMenu("Play")]
        public void Play()
        {
            Stop();

            sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(rt.DOAnchorPosY(restingPosition.y - dipDistance, downDuration).SetEase(Ease.OutQuad));
            sequence.Append(rt.DOAnchorPosY(restingPosition.y, upDuration).SetEase(Ease.OutBack));
            sequence.OnComplete(ResetToRest);
        }

        /// <summary>Cuts the dip short and snaps straight back to rest.</summary>
        public void Stop()
        {
            if (sequence != null)
            {
                sequence.Kill();
                sequence = null;
            }

            ResetToRest();
        }

        private void ResetToRest()
        {
            if (rt != null) rt.anchoredPosition = restingPosition;
        }
    }
}
