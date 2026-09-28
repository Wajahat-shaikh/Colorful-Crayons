using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// A burst of ripple pulses -- each one scales up while fading out then snaps back
    /// to rest, repeated <see cref="pulseCount"/> times, reading as a splash of
    /// expanding rings rather than one single blip. Every pulse resets to exactly
    /// whatever scale and alpha this object was authored with -- not a hardcoded
    /// 0/1, whatever it actually is in the scene when this component wakes up. That
    /// authored state is captured once in Awake, so <see cref="Play"/> can be called
    /// as many times as the surface is touched and it will always return to the same
    /// resting look in between.
    ///
    /// Generic on purpose: nothing here knows about water, stones or jumping --
    /// this is just "pulse and settle back, a few times", usable anywhere that reads
    /// the same way (a splash, a tap ping, a highlight).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class RippleEffect : MonoBehaviour
    {
        [Tooltip("Scale multiplier at the peak of each ripple, relative to the resting scale.")]
        [SerializeField] private float scaleMultiplier = 1.6f;

        [Tooltip("Seconds for one ripple -- scale up and fade out together over this long.")]
        [SerializeField] private float duration = 0.6f;

        [Tooltip("How many times the ripple repeats per Play() call -- this is what makes it read as a splash of rings instead of one blip. -1 loops forever until Stop() is called.")]
        [SerializeField] private int pulseCount = 3;

        [Tooltip("Extra pause after one ripple fully fades before the next one starts. 0 chains them back to back.")]
        [SerializeField] private float pulseGap = 0.1f;

        [Tooltip("Starts playing on its own as soon as this is enabled -- for an ambient ripple that just keeps going, rather than one triggered by something arriving. Pair with pulseCount = -1 so it never runs out.")]
        [SerializeField] private bool playOnEnable = false;

        [Tooltip("playOnEnable picks a random wait between 0 and this before its first pulse -- so several ambient ripples started together don't all pulse in lockstep. 0 starts immediately.")]
        [SerializeField] private float maxRandomStartDelay = 0f;

        [Tooltip("For an ambient ripple that should never look like it starts/stops: each ring expands and fades out, then the next one starts from small again immediately -- forever, with no pause between them. pulseCount and pulseGap are ignored in this mode -- Stop() is the only way to end it.")]
        [SerializeField] private bool seamlessLoop = false;

        [Tooltip("seamlessLoop only -- the alpha each ring fades up to before fading back out. Independent of this object's authored resting alpha, since that resting look need not be how strong the ripple itself reads.")]
        [SerializeField] [Range(0f, 1f)] private float seamlessPeakAlpha = 0.5f;

        private Image image;
        private RectTransform rt;

        private Vector3 restingScale;
        private float restingAlpha;

        private Sequence sequence;
        private Tween startupDelay;

        private void Awake()
        {
            image = GetComponent<Image>();
            rt = GetComponent<RectTransform>();

            restingScale = rt.localScale;
            restingAlpha = image.color.a;
        }

        private void OnEnable()
        {
            if (!playOnEnable) return;

            float delay = maxRandomStartDelay > 0f ? Random.Range(0f, maxRandomStartDelay) : 0f;
            if (delay <= 0f)
            {
                Play();
                return;
            }

            startupDelay = DOVirtual.DelayedCall(delay, Play, false).SetUpdate(true);
        }

        private void OnDisable()
        {
            if (startupDelay != null)
            {
                startupDelay.Kill();
                startupDelay = null;
            }

            Stop();
        }

        /// <summary>Plays the ripple burst from the resting look back to the resting look. Safe to call again mid-burst -- it just restarts.</summary>
        [ContextMenu("Play")]
        public void Play()
        {
            Stop();
            ResetToRest();

            sequence = DOTween.Sequence().SetUpdate(true);

            if (seamlessLoop)
            {
                // LoopType.Restart snaps back to each tween's START value and
                // immediately expands out again -- no manual reset callback or gap
                // needed, so there is never a pause for the eye to read as "stopping",
                // and it never reverses back inward like a yoyo would.
                //
                // The scale snap itself is invisible only if alpha is already at 0 the
                // instant it happens. So alpha does not run start-to-finish like the
                // scale does -- it fades IN from 0 for the first half of the ripple and
                // back OUT to 0 for the second half, while the scale keeps expanding the
                // whole time. The loop always resets during that zero-alpha instant, so
                // the restart is never actually seen.
                float half = duration * 0.5f;

                if (image != null)
                {
                    Color c = image.color;
                    c.a = 0f;
                    image.color = c;
                }

                sequence.Append(rt.DOScale(restingScale * scaleMultiplier, duration).SetEase(Ease.OutSine));
                sequence.Join(image.DOFade(seamlessPeakAlpha, half).SetEase(Ease.OutSine));
                sequence.Insert(half, image.DOFade(0f, duration - half).SetEase(Ease.InSine));
                sequence.SetLoops(-1, LoopType.Restart);
                return;
            }

            sequence.Append(rt.DOScale(restingScale * scaleMultiplier, duration).SetEase(Ease.OutQuad));
            sequence.Join(image.DOFade(0f, duration).SetEase(Ease.OutQuad));
            sequence.AppendCallback(ResetToRest);
            if (pulseGap > 0f) sequence.AppendInterval(pulseGap);
            sequence.SetLoops(pulseCount, LoopType.Restart);
            sequence.OnComplete(ResetToRest);
        }

        /// <summary>Cuts the burst short and snaps straight back to rest.</summary>
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
            if (rt != null) rt.localScale = restingScale;
            if (image != null)
            {
                Color c = image.color;
                c.a = restingAlpha;
                image.color = c;
            }
        }
    }
}
