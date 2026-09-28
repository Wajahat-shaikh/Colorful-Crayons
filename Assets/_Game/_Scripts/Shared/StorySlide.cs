using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// One slide of a StorySlideshow: an image, optional caption, and an optional
    /// voice-over key. Game-agnostic on purpose -- see StorySlideshow's own header.
    /// </summary>
    [System.Serializable]
    public class StorySlide
    {
        public Sprite image;

        [TextArea(1, 3)]
        public string text;

        [Tooltip("Passed to onPlayVoiceOver. Blank plays no line.")]
        public string voiceOverKey;

        [Tooltip("Minimum seconds this slide is shown before advancing, even if its voice line finishes sooner.")]
        public float minDisplayDuration = 2.5f;
    }
}
