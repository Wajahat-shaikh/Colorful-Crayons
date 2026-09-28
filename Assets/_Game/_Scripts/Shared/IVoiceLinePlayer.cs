namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Lets a generic UI flow (e.g. StorySlideshow) wait for a voice line to finish
    /// and stop it on skip, without knowing which audio system actually played it.
    /// Implement this on a thin adapter around whatever plays voice-over in a given
    /// game, and wire that adapter into the flow's voicePlayerSource slot.
    /// </summary>
    public interface IVoiceLinePlayer
    {
        bool IsPlaying { get; }
        void StopVoice();
    }
}
