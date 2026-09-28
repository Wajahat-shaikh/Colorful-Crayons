using System;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Whatever plays a Bridge Quest storyboard for BridgeQuestFlow -- the scrapbook
    /// collage (BridgeStoryCutsceneUI) by default, or a StorySlideshow adapter for a
    /// scene that wants slides shown one at a time instead. BridgeQuestFlow only
    /// ever calls Play and waits for the callback, so any implementation is a drop-in
    /// swap with no change to the mission flow itself.
    /// </summary>
    public interface IStoryboardPlayer
    {
        void Play(StoryPanel[] panels, Action onComplete);
    }
}
