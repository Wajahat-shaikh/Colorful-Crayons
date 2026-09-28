using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// The question card: a prompt the child hears, and three options they tap.
    ///
    /// Bridge Quest is no-fail by design. A wrong tap shakes, plays a gentle retry
    /// line and re-arms -- it never costs anything and there is no attempt limit.
    /// This mirrors RocketRun's "wrong pickup is a free mistake" tier; RocketRun's
    /// other tier (hazard crash costs a life) has no equivalent here, which is why
    /// Bridge Quest has no lose screen at all.
    ///
    /// Presentation order is shuffled every time. Without that, a child learns the
    /// position rather than the letter -- the single most important thing this
    /// component does.
    /// </summary>
    public class QuestionCardUI : MonoBehaviour
    {
        /// <summary>One option slot in the scene. Art and label are mutually exclusive.</summary>
        [Serializable]
        public class OptionView
        {
            public Button button;
            public RectTransform rect;
            public Image art;
            public TextMeshProUGUI label;

            [Tooltip("Plain colour swatch, used by Colour questions.")]
            public Image swatch;

            [Tooltip("Background fill flashed red on a wrong answer. Usually the option's " +
                     "own 'Fill' image. Left empty, the wrong tap still shakes but does not flash.")]
            public Image fillFlash;
        }

        [Header("Refs")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform card;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private Image promptArt;

        [Tooltip("Re-speaks the question. Essential -- pre-readers cannot re-read the prompt.")]
        [SerializeField] private Button replayButton;

        [Tooltip("Exactly three, per the GDD.")]
        [SerializeField] private OptionView[] options = new OptionView[3];

        [Header("Feedback")]
        [SerializeField] private float popInDuration = 0.35f;
        [SerializeField] private float shakeDuration = 0.4f;
        [SerializeField] private float shakeStrength = 24f;

        [Tooltip("Taps ignored for this long after a wrong answer, so a mashing child cannot fire the retry line five times.")]
        [SerializeField] private float wrongLockout = 0.5f;

        [Tooltip("After the retry line, speak the question again. A pre-reader who guessed wrong has no way to re-read the prompt, and the alternative is guessing at silence.")]
        [SerializeField] private bool replayPromptOnWrong = true;

        [Tooltip("Beat between the retry line ending and the question starting. One shared AudioSource, so this is a pause between two clips, not a crossfade.")]
        [SerializeField] private float promptReplayGap = 0.15f;

        [Tooltip("Beat between the correct tap and handing control back, so the praise line is not cut off by the plank card.")]
        [SerializeField] private float correctHold = 0.6f;

        [Header("Wrong Answer Flash")]
        [Tooltip("Colour the tapped option's fill snaps to on a wrong answer.")]
        [SerializeField] private Color wrongFlashColor = new Color(0.93f, 0.20f, 0.20f, 1f);

        [Tooltip("Seconds the option stays fully red before it starts fading back -- 'for sometime', not an instant blink.")]
        [SerializeField] private float wrongFlashHoldDuration = 0.45f;

        [Tooltip("Seconds the red takes to fade back to the option's normal colour once the hold ends.")]
        [SerializeField] private float wrongFlashFadeDuration = 0.3f;

        [Header("Correct Answer Flash")]
        [Tooltip("Colour the tapped option's fill snaps to on a correct answer -- same shake-and-flash language as a wrong tap, just green instead of red. Holds until Hide() clears the card (after correctHold), no separate fade needed.")]
        [SerializeField] private Color correctFlashColor = new Color(0.25f, 0.80f, 0.30f, 1f);

        [Header("Accessibility")]
        [Tooltip("Colour questions must not rely on hue alone -- red/green is the most common deficiency. Leave on to show the colour's name under the swatch.")]
        [SerializeField] private bool labelColourOptions = true;

        private QuestionData current;
        private int[] order;          // presentation slot -> authored option index
        private Action onAnswered;
        private bool accepting;
        private bool answered;

        // each option's authored fill colour, captured once before anything flashes it --
        // so the flash always fades back to what the scene actually authored rather than
        // a hardcoded guess
        private Color[] optionFillDefaultColors;

        // the graphic actually tinted on a wrong tap, resolved once per slot -- see
        // ResolveFlashGraphic for why it is not always the option's Fill
        private Image[] optionFlashGraphics;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            CacheFillColors();
        }

        private void CacheFillColors()
        {
            if (options == null) return;

            optionFlashGraphics = new Image[options.Length];
            optionFillDefaultColors = new Color[options.Length];

            for (int i = 0; i < options.Length; i++)
            {
                Image g = ResolveFlashGraphic(options[i]);
                optionFlashGraphics[i] = g;
                if (g != null) optionFillDefaultColors[i] = g.color;
            }
        }

        /// <summary>
        /// The image a wrong tap turns red.
        ///
        /// Normally the option's own 'Fill' -- but a scene is free to switch Fill off
        /// and let the tile's plain rounded rect show instead, which is what this one
        /// does. Tinting a deactivated object costs nothing and shows nothing, so the
        /// wrong answer would read as a shake and no colour at all. So: Fill when it is
        /// actually on screen, otherwise the tile's own Image, which always is.
        ///
        /// Resolved once in Awake rather than per tap. Nothing toggles Fill at runtime,
        /// and the cached default colour has to belong to the same graphic that gets
        /// tinted or the fade-back would land on another object's colour.
        /// </summary>
        private Image ResolveFlashGraphic(OptionView view)
        {
            if (view == null) return null;

            if (view.fillFlash != null && view.fillFlash.gameObject.activeSelf)
                return view.fillFlash;

            if (view.rect != null)
            {
                Image own = view.rect.GetComponent<Image>();
                if (own != null && own.enabled) return own;
            }

            return view.fillFlash;
        }

        /// <summary>Kills any in-flight flash on one slot and puts its colour back.</summary>
        private void RestoreFlashGraphic(int slot)
        {
            if (optionFlashGraphics == null || slot < 0 || slot >= optionFlashGraphics.Length) return;

            Image g = optionFlashGraphics[slot];
            if (g == null) return;

            g.DOKill();
            g.color = optionFillDefaultColors[slot];
        }

        private void OnEnable()
        {
            if (replayButton != null) replayButton.onClick.AddListener(RepeatPrompt);
            HookOptions(true);
        }

        private void OnDisable()
        {
            if (replayButton != null) replayButton.onClick.RemoveListener(RepeatPrompt);
            HookOptions(false);
        }

        private void HookOptions(bool add)
        {
            if (options == null) return;

            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == null || options[i].button == null) continue;

                int slot = i; // capture
                if (add) options[i].button.onClick.AddListener(delegate { OnOptionTapped(slot); });
                else options[i].button.onClick.RemoveAllListeners();
            }
        }

        /// <summary>
        /// Shows a question and waits for the child to find the answer.
        /// <paramref name="onCorrect"/> fires once, on the correct tap -- wrong taps
        /// never end the question.
        /// </summary>
        public void Present(QuestionData question, Action onCorrect)
        {
            if (question == null || !question.IsValid)
            {
                Debug.LogWarning("[BridgeQuest] Asked to present an invalid question -- skipping it.", this);
                if (onCorrect != null) onCorrect();
                return;
            }

            current = question;
            onAnswered = onCorrect;
            answered = false;

            BuildOrder(question.options.Length);
            Paint();

            if (root != null) root.SetActive(true);

            StopAllCoroutines();
            repeatRoutine = null;
            wrongVoiceRoutine = null;
            StartCoroutine(PopInThenAccept());
        }

        public void Hide()
        {
            StopAllCoroutines();
            repeatRoutine = null;
            wrongVoiceRoutine = null;
            accepting = false;
            if (card != null) card.DOKill();
            ResetFillFlashes();
            if (root != null) root.SetActive(false);
        }

        /// <summary>Stops any in-flight red flash and puts every option's fill back to its authored colour.</summary>
        private void ResetFillFlashes()
        {
            if (optionFlashGraphics == null) return;

            for (int i = 0; i < optionFlashGraphics.Length; i++) RestoreFlashGraphic(i);
        }

        /// <summary>Fisher-Yates. Re-rolled per presentation, so a replayed mission never repeats the layout.</summary>
        private void BuildOrder(int count)
        {
            order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;

            for (int i = count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }
        }

        private void Paint()
        {
            if (promptText != null) promptText.text = current.prompt;

            if (promptArt != null)
            {
                promptArt.sprite = current.promptArt;
                promptArt.enabled = current.promptArt != null;
            }

            int slots = options != null ? options.Length : 0;

            for (int slot = 0; slot < slots; slot++)
            {
                OptionView view = options[slot];
                if (view == null) continue;

                bool used = order != null && slot < order.Length;
                if (view.button != null) view.button.gameObject.SetActive(used);
                if (!used) continue;

                AnswerOption opt = current.options[order[slot]];

                if (view.rect != null)
                {
                    view.rect.DOKill();
                    view.rect.localRotation = Quaternion.identity;
                    view.rect.localScale = Vector3.one;
                }

                // a fresh question starts with every fill back at its authored colour --
                // a wrong tap from the previous question must never bleed into this one
                if (view.fillFlash != null)
                {
                    view.fillFlash.DOKill();
                    if (optionFillDefaultColors != null && slot < optionFillDefaultColors.Length)
                        view.fillFlash.color = optionFillDefaultColors[slot];
                }

                RestoreFlashGraphic(slot);

                bool isColour = opt.useColour;

                if (view.swatch != null)
                {
                    view.swatch.gameObject.SetActive(isColour);
                    if (isColour) view.swatch.color = opt.colour;
                }

                if (view.art != null)
                {
                    view.art.sprite = opt.art;
                    view.art.enabled = !isColour && opt.art != null;
                }

                if (view.label != null)
                {
                    // a colour swatch still gets its name underneath unless that is
                    // switched off -- hue alone is not an accessible cue
                    bool showLabel = !string.IsNullOrEmpty(opt.label)
                        && (!isColour || labelColourOptions)
                        && (isColour || opt.art == null);

                    view.label.gameObject.SetActive(showLabel);
                    if (showLabel) view.label.text = opt.label;
                }

                if (view.button != null) view.button.interactable = true;
            }
        }

        private IEnumerator PopInThenAccept()
        {
            accepting = false;

            if (card != null)
            {
                card.DOKill();
                card.localScale = Vector3.zero;
                Tween t = card.DOScale(1f, popInDuration).SetEase(Ease.OutBack, 1.5f).SetUpdate(true);
                yield return t.WaitForCompletion();
                card.localScale = Vector3.one;
            }

            // the prompt is only spoken once the card has landed -- a line that starts
            // under a moving card reads as belonging to whatever came before it.
            // PlayWhenReady rather than Play: the very first question can land before
            // the category bundle has finished loading, and a dropped first line is
            // worse than a short wait.
            BridgeQuestVoice.PlayWhenReady(this, current.promptVoiceKey);

            accepting = true;
        }

        private Coroutine repeatRoutine;
        private Coroutine wrongVoiceRoutine;

        private void RepeatPrompt()
        {
            BridgeQuestSfx.Tap();
            RepeatPromptSilently();
        }

        /// <summary>
        /// Same replay as the tap-to-repeat button, minus the tap SFX -- for anything
        /// that should re-speak the question on its own rather than in response to a
        /// deliberate tap (e.g. BridgeQuestTutorial's stuck-nudge, repeating the
        /// question each time the hint hand reappears).
        /// </summary>
        public void RepeatPromptSilently()
        {
            if (current == null || answered) return;

            // a deliberate/automatic repeat outranks anything a wrong answer queued --
            // otherwise the two would fight over the one AudioSource
            if (wrongVoiceRoutine != null) { StopCoroutine(wrongVoiceRoutine); wrongVoiceRoutine = null; }
            if (repeatRoutine != null) StopCoroutine(repeatRoutine);
            repeatRoutine = StartCoroutine(RepeatRoutine());
        }

        /// <summary>
        /// Speaks 'Listen again.' and then the question itself. The two cannot be
        /// fired back to back: RuntimeAudioLoader plays every line through one shared
        /// AudioSource and calls Stop() first, so the second call would swallow the
        /// first. Realtime waits, because the card is up while Time.timeScale is 0.
        /// </summary>
        private IEnumerator RepeatRoutine()
        {
            BridgeQuestAudioMapper voice = BridgeQuestVoice.Mapper;
            float lead = voice != null ? BridgeQuestVoice.PlayAndGetLength(voice.RepeatQuestion) : 0f;
            if (lead > 0f) yield return new WaitForSecondsRealtime(lead);

            // the child may have answered while 'Listen again.' was still speaking
            if (current == null || answered) { repeatRoutine = null; yield break; }

            BridgeQuestVoice.PlayWhenReady(this, current.promptVoiceKey);
            repeatRoutine = null;
        }

        private void OnOptionTapped(int slot)
        {
            if (!accepting || answered || current == null) return;
            if (order == null || slot >= order.Length) return;

            int authored = order[slot];
            AnswerOption opt = current.options[authored];

            if (authored == current.correctIndex)
            {
                answered = true;
                accepting = false;
                StartCoroutine(CorrectRoutine(slot, opt));
                return;
            }

            StartCoroutine(WrongRoutine(slot, opt));
        }

        private IEnumerator CorrectRoutine(int slot, AnswerOption opt)
        {
            LockAll();

            OptionView view = options[slot];
            if (view != null && view.rect != null)
            {
                view.rect.DOKill();
                view.rect.DOPunchScale(Vector3.one * 0.25f, 0.35f, 8, 0.8f).SetUpdate(true);
            }

            FlashCorrect(slot);

            BridgeQuestAudioMapper voice = BridgeQuestVoice.Mapper;
            if (voice != null) BridgeQuestVoice.Play(voice.GetRandomCorrect());

            BridgeQuestGameManager.RaiseCorrectAnswer(current.type);

            yield return new WaitForSecondsRealtime(correctHold);

            Hide();

            Action cb = onAnswered;
            onAnswered = null;
            if (cb != null) cb();
        }

        private IEnumerator WrongRoutine(int slot, AnswerOption opt)
        {
            accepting = false;

            OptionView view = options[slot];
            if (view != null && view.rect != null)
            {
                view.rect.DOKill();
                view.rect.localRotation = Quaternion.identity;
                view.rect
                    .DOShakeAnchorPos(shakeDuration, new Vector2(shakeStrength, 0f), 12, 90f, false, true)
                    .SetUpdate(true);

                // NB: the particle puff that used to fire here was removed with the
                // rest of the FX layer. Nothing else in this method depended on it.
            }

            FlashWrong(view, slot);

            // the retry line, and then the question again. Not fired from here: the two
            // clips have to be sequenced, and this routine must not be the thing that
            // waits for them -- see WrongVoiceRoutine.
            if (repeatRoutine != null) { StopCoroutine(repeatRoutine); repeatRoutine = null; }
            if (wrongVoiceRoutine != null) StopCoroutine(wrongVoiceRoutine);
            wrongVoiceRoutine = StartCoroutine(WrongVoiceRoutine());

            BridgeQuestGameManager.RaiseWrongAnswer(current.type);

            yield return new WaitForSecondsRealtime(wrongLockout);

            // no penalty, no attempt counter, nothing removed from the board --
            // the child simply gets to try again. Deliberately re-armed on the short
            // lockout rather than when the voice finishes: a child who has already
            // spotted the answer should not have to sit through the question again.
            if (!answered) accepting = true;
        }

        /// <summary>
        /// 'Try again' and then the question itself.
        ///
        /// The two cannot be fired back to back. RuntimeAudioLoader plays every line
        /// through one shared AudioSource and calls Stop() first, so a second call
        /// swallows the first and the child would hear only the question -- the same
        /// trap RepeatRoutine and the closing storyboard both had. So the retry line's
        /// length is measured and waited out before the prompt starts.
        ///
        /// This runs beside WrongRoutine rather than inside it, because WrongRoutine
        /// owns the tap lockout and must re-arm on its own short timer whatever the
        /// audio is doing.
        ///
        /// Realtime waits, because a card is up whenever this runs.
        /// </summary>
        private IEnumerator WrongVoiceRoutine()
        {
            BridgeQuestAudioMapper voice = BridgeQuestVoice.Mapper;

            float lead = voice != null
                ? BridgeQuestVoice.PlayAndGetLength(voice.GetRandomWrong())
                : 0f;

            if (!replayPromptOnWrong) { wrongVoiceRoutine = null; yield break; }

            if (lead > 0f) yield return new WaitForSecondsRealtime(lead);
            if (promptReplayGap > 0f) yield return new WaitForSecondsRealtime(promptReplayGap);

            // The child may have found the answer while the retry line was still
            // speaking -- taps re-arm after wrongLockout, long before the audio ends.
            // The praise line is already in flight by then and must not be talked over.
            if (current == null || answered) { wrongVoiceRoutine = null; yield break; }

            BridgeQuestVoice.PlayWhenReady(this, current.promptVoiceKey);
            wrongVoiceRoutine = null;
        }

        /// <summary>
        /// Snaps the tapped option's fill to <see cref="wrongFlashColor"/> and holds it
        /// there for <see cref="wrongFlashHoldDuration"/> before fading back to whatever
        /// colour the scene authored for it -- a plain swatch option keeps its own
        /// colour once the flash clears. Runs on unscaled time like the rest of the
        /// card's feedback, since the world is frozen behind it.
        /// </summary>
        private void FlashWrong(OptionView view, int slot)
        {
            if (optionFlashGraphics == null || slot < 0 || slot >= optionFlashGraphics.Length) return;

            Image g = optionFlashGraphics[slot];
            if (g == null) return;

            Color original = optionFillDefaultColors[slot];

            g.DOKill();
            g.color = wrongFlashColor;
            g.DOColor(original, wrongFlashFadeDuration)
             .SetDelay(wrongFlashHoldDuration)
             .SetUpdate(true);
        }

        /// <summary>
        /// Snaps the tapped option's fill to <see cref="correctFlashColor"/> -- the
        /// same shake-and-flash language as a wrong tap, just green. No fade-back
        /// tween needed: Hide() (called shortly after, once correctHold elapses)
        /// already restores every slot's fill to its authored colour via
        /// ResetFillFlashes(), and the card is gone by the time the next question
        /// paints these slots again anyway.
        /// </summary>
        private void FlashCorrect(int slot)
        {
            if (optionFlashGraphics == null || slot < 0 || slot >= optionFlashGraphics.Length) return;

            Image g = optionFlashGraphics[slot];
            if (g == null) return;

            g.DOKill();
            g.color = correctFlashColor;
        }

        private void LockAll()
        {
            if (options == null) return;
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] != null && options[i].button != null)
                {
                    options[i].button.interactable = false;
                }
            }
        }

        /// <summary>The slot the correct answer landed in this shuffle. Used by the tutorial to point at it.</summary>
        public RectTransform CorrectSlotRect
        {
            get
            {
                if (current == null || order == null || options == null) return null;

                for (int slot = 0; slot < order.Length && slot < options.Length; slot++)
                {
                    if (order[slot] == current.correctIndex && options[slot] != null)
                    {
                        return options[slot].rect;
                    }
                }
                return null;
            }
        }
    }
}
