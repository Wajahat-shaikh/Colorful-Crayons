using DG.Tweening;
using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// A tiny shake whenever a wrong answer is tapped -- something reacting to the
    /// mistake without making a big deal of it. Subscribes to
    /// BridgeQuestGameManager.OnWrongAnswer itself, so nothing else needs to call it.
    ///
    /// Plain Transform, not RectTransform -- meant for a world-space rig (e.g.
    /// BhideOnBoat, filmed into a walker slot by its own RigCamera), not a UI
    /// element; QuestionCardUI already shakes the tapped option itself separately.
    ///
    /// Generic on purpose: nothing here knows about boats specifically, just "give
    /// this a tiny jolt on a wrong answer" -- usable on anything that should react.
    /// </summary>
    public class ShakeOnWrongAnswer : MonoBehaviour
    {
        [Tooltip("How far this shakes, in local units. Small -- this is a reaction, not the wrong-answer feedback itself.")]
        [SerializeField] private float shakeStrength = 0.15f;

        [SerializeField] private float shakeDuration = 0.3f;
        [SerializeField] private int vibrato = 12;
        [SerializeField] [Range(0f, 180f)] private float randomness = 90f;

        private Vector3 restPosition;

        private void Awake()
        {
            restPosition = transform.localPosition;
        }

        private void OnEnable()
        {
            BridgeQuestGameManager.OnWrongAnswer += HandleWrongAnswer;
        }

        private void OnDisable()
        {
            BridgeQuestGameManager.OnWrongAnswer -= HandleWrongAnswer;
            transform.DOKill();
            transform.localPosition = restPosition;
        }

        private void HandleWrongAnswer(QuestionType type)
        {
            transform.DOKill();
            transform.localPosition = restPosition;
            transform
                .DOShakePosition(shakeDuration, shakeStrength, vibrato, randomness, false, true)
                .SetUpdate(true)
                .OnComplete(delegate { transform.localPosition = restPosition; });
        }
    }
}
