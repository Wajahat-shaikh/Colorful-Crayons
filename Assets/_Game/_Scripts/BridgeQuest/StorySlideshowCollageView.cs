using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Bridge Quest-specific companion to a StorySlideshow: instead of a
    /// single image swapping in place, each slide flies in as its own image and
    /// stays on screen, with every earlier slide dimming down to dimmedAlpha as the
    /// next one arrives -- a scrapbook collage building up, in step with
    /// StorySlideshow's own pacing (onSlideShown already waits for minDisplayDuration
    /// and the voice line, same as every other slide advance).
    ///
    /// Kept separate from StorySlideshow itself, which stays generic and knows
    /// nothing about StoryPanel or this layered look.
    /// </summary>
    public class StorySlideshowCollageView : MonoBehaviour
    {
        [SerializeField] private StorySlideshow slideshow;
        [SerializeField] private RectTransform collageLayer;
        [SerializeField] private Image slotPrototype;

        [SerializeField] private float slideInDuration = 0.55f;

        [Tooltip("Alpha every earlier slide settles to once a newer one arrives.")]
        [SerializeField] [Range(0f, 1f)] private float dimmedAlpha = 0.7f;

        [Tooltip("Used when a panel doesn't author its own fromOffset/fromRotation.")]
        [SerializeField] private Vector2 fallbackFromOffset = new Vector2(0f, 900f);
        [SerializeField] private float fallbackFromRotation = -18f;

        private StoryPanel[] panels;
        private readonly List<Image> activeSlots = new List<Image>();

        private void OnEnable()
        {
            if (slideshow == null) return;
            slideshow.onSlideshowStarted.AddListener(ResetSlots);
            slideshow.onSlideShown.AddListener(OnSlideShown);
        }

        private void OnDisable()
        {
            if (slideshow == null) return;
            slideshow.onSlideshowStarted.RemoveListener(ResetSlots);
            slideshow.onSlideShown.RemoveListener(OnSlideShown);
        }

        /// <summary>Called by the adapter just before Play() so each slide knows its own art and fly-in pose.</summary>
        public void SetPanels(StoryPanel[] storyPanels)
        {
            panels = storyPanels;
        }

        private void ResetSlots()
        {
            foreach (Image slot in activeSlots)
            {
                if (slot == null) continue;
                slot.rectTransform.DOKill();
                Destroy(slot.gameObject);
            }
            activeSlots.Clear();
        }

        private void OnSlideShown(int index)
        {
            if (panels == null || index < 0 || index >= panels.Length) return;
            if (slotPrototype == null || collageLayer == null) return;

            // everything already on screen settles back to make room for the new arrival
            foreach (Image slot in activeSlots)
            {
                if (slot == null) continue;
                slot.DOKill();
                slot.DOFade(dimmedAlpha, slideInDuration).SetUpdate(true);
            }

            StoryPanel p = panels[index];

            GameObject go = Instantiate(slotPrototype.gameObject, collageLayer);
            go.name = "Slide_" + index;
            go.SetActive(true);

            Image image = go.GetComponent<Image>();
            RectTransform rt = image.rectTransform;

            if (p.art != null) image.sprite = p.art;

            Vector2 fromOffset = p.fromOffset != Vector2.zero ? p.fromOffset : fallbackFromOffset;
            float fromRotation = p.fromRotation != 0f ? p.fromRotation : fallbackFromRotation;
            float restScale = p.restScale > 0f ? p.restScale : 1f;

            rt.anchoredPosition = p.restPosition + fromOffset;
            rt.localRotation = Quaternion.Euler(0f, 0f, fromRotation);
            rt.localScale = Vector3.one * (restScale * 0.8f);

            Color c = image.color;
            c.a = 0f;
            image.color = c;

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(rt.DOAnchorPos(p.restPosition, slideInDuration).SetEase(Ease.OutBack, 1.2f));
            seq.Join(rt.DOLocalRotate(new Vector3(0f, 0f, p.restRotation), slideInDuration).SetEase(Ease.OutBack));
            seq.Join(rt.DOScale(restScale, slideInDuration).SetEase(Ease.OutBack));
            seq.Join(image.DOFade(1f, slideInDuration).SetEase(Ease.OutSine));

            activeSlots.Add(image);
        }
    }
}
