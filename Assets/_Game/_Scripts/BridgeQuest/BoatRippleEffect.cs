using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// A calm ring of water disturbance around the boat -- a couple of soft
    /// concentric rings that pulse outward and fade, fired once per paddle stroke
    /// (see RowingSplashTrigger) rather than running continuously. Nothing flung
    /// outward, nothing scattered -- just the water settling around the hull each
    /// time the paddle dips, matching a calm rowboat rather than a splash.
    ///
    /// Hand-rolled the same way as WaterSplashParticle -- plain pooled Image
    /// children driven by DOTween, since this sits on a Screen Space - Overlay
    /// canvas where a world-space particle system cannot composite correctly. Draws
    /// its own ring sprite (a soft annulus, transparent centre) rather than reusing
    /// borrowed art, so there is nothing else to wire up.
    ///
    /// Generic on purpose: nothing here knows about boats or paddles specifically --
    /// just "a couple of rings, once, around this point" -- usable anywhere
    /// something disturbs the surface briefly rather than continuously.
    /// </summary>
    public class BoatRippleEffect : MonoBehaviour
    {
        [Tooltip("How many concentric rings one Play() call spawns, staggered slightly apart.")]
        [SerializeField] private int ringCount = 2;

        [Tooltip("Seconds between each ring in the burst starting.")]
        [SerializeField] private float ringStagger = 0.12f;

        [Tooltip("Seconds for one ring to expand and fade.")]
        [SerializeField] private float ringDuration = 0.7f;

        [Tooltip("Ring size at the moment it appears, in local canvas units.")]
        [SerializeField] private float ringStartSize = 26f;

        [Tooltip("Ring size at the end of its expansion.")]
        [SerializeField] private float ringEndSize = 64f;

        [SerializeField] [Range(0f, 1f)] private float peakAlpha = 0.4f;
        [SerializeField] private Color ringColor = new Color(0.85f, 0.95f, 1f, 1f);

        [Tooltip("Ellipse squash on Y -- water ripples read as flattened ovals from this side-on camera angle, not perfect circles.")]
        [SerializeField] [Range(0.2f, 1f)] private float verticalSquash = 0.5f;

        private RectTransform rt;
        private static Sprite sharedRingSprite;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
            EnsureRingSprite();
        }

        /// <summary>Fires one burst of rings from this point. Safe to call again mid-burst.</summary>
        [ContextMenu("Play")]
        public void Play()
        {
            for (int i = 0; i < ringCount; i++)
            {
                float delay = i * ringStagger;
                if (delay <= 0f) SpawnRing();
                else DOVirtual.DelayedCall(delay, SpawnRing, false).SetUpdate(true);
            }
        }

        private void SpawnRing()
        {
            if (rt == null) return;

            GameObject go = new GameObject("BoatRipple", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform ringRt = go.GetComponent<RectTransform>();
            ringRt.SetParent(rt, false);
            ringRt.anchoredPosition = Vector2.zero;
            ringRt.sizeDelta = new Vector2(ringStartSize, ringStartSize * verticalSquash);

            Image img = go.GetComponent<Image>();
            img.sprite = sharedRingSprite;
            img.raycastTarget = false;
            Color c = ringColor;
            c.a = 0f;
            img.color = c;

            float endW = ringEndSize;
            float endH = ringEndSize * verticalSquash;

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(img.DOFade(peakAlpha, ringDuration * 0.2f).SetEase(Ease.OutSine));
            seq.Join(ringRt.DOSizeDelta(new Vector2(endW, endH), ringDuration).SetEase(Ease.OutSine));
            seq.Insert(ringDuration * 0.2f, img.DOFade(0f, ringDuration * 0.8f).SetEase(Ease.InSine));
            seq.OnComplete(delegate { Destroy(go); });
        }

        /// <summary>
        /// A soft-edged ring (annulus) -- bright band with a transparent centre and a
        /// gentle inner/outer falloff -- generated once and shared by every instance,
        /// so this effect needs no ring art shipped with the project.
        /// </summary>
        private static void EnsureRingSprite()
        {
            if (sharedRingSprite != null) return;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "BoatRippleRing";
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float outerRadius = size * 0.5f;
            float ringRadius = outerRadius * 0.72f;   // where the band sits
            float bandWidth = outerRadius * 0.30f;    // how wide the band is

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float distFromBand = Mathf.Abs(dist - ringRadius);
                    float alpha = Mathf.Clamp01(1f - distFromBand / bandWidth);
                    alpha = alpha * alpha; // soft falloff either side of the band
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            sharedRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sharedRingSprite.name = "BoatRippleRing";
        }
    }
}
