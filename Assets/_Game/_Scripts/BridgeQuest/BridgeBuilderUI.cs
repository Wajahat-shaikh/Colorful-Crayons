using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// The bridge itself -- and, more importantly, the progress bar.
    ///
    /// This is the best idea in the GDD: a pre-reader cannot parse "3/5" but can see
    /// a gap closing. So the plank IS the progress indicator; there is no separate
    /// counter, and there should not be one.
    ///
    /// Planks are pre-placed in the scene along the span and start hidden. A correct
    /// answer drops in the next GROUP of planks, in order, left to right.
    ///
    /// Planks and questions are deliberately decoupled. The deck art is a fan of 11
    /// narrow planks, but a mission only asks 5 questions, so one answer has to be
    /// worth more than one plank -- see <see cref="plankGroupSizes"/>. Sizing the
    /// groups 3,2,2,2,2 closes an 11-plank span over 5 questions and still lands the
    /// final plank on the final answer.
    /// </summary>
    public class BridgeBuilderUI : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Every plank across the span, laid out left to right. Hidden at mission start.\n" +
                 "This is the whole deck, NOT one per question -- plankGroupSizes decides how many\n" +
                 "of these each correct answer is worth.")]
        [SerializeField] private Image[] plankSlots;

        [Tooltip("Optional -- the character walks across this once the span is closed.")]
        [SerializeField] private RectTransform walker;

        [SerializeField] private RectTransform walkStart;
        [SerializeField] private RectTransform walkEnd;

        [Tooltip("Optional -- the character rig displayed in the walker. Idle while the bridge is\n" +
                 "being built, walk for the crossing. Left empty, the walker just slides.\n\n" +
                 "Serialized as a MonoBehaviour rather than a concrete type so any component\n" +
                 "implementing ICrossingView can be dropped in here -- the Animator-driven\n" +
                 "BridgeQuestPlayerView by default, or a rig-less view built from plain UI parts\n" +
                 "(ScooterCrossingView) for a character with no rig at all.")]
        [SerializeField] private MonoBehaviour playerView;

        private ICrossingView PlayerView { get { return playerView as ICrossingView; } }

        [Header("Grouping")]
        [Tooltip("How many planks each correct answer drops, in question order.\n" +
                 "3,2,2,2,2 lays an 11-plank deck over 5 questions -- a bigger first group so the\n" +
                 "child sees real ground gained on the very first answer.\n\n" +
                 "Ideally this sums to plankSlots.Length, but it does not have to: the LAST entry\n" +
                 "always takes whatever planks are left, so the span still finishes closed on the\n" +
                 "final answer even if the deck art or the question count changes later. Leave the\n" +
                 "array empty for the old behaviour of one plank per answer.")]
                [SerializeField] private int[] plankGroupSizes = new int[0];

        [Tooltip("Seconds between planks within one group, so a group reads as a quick run of\n" +
                 "planks landing rather than one thud. 0 drops the whole group together.")]
        [SerializeField] private float withinGroupDelay = 0.12f;

        [Header("Drop-in")]
        [SerializeField] private float dropDuration = 0.5f;
        [SerializeField] private float dropHeight = 320f;
        [SerializeField] private float dropRotation = 25f;

        [Header("Crossing")]
                [Tooltip("Full-span crossing time, near bank to far bank. When the character has already stepped part of the way across, only the remaining fraction of this is spent -- see scaleCrossingByRemainingDistance.")]
        [SerializeField] private float walkDuration = 2.2f;

        [Tooltip("Scales the crossing to the distance still to cover. With per-plank stepping on, the last leg is only the final plank to the far bank, and spending the whole walkDuration on it would be a crawl. Off, the crossing always takes walkDuration whatever the start point.")]
        [SerializeField] private bool scaleCrossingByRemainingDistance = true;

        [Tooltip("Floor for the scaled crossing, so the last hop is never instant.")]
        [SerializeField] private float minCrossingDuration = 0.45f;

        [Header("Stepping")]
        [Tooltip("Time for one step onto a freshly placed plank. Fixed per step by default, so every plank reads the same however uneven the spacing is.")]
        [SerializeField] private float stepDuration = 0.8f;

        [Tooltip("On, stepDuration becomes the time for an AVERAGE step and each one is scaled by how far it actually is -- honest, but it makes the uneven plank spacing visible.")]
        [SerializeField] private bool scaleStepByDistance = false;

        [Tooltip("Nudges where the character stands relative to the plank's centre. X shifts along the span, Y off the walk line.")]
        [SerializeField] private Vector2 walkerPlankOffset = Vector2.zero;

        [Header("Hop")]
        [Tooltip("Arc height a single step rises through, in local canvas units. 0 keeps the old\n" +
                 "straight slide between planks -- only turn this on for a scene where the walker\n" +
                 "genuinely jumps from one foothold to the next (stone hopping) rather than walks\n" +
                 "or rows across a continuous span.")]
        [SerializeField] private float hopHeight = 0f;

        [Tooltip("Normalized progress (0-1) into the step at which the character actually leaves the\n" +
                 "ground. Before this the walker neither rises nor moves horizontally -- matches a rig\n" +
                 "with its own windup before liftoff (a crouch, a wind-back). 0 keeps the old behaviour\n" +
                 "of rising and travelling from the very start of the step.")]
        [SerializeField] [Range(0f, 1f)] private float hopLiftoffFraction = 0f;

        [Tooltip("Normalized progress (0-1) into the step at which the character's foot is back on the\n" +
                 "ground. After this the walker sits at its arrival position, neither rising nor moving --\n" +
                 "matches a rig with its own landing settle after impact. 1 keeps the old behaviour of\n" +
                 "landing exactly at the end of the step.")]
        [SerializeField] [Range(0f, 1f)] private float hopLandFraction = 1f;

        [Tooltip("On, the walker's height while stepping across the deck comes from each plank's own\n" +
                 "anchored Y (plus walkerPlankOffset.y) instead of being interpolated between WalkStart\n" +
                 "and WalkEnd. Turn this on for a flat, fanned deck where every plank sits at the same\n" +
                 "height -- interpolating from the bank heights there floats or sinks the character on\n" +
                 "the planks nearest each bank. Off (default) keeps the old bank-to-bank lerp, which is\n" +
                 "still correct for a deck that genuinely rises or falls from one bank to the other.")]
        [SerializeField] private bool useSlotHeightForWalk = false;

        [Header("Travel scale")]
        [Tooltip("How much the walker scales up and back down over the course of one travel move (a step onto the next plank, or the final crossing) -- e.g. 0.03 for a boat that swells very slightly as it glides, read as a touch of life/perspective rather than a rigid slide. Peaks halfway through the move and is back to normal exactly on arrival. 0 disables.")]
        [SerializeField] private float travelScalePulse = 0f;

        private Vector3 walkerRestScale = Vector3.one;
        private bool walkerRestScaleCaptured;

        private int placed;
        private int placedGroups;

        /// <summary>How many planks are down.</summary>
        public int Placed { get { return placed; } }

        /// <summary>How many the span needs.</summary>
        public int Total { get { return plankSlots != null ? plankSlots.Length : 0; } }

        /// <summary>How many answers' worth of planks are down.</summary>
        public int PlacedGroups { get { return placedGroups; } }

        /// <summary>
        /// How many answers it takes to close the span -- the number of configured
        /// groups, or one per plank when no grouping is set.
        /// </summary>
        public int GroupCount
        {
            get
            {
                if (plankSlots == null || plankSlots.Length == 0) return 0;
                if (plankGroupSizes == null || plankGroupSizes.Length == 0) return plankSlots.Length;
                return plankGroupSizes.Length;
            }
        }

        public bool IsComplete { get { return Total > 0 && placed >= Total; } }

        /// <summary>
        /// Empties the span, ready for a mission. <paramref name="plankSprite"/> lets
        /// each mission use its own plank art (a wooden plank, a stone, a log) without
        /// a separate prefab per mission.
        /// </summary>
        public void ResetBridge(Sprite plankSprite)
        {
            ResetBridge(plankSprite, null);
        }

        /// <summary>
        /// Empties the span, ready for a mission.
        ///
        /// <paramref name="perSlotSprites"/> lets a mission draw its planks in
        /// perspective -- one sprite per slot, left to right across the span. Any
        /// slot the array does not cover (or leaves null) falls back to
        /// <paramref name="plankSprite"/>, so a mission that only wants a single
        /// themed plank (a stone, a log) still works by passing that alone.
        ///
        /// If both are null the slot keeps whatever sprite the scene gave it -- the
        /// normal case for the fanned deck, where each slot's perspective art is wired
        /// up in the scene and no mission should be overriding it.
        /// </summary>
        public void ResetBridge(Sprite plankSprite, Sprite[] perSlotSprites)
        {
            placed = 0;
            placedGroups = 0;

            // A per-slot override that does not cover the whole deck is stale data --
            // a 5-entry mission array against an 11-plank span, say. Half-applying it
            // would repaint the first few planks and leave the rest mismatched, so it
            // is ignored outright and the scene's own per-slot art stands.
            bool usePerSlot = perSlotSprites != null && plankSlots != null
                              && perSlotSprites.Length == plankSlots.Length;

            if (plankSlots != null)
            {
                for (int i = 0; i < plankSlots.Length; i++)
                {
                    Image slot = plankSlots[i];
                    if (slot == null) continue;

                    slot.rectTransform.DOKill();

                    Sprite art = null;
                    if (usePerSlot) art = perSlotSprites[i];
                    if (art == null) art = plankSprite;
                    if (art != null) slot.sprite = art;

                    slot.gameObject.SetActive(false);
                }
            }

            if (walker != null && walkStart != null)
            {
                walker.DOKill();
                walker.anchoredPosition = walkStart.anchoredPosition;
                if (walkerRestScaleCaptured) walker.localScale = walkerRestScale;
            }

            // standing at the near bank while the questions are answered -- and facing
            // sideways again, in case the last run ended mid-celebration
            if (PlayerView != null)
            {
                PlayerView.StopCelebration();
                PlayerView.PlayIdle();
            }
        }

        /// <summary>
        /// Drops the next answer's worth of planks into the span -- one plank in the
        /// ungrouped case, otherwise the whole group. <paramref name="onLanded"/> fires
        /// once, after the LAST plank of the group has settled.
        ///
        /// Kept under the old name because this is what one correct answer buys, and
        /// every caller thinks in answers rather than planks.
        /// </summary>
        public void PlaceNextPlank(Action onLanded)
        {
            PlaceNextGroup(onLanded);
        }

        /// <summary>
        /// Drops the next group of planks. Runs on unscaled time because the caller may
        /// well have the world frozen behind a card.
        /// </summary>
        public void PlaceNextGroup(Action onLanded)
        {
            if (plankSlots == null || placed >= plankSlots.Length)
            {
                if (onLanded != null) onLanded();
                return;
            }

            int first = placed;
            int count = ResolveGroupSize();

            placed += count;
            placedGroups++;

            // one progress beat and one voice line per ANSWER, not per plank -- three
            // plank lines fired at once is just noise
            BridgeQuestGameManager.RaisePlankPlaced(placed, Total);

            BridgeQuestAudioMapper voice = BridgeQuestVoice.Mapper;
            if (voice != null) BridgeQuestVoice.Play(voice.GetRandomPlank());

            int pending = 0;
            for (int i = first; i < first + count; i++)
            {
                if (plankSlots[i] != null) pending++;
            }

            // nothing actually wired in this group -- do not strand the caller
            if (pending == 0)
            {
                AnnounceIfComplete(voice);
                if (onLanded != null) onLanded();
                return;
            }

            int landed = 0;
            for (int i = first; i < first + count; i++)
            {
                Image slot = plankSlots[i];
                if (slot == null) continue;

                DropSlot(slot, withinGroupDelay * (i - first), delegate
                {
                    landed++;
                    if (landed < pending) return;

                    AnnounceIfComplete(voice);
                    if (onLanded != null) onLanded();
                });
            }
        }

        /// <summary>
        /// How many planks this answer is worth.
        ///
        /// The last configured group always takes every plank still standing, so the
        /// span finishes closed on the final answer even when the group sizes and the
        /// deck length have drifted apart.
        /// </summary>
        private int ResolveGroupSize()
        {
            int remaining = plankSlots.Length - placed;

            if (plankGroupSizes == null || plankGroupSizes.Length == 0) return 1;
            if (placedGroups >= plankGroupSizes.Length - 1) return remaining;

            return Mathf.Clamp(plankGroupSizes[placedGroups], 1, remaining);
        }

        /// <summary>One plank falling in, with an optional stagger inside its group.</summary>
        private void DropSlot(Image slot, float delay, Action onLanded)
        {
            RectTransform rt = slot.rectTransform;

            rt.DOKill();
            Vector2 rest = rt.anchoredPosition;

            slot.gameObject.SetActive(true);
            rt.anchoredPosition = rest + new Vector2(0f, dropHeight);
            rt.localRotation = Quaternion.Euler(0f, 0f, dropRotation);

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            if (delay > 0f) seq.AppendInterval(delay);
            seq.Append(rt.DOAnchorPos(rest, dropDuration).SetEase(Ease.OutBounce));
            seq.Join(rt.DOLocalRotate(Vector3.zero, dropDuration).SetEase(Ease.OutBack));
            seq.OnComplete(delegate
            {
                rt.anchoredPosition = rest;
                rt.localRotation = Quaternion.identity;

                if (onLanded != null) onLanded();
            });
        }

        private void AnnounceIfComplete(BridgeQuestAudioMapper voice)
        {
            if (!IsComplete) return;

            BridgeQuestGameManager.RaiseBridgeComplete();
            if (voice != null) BridgeQuestVoice.Play(voice.BridgeComplete);
        }

        /// <summary>
        /// Walks the character onto the plank at <paramref name="slotIndex"/> and leaves
        /// them standing there. This is the per-question beat: answer, the planks land,
        /// the child sees the character actually gain ground, then the next question.
        ///
        /// Steps are NOT reset between calls -- each one starts from wherever the last
        /// ended, so the walk accumulates across the mission and PlayCrossing is left
        /// with only the final plank to the far bank.
        ///
        /// Unscaled, like everything else here: the caller usually has the world frozen.
        /// </summary>
        public void StepToPlank(int slotIndex, Action onArrived)
        {
            if (walker == null || plankSlots == null
                || slotIndex < 0 || slotIndex >= plankSlots.Length
                || plankSlots[slotIndex] == null)
            {
                if (onArrived != null) onArrived();
                return;
            }

            Vector2 target = WalkPositionForSlot(slotIndex);
            float distance = Vector2.Distance(walker.anchoredPosition, target);

            // already standing there -- do not play a walk cycle on the spot
            if (distance < 1f)
            {
                if (onArrived != null) onArrived();
                return;
            }

            float duration = stepDuration;
            if (scaleStepByDistance)
            {
                float average = AverageStepDistance();
                if (average > 0.01f) duration = stepDuration * (distance / average);
            }
            duration = Mathf.Max(0.05f, duration);

            walker.DOKill();
            if (PlayerView != null) PlayerView.PlayWalk();

            TweenWalkerTo(target, duration, delegate
            {
                if (PlayerView != null) PlayerView.PlayIdle();
                if (onArrived != null) onArrived();
            });
        }

        /// <summary>
        /// Moves the walker to <paramref name="target"/> over <paramref name="duration"/>.
        /// A plain linear slide when <see cref="hopHeight"/> is 0 (every existing scene);
        /// otherwise the same straight-line path with a sine arc added on top, so the
        /// walker visibly leaves the ground and lands again instead of gliding across --
        /// e.g. Goli hopping from stone to stone. Either way <paramref name="onArrived"/>
        /// fires once, with the walker snapped exactly onto target.
        /// </summary>
        private void TweenWalkerTo(Vector2 target, float duration, Action onArrived)
        {
            PulseWalkerScale(duration);

            if (hopHeight <= 0f)
            {
                walker.DOAnchorPos(target, duration)
                    .SetEase(Ease.Linear)
                    .SetUpdate(true)
                    .OnComplete(delegate
                    {
                        walker.anchoredPosition = target;
                        if (onArrived != null) onArrived();
                    });
                return;
            }

            Vector2 start = walker.anchoredPosition;

            DOVirtual.Float(0f, 1f, duration, delegate(float t)
            {
                // outside [hopLiftoffFraction, hopLandFraction] the walker sits still --
                // a windup before liftoff, a settle after landing -- and only travels
                // and arcs during the airborne window in between
                float u = hopLandFraction > hopLiftoffFraction
                    ? Mathf.Clamp01((t - hopLiftoffFraction) / (hopLandFraction - hopLiftoffFraction))
                    : t;

                Vector2 pos = Vector2.Lerp(start, target, u);
                pos.y += hopHeight * Mathf.Sin(u * Mathf.PI);
                walker.anchoredPosition = pos;
            })
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .SetTarget(walker)
            .OnComplete(delegate
            {
                walker.anchoredPosition = target;
                if (onArrived != null) onArrived();
            });
        }

        /// <summary>
        /// Grows the walker very slightly and back down over one travel move -- a
        /// separate tween on localScale, running alongside whatever is driving
        /// anchoredPosition, so it never fights the slide/hop. Peaks at the
        /// midpoint and lands back on the walker's own authored scale exactly when
        /// the move ends, whatever that scale is (captured once, lazily, so this
        /// works whether or not the walker was authored at localScale == 1).
        /// </summary>
        private void PulseWalkerScale(float duration)
        {
            if (walker == null || travelScalePulse <= 0f) return;

            if (!walkerRestScaleCaptured)
            {
                walkerRestScale = walker.localScale;
                walkerRestScaleCaptured = true;
            }

            walker.DOScale(walkerRestScale * (1f + travelScalePulse), duration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true);
        }

        /// <summary>
        /// Steps onto the last plank <see cref="PlaceNextGroup"/> has just dropped --
        /// the far end of the group, so one answer moves the character by the whole run
        /// of planks it just earned.
        /// </summary>
        public void StepToLastPlacedPlank(Action onArrived)
        {
            StepToPlank(placed - 1, onArrived);
        }

