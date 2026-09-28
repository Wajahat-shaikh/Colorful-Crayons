using UnityEngine;
using UnityEngine.Events;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Fires an effect (confetti, a sting, anything) exactly when one specific
    /// voice-over key plays -- wired as an extra listener alongside the real voice
    /// player on something like StoryboardSequencer's onPlayVoiceOver, so a class as
    /// generic as that one can still land a celebration in step with a particular
    /// line (the ending card's "you made it" VO, say) without knowing confetti
    /// exists.
    ///
    /// Generic on purpose: nothing here knows about Bridge Quest, storyboards or
    /// confetti specifically -- it just watches for one key and fires an event.
    /// </summary>
    public class VoiceKeyEffectTrigger : MonoBehaviour
    {
        [Tooltip("Only this exact voice-over key fires onTriggered.")]
        [SerializeField] private string triggerKey;

        [SerializeField] private UnityEvent onTriggered;

        /// <summary>Wire this to the same onPlayVoiceOver event the real voice player listens to.</summary>
        public void OnVoiceLine(string key)
        {
            if (!string.IsNullOrEmpty(triggerKey) && triggerKey == key) onTriggered?.Invoke();
        }
    }
}
