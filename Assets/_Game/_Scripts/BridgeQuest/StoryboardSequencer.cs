using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using DG.Tweening;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Bridge Quest's opening storyboard cutscene, presented as a growing pile of
    /// tilted photo cards over a starry sky. Panels are revealed one at a time: each
    /// card pops in (scale + fade) on top of the previous ones — earlier cards stay
    /// on screen, fanned out — while the caption pill shows that panel's one-liner
    /// and its voice-over hook fires.
    ///
    /// A panel auto-advances once its narration has finished (if a voicePlayerSource is wired
    /// up, playback completion — IVoiceLinePlayer.IsPlaying — drives the hold; otherwise a
    /// fixed <see cref="fallbackHold"/> is used). A tap reveals the next card early; the round
    /// Skip button jumps straight into the game from any panel.
    ///
    /// Drives itself from its own Start()/onStoryboardComplete rather than a
    /// Play(panels, callback) call -- see StoryboardSequencerBoard for how
    /// BridgeQuestFlow's IStoryboardPlayer hook fits around that.
    /// </summary>
    public class StoryboardSequencer : MonoBehaviour
    {
        [System.Serializable]
        public class Panel
        {
            public RectTransform card;      // the tilted photo card (fans in and stays)
            public CanvasGroup   cardGroup; // fades this card in
            [Tooltip("Optional decorative border on the card (a child of it). Nested CanvasGroups multiply alpha down the hierarchy, so this needs to be explicitly faded in step with cardGroup rather than just left at some fixed alpha — otherwise a border authored at alpha 0 stays invisible no matter what cardGroup does, and one authored at alpha 1 pops in at full opacity a frame ahead of the card's own fade.")]
            public CanvasGroup   borderGroup;
            [TextArea] public string narration;
            [Tooltip("VO key played when this panel appears, e.g. \"PC_Intro_Slide1\".")]
            public string voKey;

            [Tooltip("Optional second VO key, played immediately after voKey finishes (same card, same beat -- e.g. two short sentences recorded as separate clips rather than one). Empty plays just voKey, same as before.")]
            public string voKey2;

            [Tooltip("Deactivated the instant this panel gets dimmed for the next card -- e.g. a particle system that should stop rather than keep emitting, unseen, underneath every card that follows. Re-activated on ResetAndReplay/ResetForNextAttempt so a retry gets it again.")]
            public GameObject[] disableWhenDimmed;
        }

        [Header("Panels (revealed in order, stacking)")]
        public Panel[] panels;

        [Header("Ending card")]
        [Tooltip("A dedicated closing card (e.g. the character safely arrived), popped in via PlayEndingCard when the mission completes rather than as part of the opening sequence.")]
        public Panel endingPanel;

        [Header("UI references")]
        public CanvasGroup rootGroup;     // whole storyboard canvas (final fade-out)
        public CanvasGroup captionGroup;  // caption pill (fades per panel)
        public TMP_Text    narrationText; // the one-liner caption
        public Button      tapCatcher;    // full-screen; tap = reveal next card
        public Button      skipButton;    // jump straight to gameplay

        [Header("Voice-over")]
        [Tooltip("Invoked with each panel's voKey. Wire to a void(string) method that plays audio by key, e.g. PC_AudioManager.PlayVoiceLineVoid.")]
        public StringUnityEvent onPlayVoiceOver;
        [Tooltip("Optional. If the object handling onPlayVoiceOver also implements IVoiceLinePlayer, assign it " +
                 "here so a tap or Skip can cut the currently-playing line off instead of leaving it talking over the next card.")]
        [SerializeField] private MonoBehaviour voicePlayerSource;
        [SerializeField] private bool stopVoiceOverOnSkip = true;

        [Header("Events")]
        public UnityEvent onStoryboardStarted;
        public UnityEvent onStoryboardSkipped;
        public UnityEvent onStoryboardComplete;

        [Header("Timing")]
        public float cardPopDuration = 0.5f;
        public float captionFade     = 0.25f;
        [Tooltip("Extra beat held after the narration audio finishes.")]
        public float voEndPad        = 0.6f;
        [Tooltip("Hold per panel when no voicePlayerSource is available (or a panel has no voKey).")]
        public float fallbackHold    = 4.5f;
        public float rootFadeOut     = 0.4f;
        [Range(0.1f, 1f)] public float cardStartScale = 0.55f;
        [Tooltip("As each new card pops in, the previous card fades to this alpha instead of staying fully opaque — keeps the pile readable by visually pushing focus onto the newest card.")]
        [Range(0f, 1f)] public float previousCardDimAlpha = 0.4f;

        private IVoiceLinePlayer VoicePlayer => voicePlayerSource as IVoiceLinePlayer;

        Vector3[] _targetScale;
        Vector3 _endingTargetScale;
        bool _advance;   // tap -> next card
        bool _skipAll;   // skip button -> straight to gameplay
        bool _started;

        void Awake()
        {
            if (tapCatcher != null) tapCatcher.onClick.AddListener(OnTap);
            if (skipButton != null) skipButton.onClick.AddListener(OnSkip);

            // cache each card's authored scale, then hide them all
            if (panels != null)
            {
                _targetScale = new Vector3[panels.Length];
                for (int i = 0; i < panels.Length; i++)
                {
                    var p = panels[i];
                    _targetScale[i] = p.card != null ? p.card.localScale : Vector3.one;
                    if (p.cardGroup != null) p.cardGroup.alpha = 0f;
                    if (p.borderGroup != null) p.borderGroup.alpha = 0f;
                }
            }

            if (endingPanel != null && endingPanel.card != null)
            {
                _endingTargetScale = endingPanel.card.localScale;
                if (endingPanel.cardGroup != null) endingPanel.cardGroup.alpha = 0f;
                if (endingPanel.borderGroup != null) endingPanel.borderGroup.alpha = 0f;
            }
        }

        void Start()
        {
            if (_started) return;
            _started = true;

            if (rootGroup != null) { rootGroup.alpha = 1f; rootGroup.blocksRaycasts = true; }
            if (captionGroup != null) captionGroup.alpha = 0f;

            if (panels == null || panels.Length == 0) { Finish(); return; }

            onStoryboardStarted?.Invoke();
            StartCoroutine(PlaySequence());
        }

        public void OnTap()  => _advance = true;
        public void OnSkip() => _skipAll = true;

        /// <summary>Replays the whole sequence from panel 0 — for a game that can retry/restart
        /// (e.g. a mission retry) and needs its intro storyboard to play again. Start() only ever
        /// runs once per component lifetime, so a second playthrough needs this instead. Safe to
        /// call even before the first natural Start() has run.</summary>
        public void ResetAndReplay()
        {
            StopAllCoroutines();
            _advance = false;
            _skipAll = false;
            _started = true;

            if (panels != null)
            {
                for (int i = 0; i < panels.Length; i++)
                {
                    var p = panels[i];
                    if (p.card != null)
                    {
                        p.card.DOKill();
                        p.card.gameObject.SetActive(true); // undo the ending card's outright hide, if it ran
                        if (_targetScale != null) p.card.localScale = _targetScale[i];
                    }
                    if (p.cardGroup != null)
                    {
                        p.cardGroup.DOKill();
                        p.cardGroup.alpha = 0f;
                    }
                    if (p.borderGroup != null)
                    {
                        p.borderGroup.DOKill();
                        p.borderGroup.alpha = 0f;
                    }
                    EnableAll(p.disableWhenDimmed);
                }
            }

            if (endingPanel != null)
            {
                if (endingPanel.card != null)
                {
                    endingPanel.card.DOKill();
                    endingPanel.card.localScale = _endingTargetScale;
                }
                if (endingPanel.cardGroup != null)
                {
                    endingPanel.cardGroup.DOKill();
                    endingPanel.cardGroup.alpha = 0f;
                }
                if (endingPanel.borderGroup != null)
                {
                    endingPanel.borderGroup.DOKill();
                    endingPanel.borderGroup.alpha = 0f;
                }
            }

            if (rootGroup != null)
            {
                rootGroup.DOKill();
                rootGroup.alpha = 1f;
                rootGroup.blocksRaycasts = true;
            }
            if (captionGroup != null)
            {
                captionGroup.DOKill();
                captionGroup.alpha = 0f;
            }

            if (panels == null || panels.Length == 0) { Finish(); return; }

            onStoryboardStarted?.Invoke();
            StartCoroutine(PlaySequence());
        }

        private static void EnableAll(GameObject[] items)
        {
            if (items == null) return;
            foreach (GameObject go in items)
            {
                if (go != null) go.SetActive(true);
            }
        }

        IEnumerator PlaySequence()
        {
            for (int i = 0; i < panels.Length && !_skipAll; i++)
            {
                Panel p = panels[i];
                Panel prev = i > 0 ? panels[i - 1] : null;
                yield return RevealPanel(p, _targetScale[i], prev);
            }

            if (_skipAll)
            {
                if (stopVoiceOverOnSkip) VoicePlayer?.StopVoice();
                onStoryboardSkipped?.Invoke();
            }

            // Hand off to the game BEFORE uncovering the scene — whatever onStoryboardComplete
            // triggers (e.g. the entrance sequence) can start moving while the storyboard is still
            // fading, so by the time it's gone the scene is already mid-animation, not popping in.
            if (rootGroup != null) rootGroup.blocksRaycasts = false;
            Finish();

            if (rootGroup != null)
                yield return rootGroup.DOFade(0f, rootFadeOut).SetUpdate(true).WaitForCompletion();
        }

        /// <summary>
        /// Pops in a single dedicated closing card on top of whatever the opening
        /// sequence left on screen, then fades the whole storyboard out again. Meant
        /// to be called once, when the mission actually completes -- see
        /// StoryboardSequencerBoard, which routes BridgeQuestFlow's closing-storyboard
        /// call here instead of into the (already finished) opening PlaySequence.
        /// </summary>
        public void PlayEndingCard(Action onComplete)
        {
            StartCoroutine(PlayEndingCardRoutine(onComplete));
        }

        private IEnumerator PlayEndingCardRoutine(Action onComplete)
        {
            if (endingPanel == null || endingPanel.card == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            _advance = false;
            _skipAll = false;

            if (rootGroup != null) { rootGroup.alpha = 1f; rootGroup.blocksRaycasts = true; }

            // the ending card is the whole story now -- hide the opening pile outright
            // rather than dimming it, so nothing competes with it for attention
            if (panels != null)
            {
                foreach (Panel p in panels)
                {
                    if (p.card != null) p.card.gameObject.SetActive(false);
                }
            }

            yield return RevealPanel(endingPanel, _endingTargetScale, null);

            if (rootGroup != null) rootGroup.blocksRaycasts = false;
            onComplete?.Invoke();

            if (rootGroup != null)
                yield return rootGroup.DOFade(0f, rootFadeOut).SetUpdate(true).WaitForCompletion();
        }

        /// <summary>
        /// Quietly puts every card back to its just-Awake state -- re-enabled (undoing
        /// the ending card's outright hide), hidden at alpha 0, authored scale -- but
        /// does NOT play anything, unlike <see cref="ResetAndReplay"/>. Meant for a
        /// mission retry/replay that should NOT watch the opening storyboard again:
        /// wire this to BridgeQuestFlow.onMissionRestarted so a scene whose ending
        /// card already ran once (a win, then Replay) does not stay permanently
        /// missing its opening cards on the next attempt.
        /// </summary>
        public void ResetForNextAttempt()
        {
            if (panels != null)
            {
                for (int i = 0; i < panels.Length; i++)
                {
                    Panel p = panels[i];
                    if (p.card != null)
                    {
                        p.card.DOKill();
                        p.card.gameObject.SetActive(true);
                        if (_targetScale != null) p.card.localScale = _targetScale[i];
                    }
                    if (p.cardGroup != null)
                    {
                        p.cardGroup.DOKill();
                        p.cardGroup.alpha = 0f;
                    }
                    if (p.borderGroup != null)
                    {
                        p.borderGroup.DOKill();
                        p.borderGroup.alpha = 0f;
                    }
                    EnableAll(p.disableWhenDimmed);
                }
            }

            if (endingPanel != null)
            {
                if (endingPanel.card != null)
                {
                    endingPanel.card.DOKill();
                    endingPanel.card.localScale = _endingTargetScale;
                }
                if (endingPanel.cardGroup != null)
                {
                    endingPanel.cardGroup.DOKill();
                    endingPanel.cardGroup.alpha = 0f;
                }
                if (endingPanel.borderGroup != null)
                {
                    endingPanel.borderGroup.DOKill();
                    endingPanel.borderGroup.alpha = 0f;
                }
            }

            if (rootGroup != null)
            {
                rootGroup.DOKill();
                rootGroup.alpha = 0f;
                rootGroup.blocksRaycasts = false;
            }
            if (captionGroup != null)
            {
                captionGroup.DOKill();
                captionGroup.alpha = 0f;
            }
        }

        /// <summary>Pops in one card, fires its VO, and holds until the line (or fallbackHold) finishes.</summary>
        private IEnumerator RevealPanel(Panel p, Vector3 targetScale, Panel prev)
        {
            _advance = false;

            if (narrationText != null) narrationText.text = p.narration;
            if (captionGroup != null)
            {
                captionGroup.alpha = 0f;
                captionGroup.DOFade(1f, captionFade).SetUpdate(true);
            }

            // pop the card onto the pile (it stays for the rest of the storyboard)
            if (p.card != null && p.cardGroup != null)
            {
                p.card.localScale = targetScale * cardStartScale;
                p.cardGroup.alpha = 0f;
                p.cardGroup.DOFade(1f, cardPopDuration).SetUpdate(true);
                p.card.DOScale(targetScale, cardPopDuration).SetEase(Ease.OutBack).SetUpdate(true);
            }
            if (p.borderGroup != null)
            {
                p.borderGroup.alpha = 0f;
                p.borderGroup.DOFade(1f, cardPopDuration).SetUpdate(true);
            }

            // dim the previous card (and its border) so the newest one reads as the current focus
            if (prev != null)
            {
                if (prev.cardGroup != null) prev.cardGroup.DOFade(previousCardDimAlpha, cardPopDuration).SetUpdate(true);
                if (prev.borderGroup != null) prev.borderGroup.DOFade(previousCardDimAlpha, cardPopDuration).SetUpdate(true);

                // a CanvasGroup fade only dims UI Graphics -- something like a raw
                // ParticleSystem child ignores it completely and would keep emitting,
                // unseen, under every card that follows. Turn those off outright.
                if (prev.disableWhenDimmed != null)
                {
                    foreach (GameObject go in prev.disableWhenDimmed)
                    {
                        if (go != null) go.SetActive(false);
                    }
                }
            }

            // fire the narration VO; wait for it to finish (if we can tell) before holding the pad
            bool hasVoKey = !string.IsNullOrEmpty(p.voKey);
            if (hasVoKey) onPlayVoiceOver?.Invoke(p.voKey);

            IVoiceLinePlayer player = VoicePlayer;
            if (hasVoKey && player != null)
            {
                while (player.IsPlaying && !_advance && !_skipAll) yield return null;

                if (!string.IsNullOrEmpty(p.voKey2) && !_advance && !_skipAll)
                {
                    onPlayVoiceOver?.Invoke(p.voKey2);
                    while (player.IsPlaying && !_advance && !_skipAll) yield return null;
                }

                float pad = 0f;
                while (pad < voEndPad && !_advance && !_skipAll)
                {
                    pad += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                float t = 0f;
                while (t < fallbackHold && !_advance && !_skipAll)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (_advance) VoicePlayer?.StopVoice();   // tapped early — cut the line off rather than let it talk over the next card
        }

        void Finish() => onStoryboardComplete?.Invoke();
    }
}
