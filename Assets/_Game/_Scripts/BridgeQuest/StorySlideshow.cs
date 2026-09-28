using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TMKOC.BridgeQuest
{
/// <summary>
/// Generic, modular story/tutorial slideshow. Feed it a list of StorySlide
/// entries (image + text + voice-over key) and it shows them one at a time,
/// waiting for a minimum display time and — if a voice-over player is wired
/// up — for the voice line to finish, before advancing. A skip button bails
/// out of the whole sequence at any point.
///
/// Not tied to any specific audio system: hook onPlayVoiceOver up (in the
/// Inspector) to any void(string) method that plays audio by name.
/// </summary>
/// <summary>Concrete UnityEvent&lt;string&gt; subclass — required for it to serialize/draw properly in the Inspector.</summary>
[System.Serializable]
public class StringUnityEvent : UnityEvent<string> { }

/// <summary>Concrete UnityEvent&lt;int&gt; subclass — required for it to serialize/draw properly in the Inspector.</summary>
[System.Serializable]
public class IntUnityEvent : UnityEvent<int> { }

public class StorySlideshow : MonoBehaviour
{
    [Header("Slides")]
    [SerializeField] private List<StorySlide> slides = new List<StorySlide>();
    [SerializeField] private bool playOnStart = true;
    [Tooltip("If off, the slideshow never shows — onSlideshowComplete fires immediately on Start so gameplay begins right away.")]
    [SerializeField] private bool showSlideshow = true;

    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image slideImage;
    [SerializeField] private TMP_Text slideText;
    [SerializeField] private Button skipButton;

    [Header("Voice-over")]
    [Tooltip("Invoked with each slide's voiceOverKey. Wire to a void(string) method that plays audio by name.")]
    public StringUnityEvent onPlayVoiceOver;    

    [Tooltip("Optional. If the object handling onPlayVoiceOver also implements IVoiceLinePlayer, assign it " +
             "here so the slideshow can wait for the line to finish (and stop it on skip) before advancing.")]
    [SerializeField] private MonoBehaviour voicePlayerSource;
    [SerializeField] private bool stopVoiceOverOnSkip = true;

    [Header("Events")]
    public UnityEvent onSlideshowStarted;
    public IntUnityEvent onSlideShown;
    public UnityEvent onSlideshowSkipped;
    public UnityEvent onSlideshowComplete;

    private IVoiceLinePlayer VoicePlayer => voicePlayerSource as IVoiceLinePlayer;

    private Coroutine runRoutine;

    public int CurrentIndex { get; private set; } = -1;
    public bool IsPlaying => runRoutine != null;

    // ── Lifecycle ────────────────────────────────────────────────────────

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (skipButton != null) skipButton.onClick.AddListener(Skip);
    }

    private void OnDisable()
    {
        if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
    }

    private void Start()
    {
        if (!showSlideshow)
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            // Deferred a frame: firing onSlideshowComplete synchronously here can beat
            // listeners (e.g. ZE_GameManager.Start()) that subscribe to it in their own
            // Start(), since MonoBehaviour Start() order across scripts isn't guaranteed —
            // the event would fire with nothing subscribed yet and gameplay would never begin.
            StartCoroutine(FireCompleteNextFrame());
            return;
        }

        if (playOnStart) Play();
    }

    private IEnumerator FireCompleteNextFrame()
    {
        yield return null;
        onSlideshowComplete?.Invoke();
    }

    // ── Public API ───────────────────────────────────────────────────────

    /// <summary>Play the slides assigned in the Inspector.</summary>
    public void Play() => Play(slides);

    /// <summary>Play a specific slide list, replacing whatever was assigned in the Inspector.</summary>
    public void Play(List<StorySlide> slidesToPlay)
    {
        if (slidesToPlay != null) slides = slidesToPlay;
        if (runRoutine != null) StopCoroutine(runRoutine);

        if (panelRoot != null) panelRoot.SetActive(true);

        onSlideshowStarted?.Invoke();
        runRoutine = StartCoroutine(RunSlideshow());
    }

    /// <summary>Bail out of the whole sequence — hooked up to the skip button.</summary>
    public void Skip()
    {
        if (runRoutine == null) return;

        StopCoroutine(runRoutine);
        runRoutine = null;
        CurrentIndex = -1;

        if (stopVoiceOverOnSkip) VoicePlayer?.StopVoice();
        if (panelRoot != null) panelRoot.SetActive(false);

        onSlideshowSkipped?.Invoke();
        onSlideshowComplete?.Invoke();
    }

    // ── Playback ─────────────────────────────────────────────────────────

    private IEnumerator RunSlideshow()
    {
        for (int i = 0; i < slides.Count; i++)
        {
            CurrentIndex = i;
            ShowSlide(slides[i]);
            onSlideShown?.Invoke(i);

            yield return WaitForSlide(slides[i]);
        }

        runRoutine = null;
        CurrentIndex = -1;
        if (panelRoot != null) panelRoot.SetActive(false);
        onSlideshowComplete?.Invoke();
    }

    private void ShowSlide(StorySlide slide)
    {
        if (slideImage != null)
        {
            slideImage.sprite  = slide.image;
            slideImage.enabled = slide.image != null;
        }

        if (slideText != null) slideText.text = slide.text;

       /* var subscribers = onPlayVoiceOver.GetInvocationList();
        Debug.Log($"[VoiceOver] Invoking onPlayVoiceOver for key '{slide.voiceOverKey}' " + $"with {subscribers.Length} subscriber(s): " +
                  string.Join(", ", subscribers.Select(d => $"{d.Target}.{d.Method.Name}")));*/
        if (!string.IsNullOrEmpty(slide.voiceOverKey)) onPlayVoiceOver?.Invoke(slide.voiceOverKey);
    }

    private IEnumerator WaitForSlide(StorySlide slide)
    {
        float elapsed = 0f;
        while (elapsed < slide.minDisplayDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        IVoiceLinePlayer player = VoicePlayer;
        if (player != null)
        {
            while (player.IsPlaying) yield return null;
        }
    }
}
}
