using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Two jobs, both borrowed from RocketRunTutorial's shape.
    ///
    /// 1. The opening lesson. Each instruction is shown with the world frozen and
    ///    waits for a tap. It sits still until the opening storyboard has finished,
    ///    so the two never speak over each other.
    ///
    /// 2. The stuck-nudge. If a question goes unanswered for a while, an animated
    ///    hand appears over the correct option. RocketRun did not need this -- a
    ///    runner always moves -- but a question card does not resolve itself, and a
    ///    three-year-old who cannot find the answer has no other way forward. The
    ///    GDD specifies no hint behaviour at all; this is the safety net.
    /// </summary>
    public class BridgeQuestTutorial : MonoBehaviour
    {
        [Header("Bubble")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform bubble;
        [SerializeField] private Image primaryIcon;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TextMeshProUGUI tapHintText;

        [Header("Step icons")]
        [Tooltip("Ear or speaker art for the 'listen' step.")]
        [SerializeField] private Sprite listenIcon;

        [Tooltip("Pointing-finger art for the 'tap the answer' step.")]
        [SerializeField] private Sprite handIcon;

        [Tooltip("Art for the third step -- 'each right answer moves you forward'. A plank for Tappu's bridge by default; swap per scene to match the mechanic (a boat/paddle for Bhide's rowing, etc).")]
        [SerializeField] private Sprite plankIcon;

        [Tooltip("Third step's message. Defaults to the bridge-building copy -- only Tappu actually builds a bridge, so every other scene should override this to describe its own mechanic (rowing a boat, etc) instead of sharing this line.")]
        [SerializeField] private string thirdStepMessage = "Every right answer builds the bridge.";

        [Tooltip("Third step's voice key. Empty (default) falls back to the shared AudioMapper.TutorialBridge line -- override this whenever thirdStepMessage is overridden, so the scene's own recorded line (matching its own audio category) actually plays instead of the generic bridge-building one.")]
        [SerializeField] private string thirdStepVoiceKey = "";

        [Header("Stuck-nudge")]
        [Tooltip("Animated hand, reparented over the correct option when the child stalls.")]
        [SerializeField] private RectTransform hintHand;

        [Tooltip("Seconds of no tap before the hand appears. Long enough not to rob them of the answer.")]
        [SerializeField] private float hintDelay = 8f;

        [Tooltip("Seconds before the hand appears again, if they still have not answered.")]
        [SerializeField] private float hintRepeatDelay = 6f;

        [Tooltip("How much the hand shrinks on each press, as a fraction of its scale -- e.g. 0.25 punches down to 75% size and springs back, reading as a fingertip tapping rather than a hand bobbing up and down.")]
        [SerializeField] private float hintTapScale = 0.25f;

        [Tooltip("Seconds for one press-and-release cycle.")]
        [SerializeField] private float hintTapDuration = 0.35f;

        [Tooltip("How many taps play per appearance, before the hand waits out hintRepeatDelay and tries again.")]
        [SerializeField] private int hintTapCount = 6;

        [Header("Tuning")]
        [SerializeField] private float popInDuration = 0.3f;

        private class Step
        {
            public string message;
            public Sprite icon;
            public string voiceKey;
        }

        private Coroutine hintRoutine;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            if (hintHand != null) hintHand.gameObject.SetActive(false);
        }

        // ---- opening lesson -------------------------------------------------

        /// <summary>
        /// Runs the three-step opening lesson, then calls back. Waits out the opening
        /// storyboard first.
        /// </summary>
        public void RunOpeningLesson(Action onComplete)
        {
            StartCoroutine(LessonRoutine(onComplete));
        }

        private IEnumerator LessonRoutine(Action onComplete)
        {
            // let every other Start() run before we touch anything
            yield return null;

            // The storyboard shares this scene's one AudioSource. RunMission already
            // waits for the storyboard's own onStoryboardComplete before calling here,
            // but that handshake fires the instant the last panel's wait loop exits --
            // a tap-skip on that very last line can leave the tail end of it (or a
            // late-decoding clip) still audible for a beat after. Wait that out too, so
            // this lesson's own first line never Stop()s a story line still speaking.
            float waited = 0f;
            while (waited < 5f)
            {
                RuntimeAudioLoader loader = RuntimeAudioLoader.Instance;
                if (loader == null || loader._commonAudioSource == null || !loader._commonAudioSource.isPlaying) break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            BridgeQuestAudioMapper voice = BridgeQuestVoice.Mapper;

            yield return ShowInstruction(new Step
            {
                message = "Listen to the question.",
                icon = listenIcon,
                voiceKey = voice != null ? voice.TutorialListen : null
            });

            yield return ShowInstruction(new Step
            {
                message = "Tap the right answer!",
                icon = handIcon,
                voiceKey = voice != null ? voice.TutorialTap : null
            });

            yield return ShowInstruction(new Step
            {
                message = thirdStepMessage,
                icon = plankIcon,
                voiceKey = !string.IsNullOrEmpty(thirdStepVoiceKey) ? thirdStepVoiceKey : (voice != null ? voice.TutorialBridge : null)
            });

            if (root != null) root.SetActive(false);
            Time.timeScale = 1f;

            if (onComplete != null) onComplete();
        }

        /// <summary>
        /// Freezes the world, pops the bubble in, waits for a tap, then hides it and
        /// unfreezes so the child can act on what they just heard.
        /// </summary>
        private IEnumerator ShowInstruction(Step step)
        {
            Time.timeScale = 0f;
            if (root != null) root.SetActive(true);

            if (messageText != null) messageText.text = step.message;
            if (tapHintText != null) tapHintText.gameObject.SetActive(true);

            if (primaryIcon != null)
            {
                primaryIcon.sprite = step.icon;
                primaryIcon.enabled = step.icon != null;
            }

            BridgeQuestVoice.Play(step.voiceKey);

            if (bubble != null)
            {
                float t = 0f;
                bubble.localScale = Vector3.zero;
                while (t < popInDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float p = Mathf.Clamp01(t / popInDuration);
                    bubble.localScale = Vector3.one * Mathf.Sin(p * Mathf.PI * 0.5f);
                    yield return null;
                }
                bubble.localScale = Vector3.one;
            }

            // eat one frame so the tap that dismissed the previous bubble cannot
            // also immediately dismiss this one
            yield return null;

            while (!TapDetected()) yield return null;

            if (root != null) root.SetActive(false);
            Time.timeScale = 1f;
        }

        // ---- stuck-nudge ----------------------------------------------------

        /// <summary>Starts watching a presented question. Cancelled by <see cref="DisarmHint"/>.</summary>
        public void ArmHint(QuestionCardUI card)
        {
            DisarmHint();
            if (card == null || hintHand == null) return;

            hintRoutine = StartCoroutine(HintRoutine(card));
        }

        public void DisarmHint()
        {
            if (hintRoutine != null)
            {
                StopCoroutine(hintRoutine);
                hintRoutine = null;
            }

            if (hintHand != null)
            {
                hintHand.DOKill();
                hintHand.localScale = Vector3.one;
                hintHand.gameObject.SetActive(false);
            }
        }

        private IEnumerator HintRoutine(QuestionCardUI card)
        {
            yield return new WaitForSecondsRealtime(hintDelay);

            while (true)
            {
                RectTransform target = card.CorrectSlotRect;
                if (target == null) yield break;

                hintHand.SetParent(target, false);
                hintHand.anchoredPosition = Vector2.zero;
                hintHand.SetAsLastSibling();
                hintHand.gameObject.SetActive(true);

                // a stuck child has likely stopped listening by now -- say the
                // question again alongside the tap hint, not just point at it
                card.RepeatPromptSilently();

                hintHand.DOKill();
                hintHand.localScale = Vector3.one;
                hintHand
                    .DOPunchScale(Vector3.one * -hintTapScale, hintTapDuration, 1, 0f)
                    .SetLoops(hintTapCount, LoopType.Restart)
                    .SetUpdate(true);

                yield return new WaitForSecondsRealtime(hintTapDuration * hintTapCount);

                hintHand.DOKill();
                hintHand.localScale = Vector3.one;
                hintHand.gameObject.SetActive(false);

                yield return new WaitForSecondsRealtime(hintRepeatDelay);
            }
        }

        private bool TapDetected()
        {
            if (Input.GetMouseButtonDown(0)) return true;
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (hintHand != null) hintHand.DOKill();
        }
    }
}
