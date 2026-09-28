using System.Collections;
using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Adapts Bridge Quest's shared RuntimeAudioLoader AudioSource to
    /// IVoiceLinePlayer, so a generic StorySlideshow can wait for a Bridge
    /// Quest voice line to finish (and stop it on skip) without knowing anything
    /// about RuntimeAudioLoader. Also exposes PlayVoice(string) to wire up
    /// StorySlideshow's onPlayVoiceOver event -- it needs an actual object+method
    /// target to serialize as a persistent Inspector listener, which a static method
    /// like BridgeQuestVoice.Play cannot provide.
    ///
    /// RuntimeAudioLoader downloads/extracts/decodes its category bundle over
    /// several coroutine frames, so the very first line of a scene can be asked for
    /// before its clip exists yet -- BridgeQuestVoice's usual contract is to fail
    /// quiet in that case, which is right for one-off lines but would silently drop
    /// slide 1's narration on a fresh scene load. So PlayVoice holds IsPlaying true
    /// and waits for the clip to finish loading (bounded by maxLoadWait) instead of
    /// giving up, so the slideshow does not advance past a line that just hasn't
    /// arrived yet.
    /// </summary>
    public class BridgeQuestVoiceLinePlayer : MonoBehaviour, IVoiceLinePlayer
    {
        [Tooltip("Longest this waits for a voice-over clip to finish downloading/decoding before giving up on that line.")]
        [SerializeField] private float maxLoadWait = 8f;

        private bool pending;
        private Coroutine pendingRoutine;

        public bool IsPlaying
        {
            get
            {
                if (pending) return true;

                RuntimeAudioLoader loader = RuntimeAudioLoader.Instance;
                return loader != null && loader._commonAudioSource != null && loader._commonAudioSource.isPlaying;
            }
        }

        /// <summary>Wire this to StorySlideshow's onPlayVoiceOver event.</summary>
        public void PlayVoice(string key)
        {
            CancelPending();

            if (string.IsNullOrEmpty(key)) return;

            RuntimeAudioLoader loader = RuntimeAudioLoader.Instance;
            if (loader == null) return;

            if (loader.GetClip(key) != null)
            {
                BridgeQuestVoice.Play(key);
                return;
            }

            pending = true;
            pendingRoutine = StartCoroutine(WaitForClipThenPlay(key));
        }

        private IEnumerator WaitForClipThenPlay(string key)
        {
            RuntimeAudioLoader loader = RuntimeAudioLoader.Instance;
            float waited = 0f;

            // GetClip logs a warning on every miss, so this polls a few times a
            // second rather than every frame -- still responsive, far less spam
            // while the bundle is still downloading/decoding.
            WaitForSecondsRealtime poll = new WaitForSecondsRealtime(0.15f);
            while (loader.GetClip(key) == null && waited < maxLoadWait)
            {
                waited += 0.15f;
                yield return poll;
            }

            pending = false;
            pendingRoutine = null;

            if (loader.GetClip(key) != null) BridgeQuestVoice.Play(key);
        }

        public void StopVoice()
        {
            CancelPending();

            RuntimeAudioLoader loader = RuntimeAudioLoader.Instance;
            if (loader != null && loader._commonAudioSource != null) loader._commonAudioSource.Stop();
        }

        private void CancelPending()
        {
            if (pendingRoutine != null)
            {
                StopCoroutine(pendingRoutine);
                pendingRoutine = null;
            }
            pending = false;
        }
    }
}
