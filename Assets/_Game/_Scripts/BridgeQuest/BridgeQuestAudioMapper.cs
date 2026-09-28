using System.Collections;
using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Every voice-over key Bridge Quest asks RuntimeAudioLoader for. The strings
    /// here are the "voiceover_title" column of the Bridge Quest VO sheet, which is
    /// also the mp3 filename inside
    /// d2r38fn3ydtrfq.cloudfront.net/bridge_quest_game/{Language}.zip.
    ///
    /// Defaults are baked in so a fresh component works without hand-filling the
    /// inspector, but every field stays serialized so keys can be retuned without a
    /// code change -- same contract as RocketRunAudioMapper.
    /// </summary>
    public class BridgeQuestAudioMapper : AudioMapper
    {
        [Header("Audio bundle")]
        [Tooltip("CDN folder the clips live in. Matches the activity_name column of the VO sheet.")]
        public string CategoryName = "bridge_quest_game";

        [Header("Tutorial")]
        public string TutorialListen = "tut_listen";
        public string TutorialTap = "tut_tap";
        public string TutorialBridge = "tut_bridge";

        [Header("Answer feedback (one is picked at random)")]
        public string[] CorrectAnswer = { "answer_correct_1", "answer_correct_2", "answer_correct_3" };

        [Tooltip("Gentle 'try again' lines. Never a failure line -- a wrong tap costs nothing.")]
        public string[] WrongAnswer = { "answer_retry_1", "answer_retry_2", "answer_retry_3" };

        [Header("Bridge")]
        [Tooltip("Played as a plank lands.")]
        public string[] PlankPlaced = { "plank_1", "plank_2", "plank_3" };

        [Tooltip("Played when the fifth plank completes the span, before the crossing.")]
        public string BridgeComplete = "bridge_complete";

        [Header("Screens")]
        public string WinScreen = "win_screen";

        [Tooltip("Spoken on the retry screen. Never a failure line -- Bridge Quest is no-fail, so this is a 'let us build it again' invitation.")]
        public string TryAgainScreen = "tryagain_screen";

        [Header("Replay")]
        [Tooltip("Spoken when the child taps the replay button on a question card.")]
        public string RepeatQuestion = "repeat_question";

        public string GetRandomCorrect() { return Pick(CorrectAnswer); }
        public string GetRandomWrong() { return Pick(WrongAnswer); }
        public string GetRandomPlank() { return Pick(PlankPlaced); }

        private static string Pick(string[] pool)
        {
            if (pool == null || pool.Length == 0) return null;
            return pool[Random.Range(0, pool.Length)];
        }
    }

    /// <summary>
    /// Thin, fail-quiet wrapper around RuntimeAudioLoader -- lifted wholesale from
    /// RocketRunVoice, and for the same reason.
    ///
    /// The loader is a DontDestroyOnLoad singleton created by the Playschool shell,
    /// so it simply is not there when the Bridge Quest scene is played on its own,
    /// and PlayRuntimeAudio dereferences the clip for its length off-editor -- which
    /// throws when a key is missing from the bundle. Both cases should cost silence,
    /// never an exception in the middle of a question.
    /// </summary>
    public static class BridgeQuestVoice
    {
        public static void Play(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            RuntimeAudioLoader loader = RuntimeAudioLoader.Instance;
            if (loader == null || loader._commonAudioSource == null) return;
            if (loader.GetClip(key) == null) return;

            loader.PlayRuntimeAudio(key);
        }

        /// <summary>
        /// Same as <see cref="Play"/>, but if the clip isn't loaded yet (the category
        /// bundle is still downloading/decoding -- most likely right after a scene
        /// opens, before the first question's prompt), waits for it instead of
        /// dropping the line silently. Mirrors BridgeQuestVoiceLinePlayer's own
        /// load-wait handling for the storyboard, extended to any other one-off line
        /// that can land before the bundle is ready.
        ///
        /// Needs a MonoBehaviour to host the wait coroutine on -- BridgeQuestVoice
        /// itself is static, so it borrows the caller's (any live component works;
        /// RuntimeAudioLoader is never edited).
        /// </summary>
        public static void PlayWhenReady(MonoBehaviour host, string key, float maxWait = 25f)
        {
            if (string.IsNullOrEmpty(key) || host == null) return;

            RuntimeAudioLoader loader = RuntimeAudioLoader.Instance;
            if (loader == null || loader._commonAudioSource == null) return;

            if (loader.GetClip(key) != null)
            {
                loader.PlayRuntimeAudio(key);
                return;
            }

            host.StartCoroutine(WaitForClipThenPlay(loader, key, maxWait));
        }

        private static IEnumerator WaitForClipThenPlay(RuntimeAudioLoader loader, string key, float maxWait)
        {
            float waited = 0f;
            WaitForSecondsRealtime poll = new WaitForSecondsRealtime(0.15f);

            while (loader.GetClip(key) == null && waited < maxWait)
            {
                waited += 0.15f;
                yield return poll;
            }

            if (loader.GetClip(key) != null) loader.PlayRuntimeAudio(key);
        }

        /// <summary>
        /// Plays a line and reports how long it runs, so a caller can chain a second
        /// line behind it. RuntimeAudioLoader.PlayRuntimeAudio returns 0 in the editor
        /// (and dereferences a possibly-null clip off-editor), so the length is read
        /// off the clip directly here. Returns 0 when nothing was played.
        /// </summary>
        public static float PlayAndGetLength(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0f;

            RuntimeAudioLoader loader = RuntimeAudioLoader.Instance;
            if (loader == null || loader._commonAudioSource == null) return 0f;

            AudioClip clip = loader.GetClip(key);
            if (clip == null) return 0f;

            loader.PlayRuntimeAudio(key);
            return clip.length;
        }

        /// <summary>
        /// Speaks a tapped option. Alphabet and Number questions get this for free:
        /// the shared "common" bundle already carries every letter and numeral, so a
        /// tapped B says "B" without Bridge Quest shipping a single new clip. Anything
        /// else falls back to the option's own key.
        /// </summary>
        public static void SpeakOption(AnswerOption option, QuestionType type)
        {
            if (option == null) return;

            RuntimeAudioLoader loader = RuntimeAudioLoader.Instance;
            if (loader == null || loader._commonAudioSource == null) return;

            if (type == QuestionType.Alphabet && !string.IsNullOrEmpty(option.label))
            {
                if (loader.GetCommonAudioClip(option.label) != null)
                {
                    loader.PlayAlphabetClip(option.label);
                    return;
                }
            }

            if ((type == QuestionType.Number || type == QuestionType.Counting)
                && !string.IsNullOrEmpty(option.label))
            {
                int n;
                if (int.TryParse(option.label, out n)
                    && loader.GetCommonAudioClip(n.ToString() + ".0") != null)
                {
                    loader.PlayNumberClip(n);
                    return;
                }
            }

            Play(option.voiceKey);
        }

        /// <summary>Bridge Quest's key table, or null when no mapper is in the scene.</summary>
        public static BridgeQuestAudioMapper Mapper
        {
            get { return AudioMapper.Instance as BridgeQuestAudioMapper; }
        }

        /// <summary>
        /// Makes sure this game's voice-over bundle is in memory even when the scene
        /// is opened directly rather than through the Playschool menu.
        /// </summary>
        public static void EnsureLoaded()
        {
            BridgeQuestAudioMapper mapper = Mapper;
            if (mapper == null || RuntimeAudioLoader.Instance == null) return;

        
            RuntimeAudioLoader.Instance.EnsureCategoryLoaded(mapper.CategoryName);
        }
    }
}