/// <summary>
        /// Where the character stands to be on slot <paramref name="slotIndex"/>.
        ///
        /// X comes from the plank, converted into the walker's own parent space rather
        /// than read straight off anchoredPosition -- the planks sit under their own
        /// container, and that container is free to move.
        ///
        /// Y depends on <see cref="useSlotHeightForWalk"/>. On, it comes from the SAME
        /// transformed point as X -- the plank's own height, plus walkerPlankOffset.y --
        /// so a flat deck (every plank at the same anchored Y, just fanned or sheared for
        /// perspective) keeps the character at a constant, correct height the whole
        /// crossing. Off, Y is interpolated between WalkStart and WalkEnd instead, which
        /// is the original behaviour and still right for a deck that actually rises or
        /// falls from one bank to the other.
        /// </summary>
        private Vector2 WalkPositionForSlot(int slotIndex)
        {
            RectTransform slot = plankSlots[slotIndex].rectTransform;
            RectTransform parent = walker.parent as RectTransform;

            Vector3 local = parent != null
                ? parent.InverseTransformPoint(slot.position)
                : (Vector3)slot.anchoredPosition;

            float x = local.x;
            float y;

            if (useSlotHeightForWalk)
            {
                y = local.y;
            }
            else
            {
                y = walker.anchoredPosition.y;
                if (walkStart != null && walkEnd != null)
                {
                    int last = plankSlots.Length - 1;
                    float t = last > 0 ? (float)slotIndex / last : 1f;
                    y = Mathf.Lerp(walkStart.anchoredPosition.y, walkEnd.anchoredPosition.y, t);
                }
            }

            return new Vector2(x, y) + walkerPlankOffset;
        }

        /// <summary>Mean gap between consecutive walk positions. Only scaleStepByDistance reads it.</summary>
        private float AverageStepDistance()
        {
            if (walkStart == null || walkEnd == null || plankSlots == null || plankSlots.Length == 0) return 0f;

            float span = Vector2.Distance(walkStart.anchoredPosition, walkEnd.anchoredPosition);
            return span / (plankSlots.Length + 1);
        }


        /// <summary>
        /// Walks the character across the finished span. The payoff the whole mission
        /// has been building to -- give it room before the closing storyboard starts.
        /// </summary>
        public void PlayCrossing(Action onArrived)
        {
            if (walker == null || walkEnd == null)
            {
                if (onArrived != null) onArrived();
                return;
            }

            walker.DOKill();

            Vector2 target = walkEnd.anchoredPosition;

            // already on the far bank -- nothing left to walk
            if (Vector2.Distance(walker.anchoredPosition, target) < 1f)
            {
                if (PlayerView != null) PlayerView.PlayIdle();
                if (onArrived != null) onArrived();
                return;
            }

            float duration = walkDuration;

            // With per-plank stepping the character is already standing on the last
            // plank, so this leg is a short hop onto the bank. Spend the same fraction
            // of walkDuration as the distance left is of the whole span, so the pace
            // matches the steps instead of crawling.
            if (scaleCrossingByRemainingDistance && walkStart != null)
            {
                float span = Vector2.Distance(walkStart.anchoredPosition, target);
                float remaining = Vector2.Distance(walker.anchoredPosition, target);

                if (span > 0.01f)
                {
                    duration = Mathf.Max(minCrossingDuration, walkDuration * (remaining / span));
                }
            }

            // feet move for exactly as long as the slot slides
            if (PlayerView != null) PlayerView.PlayWalk();

            TweenWalkerTo(target, duration, delegate
            {
                // arrived -- stand still again before the closing storyboard
                if (PlayerView != null) PlayerView.PlayIdle();

                if (onArrived != null) onArrived();
            });
        }

        /// <summary>True when the view has a celebration rig to swap in.</summary>
        public bool HasCelebration { get { return PlayerView != null && PlayerView.HasCelebration; } }

        /// <summary>
        /// The little dance at the far bank. Loops until something else moves the
        /// story on, so the caller decides how long it runs -- see
        /// BridgeQuestFlow.celebrationDuration.
        /// </summary>
        public void PlayCelebration()
        {
            if (PlayerView != null) PlayerView.PlayCelebration();
        }


        private void OnDestroy()
        {
            if (plankSlots != null)
            {
                for (int i = 0; i < plankSlots.Length; i++)
                {
                    if (plankSlots[i] != null) plankSlots[i].rectTransform.DOKill();
                }
            }
            if (walker != null) walker.DOKill();
        }
    }
}
