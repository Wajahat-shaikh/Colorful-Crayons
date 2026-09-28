using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Bridges a rowing rig's Animation Event to its paddle splash. Animation Events
    /// call a method on the same GameObject the Animator sits on, but the splash
    /// (a UI particle effect living under the Walker slot in the canvas) is a
    /// separate object entirely -- this just forwards the call.
    ///
    /// Wire the down-stroke frame in the rowing clip to call OnPaddleDown() here, so
    /// the splash fires exactly when the paddle actually enters the water instead of
    /// looping on its own timer regardless of what the animation is doing.
    ///
    /// The ripple around the hull fires from here too, on the same down-stroke frame
    /// as the splash -- water disturbed by the boat only when a row actually
    /// happens, not continuously while it drifts.
    /// </summary>
    public class RowingSplashTrigger : MonoBehaviour
    {
        [SerializeField] private WaterSplashParticle paddleSplash;
        [SerializeField] private BoatRippleEffect boatRipple;

        /// <summary>Animation Event target -- fired at the paddle's down-stroke frame.</summary>
        public void OnPaddleDown()
        {
            if (paddleSplash != null) paddleSplash.Play();
            if (boatRipple != null) boatRipple.Play();
        }
    }
}
