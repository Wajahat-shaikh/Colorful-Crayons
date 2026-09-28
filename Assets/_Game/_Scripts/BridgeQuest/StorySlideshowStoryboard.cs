using System;
using System.Collections.Generic;
using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Lets BridgeQuestFlow drive a generic StorySlideshow the same way it
    /// drives BridgeStoryCutsceneUI: converts this mission's StoryPanel data into
    /// StorySlide entries and forwards the completion callback, so StorySlideshow
    /// itself never needs to know Bridge Quest's data shapes.
    /// </summary>
    public class StorySlideshowStoryboard : MonoBehaviour, IStoryboardPlayer
    {
        [SerializeField] private StorySlideshow slideshow;

        [Tooltip("Optional -- gives each slide its own flying-collage look instead of a single image swapping in place.")]
        [SerializeField] private StorySlideshowCollageView collageView;

        private Action pendingComplete;

        private void OnEnable()
        {
            if (slideshow != null) slideshow.onSlideshowComplete.AddListener(HandleComplete);
        }

        private void OnDisable()
        {
            if (slideshow != null) slideshow.onSlideshowComplete.RemoveListener(HandleComplete);
        }

        public void Play(StoryPanel[] panels, Action onComplete)
        {
            if (slideshow == null || panels == null || panels.Length == 0)
            {
                if (onComplete != null) onComplete();
                return;
            }

            List<StorySlide> slides = new List<StorySlide>(panels.Length);
            foreach (StoryPanel p in panels)
            {
                slides.Add(new StorySlide
                {
                    image = p.art,
                    text = p.caption,
                    voiceOverKey = p.voiceKey,
                    minDisplayDuration = p.hold
                });
            }

            pendingComplete = onComplete;
            if (collageView != null) collageView.SetPanels(panels);
            slideshow.Play(slides);
        }

        private void HandleComplete()
        {
            Action cb = pendingComplete;
            pendingComplete = null;
            if (cb != null) cb();
        }
    }
}
