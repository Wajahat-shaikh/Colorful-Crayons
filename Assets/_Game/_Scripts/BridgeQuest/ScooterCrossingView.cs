using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// ICrossingView for a character with no Animator rig at all -- Abdul's scooter,
    /// built entirely from plain UI RawImages already placed and textured in the
    /// scene. There is nothing to film or crossfade here: crossing just means the
    /// wheels spin, and the whole thing blinks on its own timer regardless of
    /// whether it is moving.
    ///
    /// No celebration rig -- HasCelebration is always false, same as Bhide's setup.
    /// </summary>
    public class ScooterCrossingView : MonoBehaviour, ICrossingView
    {
        [Header("Wheels")]
        [Tooltip("Front and rear tyre RectTransforms (tireF, TrieB). Spin continuously while crossing, stop when idle.")]
        [SerializeField] private RectTransform[] wheels;

        [Tooltip("Degrees per second while crossing.")]
        [SerializeField] private float wheelSpinSpeed = 720f;

        [Header("Vibrate")]
        [Tooltip("How much the whole scooter scales up and down while crossing -- a quick, tiny wobble read as engine rumble/motion, since there is no walk cycle to sell it otherwise. 0 disables.")]
        [SerializeField] private float vibrateScale = 0.035f;

        [Tooltip("Seconds for one up-down cycle of the wobble while crossing. Small and fast reads as a vibration, not a bounce.")]
        [SerializeField] private float vibrateSpeed = 0.06f;

        [Tooltip("Same wobble while just standing there, scaled down -- the engine reads as running even between hops, not only mid-step. 0 disables and leaves idle perfectly still.")]
        [SerializeField] private float idleVibrateScale = 0.015f;

        [Tooltip("Seconds for one up-down cycle of the idle wobble. Slower than the crossing one so idle reads as a gentler putter, not the same rumble.")]
        [SerializeField] private float idleVibrateSpeed = 0.12f;

        [Header("Blink")]
        [SerializeField] private GameObject eyesOpenL;
        [SerializeField] private GameObject eyesOpenR;
        [SerializeField] private GameObject eyesClosedL;
        [SerializeField] private GameObject eyesClosedR;

        [Tooltip("Average seconds between blinks.")]
        [SerializeField] private float blinkInterval = 3f;

        [Tooltip("Random +/- range applied to blinkInterval so the blink doesn't read as a metronome.")]
        [SerializeField] private float blinkIntervalJitter = 1.5f;

        [Tooltip("How long the eyes stay closed.")]
        [SerializeField] private float blinkDuration = 0.12f;

        private Coroutine blinkRoutine;
        private RectTransform body;
        private Vector3 restScale;

        public bool HasCelebration { get { return false; } }

        private void Awake()
        {
            body = GetComponent<RectTransform>();
            if (body != null) restScale = body.localScale;
        }

        private void OnEnable()
        {
            SetEyesOpen(true);
            blinkRoutine = StartCoroutine(BlinkLoop());
        }

        private void OnDisable()
        {
            if (blinkRoutine != null)
            {
                StopCoroutine(blinkRoutine);
                blinkRoutine = null;
            }
            StopWheels();
            StopVibrate();
        }

        public void PlayIdle()
        {
            StopWheels();
            StartVibrate(idleVibrateScale, idleVibrateSpeed);
        }

        public void PlayWalk()
        {
            StartWheels();
            StartVibrate(vibrateScale, vibrateSpeed);
        }

        public void PlayCelebration() { }
        public void StopCelebration() { }

        private void StartWheels()
        {
            StopWheels();
            if (wheels == null) return;

            float duration = 360f / Mathf.Max(1f, wheelSpinSpeed);
            for (int i = 0; i < wheels.Length; i++)
            {
                RectTransform w = wheels[i];
                if (w == null) continue;

                w.DOLocalRotate(new Vector3(0f, 0f, -360f), duration, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Incremental)
                    .SetUpdate(true);
            }
        }

        private void StopWheels()
        {
            if (wheels == null) return;
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] != null) wheels[i].DOKill();
            }
        }

        private void StartVibrate(float scale, float speed)
        {
            StopVibrate();
            if (body == null || scale <= 0f) return;

            body.DOScale(restScale * (1f + scale), speed)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private void StopVibrate()
        {
            if (body == null) return;
            body.DOKill();
            body.localScale = restScale;
        }

        private IEnumerator BlinkLoop()
        {
            while (true)
            {
                float wait = blinkInterval + Random.Range(-blinkIntervalJitter, blinkIntervalJitter);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.3f, wait));

                SetEyesOpen(false);
                yield return new WaitForSecondsRealtime(blinkDuration);
                SetEyesOpen(true);
            }
        }

        private void SetEyesOpen(bool open)
        {
            if (eyesOpenL != null) eyesOpenL.SetActive(open);
            if (eyesOpenR != null) eyesOpenR.SetActive(open);
            if (eyesClosedL != null) eyesClosedL.SetActive(!open);
            if (eyesClosedR != null) eyesClosedR.SetActive(!open);
        }

        private void OnDestroy()
        {
            StopWheels();
            StopVibrate();
        }
    }
}
