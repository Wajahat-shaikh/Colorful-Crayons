using System;
using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// The seven kinds of question Bridge Quest asks. This is the single taxonomy --
    /// "fruit", "vegetable" and "food" from the GDD's mission table are all
    /// <see cref="Object"/>, they are not types of their own.
    /// </summary>
    public enum QuestionType
    {
        Alphabet,
        Number,
        Counting,
        Object,
        Colour,
        Shape,
        Animal
    }

    /// <summary>
    /// One tappable answer. An option is either art (an apple, a triangle, a cat) or
    /// text (a letter, a numeral) -- never both. Colour questions use a plain swatch,
    /// which is what <see cref="useColour"/> switches on.
    /// </summary>
    [Serializable]
    public class AnswerOption
    {
        [Tooltip("Shown when set. Leave empty for a text-only option such as a letter or numeral.")]
        public Sprite art;

        [Tooltip("Shown when art is empty -- the letter, numeral or word itself.")]
        public string label;

        [Header("Colour questions")]
        [Tooltip("Tints the swatch instead of showing art. Only meaningful on Colour questions.")]
        public bool useColour;
        public Color colour = Color.white;

        [Tooltip("Spoken when this option is tapped. Blank falls back to the shared common bundle -- see BridgeQuestVoice.SpeakOption.")]
        public string voiceKey;
    }

    /// <summary>
    /// One question and its three options. <see cref="correctIndex"/> indexes
    /// <see cref="options"/> as authored; the card shuffles presentation order at
    /// runtime so the answer never sits in a fixed slot.
    /// </summary>
    [Serializable]
    public class QuestionData
    {
        public QuestionType type;

        [TextArea(1, 2)]
        [Tooltip("On-screen prompt. Non-readers get the voice line -- this is for adults and QA.")]
        public string prompt;

        [Tooltip("voiceover_title from the VO sheet. THIS is what the child actually receives -- never ship a question without it.")]
        public string promptVoiceKey;

        [Tooltip("Optional art above the prompt -- the three bananas on a Counting question, for instance.")]
        public Sprite promptArt;

        [Tooltip("Exactly three, per the GDD.")]
        public AnswerOption[] options = new AnswerOption[3];

        [Tooltip("Index into options as authored, before the runtime shuffle.")]
        public int correctIndex;

        public bool IsValid
        {
            get
            {
                return options != null
                    && options.Length > 0
                    && correctIndex >= 0
                    && correctIndex < options.Length;
            }
        }
    }

    /// <summary>
    /// One card of a storyboard collage. Mirrors RocketRun's StoryCutsceneUI.Panel,
    /// but lives in mission data rather than being hand-placed in the scene, so all
    /// twelve storyboards (six openings, six endings) are authored as assets.
    /// </summary>
    [Serializable]
    public class StoryPanel
    {
        public Sprite art;

        [TextArea(1, 2)]
        public string caption;

        [Tooltip("voiceover_title from the VO sheet, e.g. m1_story_open_1. Blank leaves the panel silent.")]
        public string voiceKey;

        [Tooltip("Optional second line spoken on this same panel, right after voiceKey finishes -- for when one card carries two beats of narration instead of flying in a whole new card for the second line. Blank plays only voiceKey.")]
        public string voiceKey2;

        [Header("Resting pose (anchored position / rotation / scale)")]
        public Vector2 restPosition;
        public float restRotation = -3f;
        public float restScale = 1f;

        [Header("Fly-in start offset from the resting pose")]
        public Vector2 fromOffset = new Vector2(0f, 900f);
        public float fromRotation = -18f;

        [Tooltip("Seconds this panel holds before the next flies in. A tap cuts it short.")]
        public float hold = 2.5f;
    }
}
