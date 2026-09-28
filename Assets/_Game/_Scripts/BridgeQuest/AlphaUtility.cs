using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Clamped 0-1 alpha helper shared across Bridge Quest's UI feedback (card fades,
    /// flashes, fade-in/out) so nothing accidentally sets a Color/Graphic alpha outside
    /// the valid range. Pulses its own target between fromAlpha and toAlpha via DOTween
    /// on unscaled time, so it keeps pulsing through Bridge Quest's frozen-world cards
    /// and storyboards instead of stalling with Time.timeScale.
    /// </summary>
    public class AlphaUtility : MonoBehaviour
    {
        [SerializeField] private Graphic target;

        [SerializeField] private bool playOnAwake = true;

        [Range(0f, 1f)] [SerializeField] private float fromAlpha = 0f;
        [Range(0f, 1f)] [SerializeField] private float toAlpha = 1f;
        [SerializeField] private float duration = 1f;

        private Tween tween;

        private void Awake()
        {
            if (playOnAwake) Play();
        }

        public void Play()
        {
            Stop();
            if (target == null) return;

            SetAlpha(target, fromAlpha);

            tween = target
                .DOFade(toAlpha, duration)
                .SetUpdate(true) // keeps pulsing while Bridge Quest has Time.timeScale at 0
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        public void Stop()
        {
            if (tween != null)
            {
                tween.Kill();
                tween = null;
            }
        }

        private void OnDestroy()
        {
            Stop();
        }

        public float Clamp01(float alpha)
        {
            return Mathf.Clamp01(alpha);
        }

        public Color WithAlpha(Color color, float alpha)
        {
            color.a = Clamp01(alpha);
            return color;
        }

        public void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            graphic.color = WithAlpha(graphic.color, alpha);
        }

        public void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            renderer.color = WithAlpha(renderer.color, alpha);
        }

        public void SetAlpha(CanvasGroup canvasGroup, float alpha)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = Clamp01(alpha);
        }
    }
}
