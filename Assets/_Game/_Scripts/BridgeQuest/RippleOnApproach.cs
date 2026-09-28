using UnityEngine;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// Fires a RippleEffect when a watched target (the walker stepping across the
    /// stones) comes within range, and re-arms once the target moves away again --
    /// so replaying a mission or stepping back onto the same stone ripples it again.
    ///
    /// Deliberately a plain distance watch rather than a hook into BridgeBuilderUI's
    /// stepping callbacks: this way it needs no changes to that shared class (used by
    /// every Bridge Quest scene) and works the same on any stone/pad in any scene
    /// that has a "walker" transform moving across it.
    /// </summary>
    public class RippleOnApproach : MonoBehaviour
    {
        [Tooltip("The transform to watch -- usually the walker. Left empty, this looks for a RectTransform named \"Walker\" in the scene once at Awake.")]
        [SerializeField] private RectTransform target;

        [Tooltip("The ripple this triggers when the target arrives.")]
        [SerializeField] private RippleEffect ripple;

        [Tooltip("Optional -- a dip this plays alongside the ripple, for something that should visibly give a little under the step. Left empty, only the ripple plays.")]
        [SerializeField] private ImpactDip dip;

        [Tooltip("How close the target has to get (in this object's local canvas units) to count as \"arrived\".")]
        [SerializeField] private float triggerDistance = 40f;

        [Tooltip("The target has to move back out past triggerDistance * this before the next arrival can fire again.")]
        [SerializeField] private float rearmMultiplier = 1.5f;

        private RectTransform rt;
        private Transform canvasTransform;
        private bool triggered;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();

            if (target == null)
            {
                GameObject found = GameObject.Find("Walker");
                if (found != null) target = found.GetComponent<RectTransform>();
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            canvasTransform = canvas != null ? canvas.rootCanvas.transform : null;
        }

        private void Update()
        {
            if (target == null || rt == null || ripple == null) return;

            // Comparing raw world-space positions breaks the moment the canvas isn't
            // at scale 1 (Screen Space canvases commonly sit around 0.01), since
            // triggerDistance is authored in pixel-scale canvas units, not world units.
            // Converting both points into the canvas's own local space undoes that
            // scale so the distance check means what it says regardless of canvas setup.
            float distance = canvasTransform != null
                ? Vector2.Distance(canvasTransform.InverseTransformPoint(rt.position), canvasTransform.InverseTransformPoint(target.position))
                : Vector2.Distance(rt.position, target.position);

            if (!triggered && distance <= triggerDistance)
            {
                triggered = true;
                ripple.Play();
                if (dip != null) dip.Play();
            }
            else if (triggered && distance > triggerDistance * rearmMultiplier)
            {
                triggered = false;
            }
        }
    }
}
