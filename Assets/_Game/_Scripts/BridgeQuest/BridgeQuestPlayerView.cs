using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Shows the real Tappu IK rig inside the UI.
    ///
    /// The rig is SpriteRenderer-based (IKManager2D + SpriteSkin), and the bridge
    /// lives on a Screen Space - Overlay canvas, so the two cannot share a sorting
    /// order -- a world sprite always draws behind an overlay canvas. So instead of
    /// bending the canvas to the rig, a small dedicated camera films the rig onto a
    /// transparent RenderTexture and the walker slot displays that texture. The
    /// canvas keeps its render mode, BridgeBuilderUI keeps tweening a RectTransform,
    /// and the child still sees a fully animated character.
    ///
    /// The rig sits on its own layer, filmed by its own camera and culled from the
    /// main one, so where it stands in the world is irrelevant -- it is parked well
    /// clear of the play area.
    ///
    /// Idle while standing, walk while crossing. <see cref="BridgeBuilderUI"/> calls
    /// <see cref="PlayWalk"/> as the crossing starts and <see cref="PlayIdle"/> when
    /// the character arrives.
    /// </summary>
    public class BridgeQuestPlayerView : MonoBehaviour, ICrossingView
    {
        [Header("Refs")]
        [Tooltip("Animator on the Tappu rig root.")]
        [SerializeField] private Animator animator;

        [Tooltip("The camera that films the rig. Renders only the rig's layer, onto the render texture.")]
        [SerializeField] private Camera rigCamera;

        [Tooltip("The walker slot in the canvas. Its rect decides the texture's shape, so the rig is never stretched.")]
        [SerializeField] private RawImage target;

        [Header("Animator states")]
        [Tooltip("Tappu_Side has no parameters and no transitions, so states are played by name rather than driven by a bool.")]
        [SerializeField] private string idleState = "TappuIdle";
        [SerializeField] private string walkState = "TappuWalk";

        [Tooltip("Seconds to blend between idle and walk. 0 cuts straight to the state.")]
        [SerializeField] private float blendDuration = 0.15f;

        [Tooltip("For a rig with no separate idle clip (idleState == walkState, e.g. Bhide's single rowing loop): pauses the animator on PlayIdle instead of leaving the same motion looping while standing still. Leave off when idleState is its own distinct animation.")]
        [SerializeField] private bool freezeOnIdle = false;

        [Tooltip("Animator.speed while walking. A single crossing hop can be shorter than one full cycle of a long clip (e.g. Bhide's ~1.3s rowing loop against a ~0.5s hop), so an Animation Event timed for a specific frame (the paddle hitting water) may never be reached at normal speed before PlayIdle freezes it again. Raise this so the clip -- and its event -- reliably plays out within a hop. 1 = no change.")]
        [SerializeField] private float walkAnimatorSpeed = 1f;

        [Tooltip("For a clip authored as two alternating hops back to back in one loop (e.g. Goli's GoliSideHop -- left-foot lead, then right-foot lead), alternates PlayWalk() between starting the clip at normalized time 0 and 0.5 instead of always restarting at 0 -- so consecutive hops read as alternating strides rather than repeating the same half every time. Only meaningful alongside freezeOnIdle.")]
        [SerializeField] private bool alternateWalkHalves = false;

        private bool walkSecondHalf;

        [Header("Celebration")]
        [Tooltip("Optional front-facing rig (Tappu_Front). Swapped in for the walking rig once the crossing is done and filmed by the SAME camera onto the SAME texture -- the scene does not gain a second camera or a second RawImage. Left empty, the crossing just ends on the idle pose.")]
        [SerializeField] private Animator celebrationAnimator;

        [Tooltip("Tappu_Front's controller has no parameters and no transitions either, so the state is played by name.")]
        [SerializeField] private string celebrationState = "TappuFrontCelebration";

        [Header("Reaction")]
        [Tooltip("Optional -- an Animator state played once, uninterrupted, in response to something external rather than the idle/walk cycle (e.g. a stumble on a wrong answer). Left empty, PlayReaction() does nothing.")]
        [SerializeField] private string reactionState = "";

        [Tooltip("Seconds the reaction plays before returning to idle -- tune to the clip's actual length.")]
        [SerializeField] private float reactionDuration = 0.8f;

        private Coroutine reactionRoutine;

        [Header("Framing")]
        [Tooltip("Headroom around the rig, as a multiplier on its height. 1 = tight crop.")]
        [SerializeField] private float verticalMargin = 1.10f;

        [Tooltip("Render texture pixels per canvas pixel. 2-3 keeps the rig crisp on a high-DPI screen.")]
        [SerializeField] private int supersample = 3;

        [Tooltip("Animate on unscaled time. The crossing and every card in this game run with the world frozen, and a scaled Animator freezes with it.")]
        [SerializeField] private bool animateUnscaled = true;

        [Header("Events")]
        [Tooltip("Fires every time PlayWalk() runs -- i.e. the instant a crossing hop starts, before any animation has played. Wire scene-specific effects here (e.g. a bow wave that should start with the movement) instead of tying them to a mid-animation event.")]
        public UnityEvent onWalkStarted;

        private RenderTexture owned;   // only textures created here are destroyed here
        private bool warned;        private bool celebrating;


        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);

            // parked scenery until the crossing is over. It is on the rig layer, so a
            // stray active frame would film it instead of the walker.
            if (celebrationAnimator != null) celebrationAnimator.gameObject.SetActive(false);

            EnsureTexture();
            FrameRig();
        }

        /// <summary>
        /// The first pose is set here rather than in Awake on purpose. An Animator
        /// initialises to its controller's default state after Awake, so a Play() in
        /// Awake is silently thrown away -- which left the rig running Tappu_Side's
        /// default TappuRun instead of standing still.
        /// </summary>
        private void Start()
        {
            ApplyUpdateMode();
            PlayIdle();
        }

        private void ApplyUpdateMode()
        {
            if (animator == null || !animateUnscaled) return;

            // the crossing, every card and the storyboard all run on a frozen world;
            // a scaled Animator freezes with it and the character stops mid-stride
            if (animator.updateMode != AnimatorUpdateMode.UnscaledTime)
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        private void OnDestroy()
        {
            if (owned == null) return;

            // never leave the camera or the UI pointing at a texture that is about to die
            if (rigCamera != null && rigCamera.targetTexture == owned) rigCamera.targetTexture = null;
            if (target != null && target.texture == owned) target.texture = null;

            owned.Release();
            Destroy(owned);
            owned = null;
        }

        // ---- animation -------------------------------------------------------

        /// <summary>Standing still.</summary>
        public void PlayIdle()
        {
            Play(idleState);
            if (freezeOnIdle && animator != null) animator.speed = 0f;
        }

        /// <summary>Crossing the bridge.</summary>
        public void PlayWalk()
        {
            onWalkStarted?.Invoke();

            if (freezeOnIdle && animator != null)
            {
                animator.speed = walkAnimatorSpeed;

                // freezeOnIdle rigs share one state for idle and walk, so crossfading
                // into the state it is already paused on would just resume from
                // wherever it stopped -- restarting at a fixed point makes every hop's
                // animation (and any event timed within it) land at the same point
                // in the stroke, regardless of how long the previous hop was.
                float startTime = 0f;
                if (alternateWalkHalves)
                {
                    startTime = walkSecondHalf ? 0.5f : 0f;
                    walkSecondHalf = !walkSecondHalf;
                }

                if (animator.HasState(0, Animator.StringToHash(walkState))) animator.Play(walkState, 0, startTime);
                return;
            }

            Play(walkState);
        }

        /// <summary>True when a celebration rig is wired and there is something to play.</summary>
        public bool HasCelebration { get { return celebrationAnimator != null; } }

        /// <summary>
        /// Plays <see cref="reactionState"/> once on the walking rig's own Animator --
        /// a stumble, a flinch, anything reacting to something external -- then
        /// returns to idle. Cuts short and restarts cleanly if called again mid-play.
        /// Does nothing if no reaction state is wired.
        /// </summary>
        public void PlayReaction()
        {
            if (string.IsNullOrEmpty(reactionState) || animator == null) return;
            if (reactionRoutine != null) StopCoroutine(reactionRoutine);
            reactionRoutine = StartCoroutine(ReactionRoutine());
        }

        private IEnumerator ReactionRoutine()
        {
            if (animator.HasState(0, Animator.StringToHash(reactionState)))
            {
                float savedSpeed = animator.speed;
                animator.speed = 1f; // the reaction plays at its own pace, regardless of any walk-speed tuning
                animator.Play(reactionState, 0, 0f);

                yield return new WaitForSecondsRealtime(reactionDuration);

                animator.speed = savedSpeed;
            }

            PlayIdle();
            reactionRoutine = null;
        }

        /// <summary>
        /// Arrived, and pleased about it. The walking rig steps aside and the
        /// front-facing rig takes the camera -- one camera, one render texture, so
        /// the walker slot simply shows a different character without the canvas or
        /// BridgeBuilderUI knowing anything changed.
        ///
        /// Loops until <see cref="StopCelebration"/>: TappuFrontCelebration is a
        /// looping clip and the controller has no exit transition, so the caller owns
        /// how long the moment lasts.
        /// </summary>
        public void PlayCelebration()
        {
            if (celebrationAnimator == null) return;

            celebrating = true;

            if (animator != null) animator.gameObject.SetActive(false);

            GameObject rig = celebrationAnimator.gameObject;
            if (!rig.activeSelf) rig.SetActive(true);

            // the rig ships with its Animator switched off -- it is scenery until now
            if (!celebrationAnimator.enabled) celebrationAnimator.enabled = true;

            // frame before playing: the camera has to travel to the front rig, which
            // lives nowhere near the walking one
            FrameRig(celebrationAnimator.transform);
            PlayOn(celebrationAnimator, celebrationState);
        }

        /// <summary>
        /// Puts the walking rig back and re-frames the camera on it. Called on replay,
        /// so a second run of the mission does not start with Tappu still cheering.
        /// </summary>
        public void StopCelebration()
        {
            if (!celebrating) return;
            celebrating = false;

            if (celebrationAnimator != null) celebrationAnimator.gameObject.SetActive(false);
            if (animator != null) animator.gameObject.SetActive(true);

            FrameRig();
        }


        private void Play(string state)
        {
            PlayOn(animator, state);
        }

        private void PlayOn(Animator target, string state)
        {
            if (target == null || string.IsNullOrEmpty(state)) return;

            // a name that is not in the controller silently does nothing, which reads
            // as "the character froze" -- say so once instead
            if (!target.HasState(0, Animator.StringToHash(state)))
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning("[BridgeQuest] Animator '" + target.name + "' has no state '" + state
                        + "' on layer 0 -- the character will not animate.", this);
                }
                return;
            }

            if (animateUnscaled && target.updateMode != AnimatorUpdateMode.UnscaledTime)
                target.updateMode = AnimatorUpdateMode.UnscaledTime;

            if (blendDuration > 0f) target.CrossFadeInFixedTime(state, blendDuration, 0);
            else target.Play(state, 0, 0f);
        }

        // ---- render texture --------------------------------------------------

        /// <summary>
        /// Builds a render texture shaped like the walker slot, so the rig keeps its
        /// proportions whatever size the slot is set to. Re-runs when the slot is
        /// resized, which is why the size is read from the rect rather than authored.
        /// </summary>
        private void EnsureTexture()
        {
            if (rigCamera == null || target == null)
            {
                Debug.LogWarning("[BridgeQuest] BridgeQuestPlayerView needs both a rigCamera and a target RawImage.", this);
                return;
            }

            Rect r = target.rectTransform.rect;
            int ss = Mathf.Max(1, supersample);
            int w = Mathf.Max(16, Mathf.RoundToInt(r.width * ss));
            int h = Mathf.Max(16, Mathf.RoundToInt(r.height * ss));

            if (owned != null && owned.width == w && owned.height == h) return;

            if (owned != null)
            {
                if (rigCamera.targetTexture == owned) rigCamera.targetTexture = null;
                if (target.texture == owned) target.texture = null;
                owned.Release();
                Destroy(owned);
            }

            owned = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
            owned.name = "TappuWalker_RT";
            owned.antiAliasing = 2;
            owned.Create();

            rigCamera.targetTexture = owned;
            target.texture = owned;
        }

        /// <summary>
        /// Points the camera at the rig and zooms it to fit. Measured from the
        /// renderers rather than authored, so a taller character (Bhide, Gogi) drops
        /// in without re-tuning the camera by hand.
        /// </summary>
        /// <summary>Frames whichever rig is currently on show.</summary>
        private void FrameRig()
        {
            Transform current = celebrating && celebrationAnimator != null
                ? celebrationAnimator.transform
                : (animator != null ? animator.transform : transform);

            FrameRig(current);
        }

        /// <summary>
        /// Points the camera at a rig and zooms it to fit. Measured from the
        /// renderers rather than authored, so a taller character (Bhide, Gogi) drops
        /// in without re-tuning the camera by hand -- and so the front rig, which is
        /// eight times the walking rig's size and parked a thousand units up the
        /// world, needs no repositioning either.
        /// </summary>
        private void FrameRig(Transform rigRoot)
        {
            if (rigCamera == null || rigRoot == null) return;

            // measure the animated rig only, and only what is actually drawn. Measuring
            // the whole object would fold in anything else parked under it -- leftover
            // art from another game, a spare rig -- and zoom the camera out to nothing.
            SpriteRenderer[] parts = rigRoot.GetComponentsInChildren<SpriteRenderer>(false);
            if (parts == null || parts.Length == 0) return;

            bool any = false;
            Bounds b = new Bounds();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null || !parts[i].enabled) continue;
                if (!any) { b = parts[i].bounds; any = true; }
                else b.Encapsulate(parts[i].bounds);
            }
            if (!any) return;

            rigCamera.orthographic = true;
            rigCamera.orthographicSize = Mathf.Max(0.01f, b.size.y * 0.5f * Mathf.Max(1f, verticalMargin));

            Vector3 p = rigCamera.transform.position;
            rigCamera.transform.position = new Vector3(b.center.x, b.center.y, p.z);
        }

#if UNITY_EDITOR
        /// <summary>Keeps the editor preview honest while the slot is being laid out.</summary>
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            EnsureTexture();
            FrameRig();
        }
#endif
    }
}
