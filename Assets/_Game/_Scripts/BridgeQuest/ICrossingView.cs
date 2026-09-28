namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Whatever BridgeBuilderUI shows for the crossing character while planks are
    /// placed and stepped on -- an Animator-driven rig (BridgeQuestPlayerView) by
    /// default, or something built entirely from plain UI parts with no rig at all
    /// (e.g. ScooterCrossingView, for a character with no Animator/SpriteSkin rig,
    /// just wheels that spin and eyes that blink). BridgeBuilderUI only ever calls
    /// these, so either is a drop-in swap with no change to the crossing logic.
    /// </summary>
    public interface ICrossingView
    {
        void PlayIdle();
        void PlayWalk();
        void PlayCelebration();
        void StopCelebration();
        bool HasCelebration { get; }
    }
}
