using System;
using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Lets BridgeQuestFlow drive a StoryboardSequencer through the same
    /// IStoryboardPlayer hook it uses for BridgeStoryCutsceneUI, even though
    /// StoryboardSequencer works differently: its cards, narration and voice keys
    /// are hand-authored on the component itself (see the prefab), not built from
    /// StoryPanel data, and it starts itself from its own Start() rather than
    /// waiting to be told to play. So Play() ignores the panels argument.
    ///
    /// BridgeQuestFlow calls Play() twice through the same IStoryboardPlayer
    /// reference -- once for the opening storyboard, once for the closing one. The
    /// first call just waits on the opening sequence's own onStoryboardComplete
    /// (already running from Start()); the second routes to PlayEndingCard instead,
    /// since the opening sequence has long since finished and nothing would ever
    /// fire onStoryboardComplete a second time on its own.
    /// </summary>
    public class StoryboardSequencerBoard : MonoBehaviour, IStoryboardPlayer
    {
        [SerializeField] private StoryboardSequencer sequencer;

        private Action pendingComplete;
        private bool listening;
        private bool openingHandled;

        public void Play(StoryPanel[] panels, Action onComplete)
        {
            if (sequencer == null)
            {
                if (onComplete != null) onComplete();
                return;
            }

            if (openingHandled)
            {
                sequencer.PlayEndingCard(onComplete);
                return;
            }

            openingHandled = true;
            pendingComplete = onComplete;

            if (!listening)
            {
                listening = true;
                sequencer.onStoryboardComplete.AddListener(HandleComplete);
            }
        }

        private void HandleComplete()
        {
            Action cb = pendingComplete;
            pendingComplete = null;
            if (cb != null) cb();
        }

        private void OnDestroy()
        {
            if (sequencer != null && listening) sequencer.onStoryboardComplete.RemoveListener(HandleComplete);
        }
    }
}
