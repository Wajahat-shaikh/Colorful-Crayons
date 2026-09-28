using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// A hand-rolled splash burst -- a foam ring plus a handful of droplets that pop
    /// outward, arc, and fade -- built the same way as FallingLeaves/RippleEffect
    /// (plain pooled Image children driven by DOTween) rather than a real
    /// ParticleSystem, since this sits on a Screen Space - Overlay UI canvas where a
    /// world-space particle system cannot composite correctly.
    ///
    /// Runs as a continuous loop (a paddle dipping, a bow cutting through water) when
    /// <see cref="loop"/> is on, or fire a single burst on demand via <see cref="Play"/>.
    /// Droplets are drawn with a small procedural soft-circle sprite generated once
    /// and shared across every instance, so this needs no external art.
    ///
    /// Generic on purpose: nothing here knows about boats or paddles -- this is just
    /// "a burst of droplets and a ring", usable anywhere something disturbs water.
    /// </summary>
    public class WaterSplashParticle : MonoBehaviour
    {
        [Tooltip("The expanding ring's sprite. Leave empty to skip the ring and only spawn droplets.")]
        [SerializeField] private Sprite ringSprite;

        [SerializeField] private bool loop = true;
        [Tooltip("Seconds between bursts when looping.")]
        [SerializeField] private float burstInterval = 1.1f;
        [Tooltip("Random +/- range applied to burstInterval so multiple splashes don't stay in lockstep.")]
        [SerializeField] private float burstIntervalJitter = 0.3f;

        [SerializeField] private int dropletCount = 7;
        [SerializeField] private float dropletMinSize = 6f;
        [SerializeField] private float dropletMaxSize = 14f;
        [SerializeField] private float spreadRadius = 45f;
        [SerializeField] private float burstDuration = 0.5f;

        [SerializeField] private float ringStartSize = 24f;
        [SerializeField] private float ringMaxScale = 2.2f;
        [SerializeField] [Range(0f, 1f)] private float ringPeakAlpha = 0.85f;

        [SerializeField] private Color dropletColor = new Color(0.85f, 0.95f, 1f, 0.9f);

        [Header("Drip trail (plays right after the burst)")]
        [Tooltip("Small droplets that fall straight down after the main burst, as if dripping off a paddle lifting out of the water. 0 disables the trail.")]
        [SerializeField] private int dripCount = 3;
        [SerializeField] private float dripMinSize = 3f;
        [SerializeField] private float dripMaxSize = 6f;
        [Tooltip("How far each drip falls before fading out.")]
        [SerializeField] private float dripFallDistance = 26f;
        [SerializeField] private float dripDuration = 0.4f;
        [Tooltip("Random spacing between each drip's start, so they don't fall in a single clump.")]
        [SerializeField] private float dripStagger = 0.08f;

        private RectTransform rt;
        private Sequence loopSeq;

        private static Sprite sharedDropletSprite;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
            EnsureDropletSprite();
        }

        private void OnEnable()
        {
            if (loop) StartLoop();
        }

        private void OnDisable()
        {
            StopLoop();
        }

        /// <summary>Fires one burst. Safe to call on a looping instance too -- it just adds an extra burst.</summary>
        [ContextMenu("Play")]
        public void Play()
        {
            DoBurst();
        }

        private void StartLoop()
        {
            StopLoop();
            ScheduleNext(0f);
        }

        private void ScheduleNext(float delay)
        {
            loopSeq = DOTween.Sequence().SetUpdate(true);
            loopSeq.AppendInterval(delay);
            loopSeq.AppendCallback(delegate
            {
                DoBurst();
                ScheduleNext(Mathf.Max(0.05f, burstInterval + Random.Range(-burstIntervalJitter, burstIntervalJitter)));
            });
        }

        private void StopLoop()
        {
            if (loopSeq != null)
            {
                loopSeq.Kill();
                loopSeq = null;
            }
        }

        private void DoBurst()
        {
            if (ringSprite != null) SpawnRing();

            for (int i = 0; i < dropletCount; i++) SpawnDroplet();

            if (dripCount > 0)
            {
                Sequence dripSeq = DOTween.Sequence().SetUpdate(true);
                dripSeq.AppendInterval(burstDuration); // right after the splash anim, not on top of it
                dripSeq.AppendCallback(SpawnDripTrail);
            }
        }

        private void SpawnDripTrail()
        {
            for (int i = 0; i < dripCount; i++)
            {
                float delay = i * dripStagger + Random.Range(0f, dripStagger * 0.5f);
                DOVirtual.DelayedCall(delay, SpawnDrip, false).SetUpdate(true);
            }
        }

        private void SpawnRing()
        {
            GameObject go = new GameObject("SplashRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform ringRt = go.GetComponent<RectTransform>();
            ringRt.SetParent(rt, false);
            ringRt.anchoredPosition = Vector2.zero;
            ringRt.sizeDelta = new Vector2(ringStartSize, ringStartSize);
            ringRt.localScale = Vector3.one * 0.4f;

            Image img = go.GetComponent<Image>();
            img.sprite = ringSprite;
            img.raycastTarget = false;
            Color c = img.color;
            c.a = 0f;
            img.color = c;

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(img.DOFade(ringPeakAlpha, burstDuration * 0.25f).SetEase(Ease.OutSine));
            seq.Join(ringRt.DOScale(ringMaxScale, burstDuration).SetEase(Ease.OutSine));
            seq.Insert(burstDuration * 0.25f, img.DOFade(0f, burstDuration * 0.75f).SetEase(Ease.InSine));
            seq.OnComplete(delegate { Destroy(go); });
        }

        private void SpawnDroplet()
        {
            GameObject go = new GameObject("Droplet", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform dropRt = go.GetComponent<RectTransform>();
            dropRt.SetParent(rt, false);

            // stretched rather than round -- reads as a short wake streak skimming the
            // surface (see reference: thin curved lines trailing the paddle) instead
            // of a droplet flung into the air
            float size = Random.Range(dropletMinSize, dropletMaxSize);
            dropRt.sizeDelta = new Vector2(size * 1.7f, size * 0.85f);
            dropRt.anchoredPosition = Vector2.zero;

            Image img = go.GetComponent<Image>();
            img.sprite = sharedDropletSprite;
            img.raycastTarget = false;
            img.color = dropletColor;

            // shallow, mostly-sideways spread along the surface rather than a high arc
            float angleDeg = Random.Range(-20f, 20f) + (Random.value < 0.5f ? 0f : 180f);
            float angle = angleDeg * Mathf.Deg2Rad;
            float dist = Random.Range(spreadRadius * 0.5f, spreadRadius);
            Vector2 target = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.3f) * dist;

            dropRt.localRotation = Quaternion.Euler(0f, 0f, angleDeg);

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(dropRt.DOAnchorPos(target, burstDuration).SetEase(Ease.OutSine));
            seq.Join(img.DOFade(0f, burstDuration).SetEase(Ease.InSine));
            seq.OnComplete(delegate { Destroy(go); });
        }

        /// <summary>One drop falling straight down and fading -- the trail dripping off a paddle as it lifts clear of the water.</summary>
        private void SpawnDrip()
        {
            if (rt == null) return;

            GameObject go = new GameObject("Drip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform dropRt = go.GetComponent<RectTransform>();
            dropRt.SetParent(rt, false);

            float size = Random.Range(dripMinSize, dripMaxSize);
            dropRt.sizeDelta = new Vector2(size, size);
            dropRt.anchoredPosition = new Vector2(Random.Range(-6f, 6f), 0f);

            Image img = go.GetComponent<Image>();
            img.sprite = sharedDropletSprite;
            img.raycastTarget = false;
            img.color = dropletColor;

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(dropRt.DOAnchorPosY(dropRt.anchoredPosition.y - dripFallDistance, dripDuration).SetEase(Ease.InQuad));
            seq.Join(img.DOFade(0f, dripDuration).SetEase(Ease.InSine));
            seq.OnComplete(delegate { Destroy(go); });
        }

        /// <summary>
        /// A small soft-edged circle, generated once and shared by every instance --
        /// this effect needs no droplet art shipped with the project.
        /// </summary>
        private static void EnsureDropletSprite()
        {
            if (sharedDropletSprite != null) return;

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "WaterSplashDroplet";
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - dist / radius);
                    alpha = alpha * alpha; // softer falloff toward the edge
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            sharedDropletSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sharedDropletSprite.name = "WaterSplashDroplet";
        }
    }
}
