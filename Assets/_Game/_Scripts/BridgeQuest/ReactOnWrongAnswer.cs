using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Plays a rig's one-shot reaction animation (see
    /// BridgeQuestPlayerView.PlayReaction) whenever a wrong answer is tapped -- e.g.
    /// Goli stumbling. Subscribes to BridgeQuestGameManager.OnWrongAnswer itself, so
    /// nothing else needs to call it.
    /// </summary>
    public class ReactOnWrongAnswer : MonoBehaviour
    {
        [SerializeField] private BridgeQuestPlayerView playerView;

        private void OnEnable()
        {
            BridgeQuestGameManager.OnWrongAnswer += HandleWrongAnswer;
        }

        private void OnDisable()
        {
            BridgeQuestGameManager.OnWrongAnswer -= HandleWrongAnswer;
        }

        private void HandleWrongAnswer(QuestionType type)
        {
            if (playerView != null) playerView.PlayReaction();
        }
    }
}
