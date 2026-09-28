using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// A handful of leaves drifting down from a tree's canopy, on loop, forever.
    /// Built with plain DOTween (matching every other ambient effect in this game --
    /// see AlphaUtility, SwayingFoliage) rather than a full particle system, since
    /// this is a small fixed pool of sprites, not hundreds of short-lived ones.
    ///
    /// Each leaf spawns at a random X within spawnWidth (centred on this object's own
    /// anchored position), drifts down by fallDistance with a gentle side-to-side sway
    /// and a slow spin, fades in and out at the ends of the fall, then loops back to
    /// a fresh random start. Leaves are staggered on their own random delay so they
    /// never fall in lockstep.
    /// </summary>
    public class FallingLeaves : MonoBehaviour
    {
        [SerializeField] private Sprite leafSprite;

        [Tooltip("How many leaves drift at once. Kept small on purpose -- this is meant to read as a light breeze, not a leaf storm.")]
        [SerializeField] private int leafCount = 3;

        [Tooltip("Leaves spawn at a random X within +/- half this, centred on this object.")]
        [SerializeField] private float spawnWidth = 200f;

        [Tooltip("Height above this object's own pivot the leaves start falling from -- set to roughly the canopy's height when this sits on a tree/bush whose own pivot is at its base.")]
        [SerializeField] private float startHeight = 260f;

        [SerializeField] private float fallDistance = 260f;
        [SerializeField] private float minFallDuration = 4f;
        [SerializeField] private float maxFallDuration = 6f;
        [SerializeField] private float minSize = 12f;
        [SerializeField] private float maxSize = 20f;
        [SerializeField] private float swayAmount = 30f;
        [SerializeField] private float spinDegrees = 90f;

        private RectTransform[] leaves;

        private void OnEnable()
        {
            if (leafSprite == null) return;

            leaves = new RectTransform[leafCount];
            for (int i = 0; i < leafCount; i++)
            {
                leaves[i] = CreateLeaf();
                StartFall(leaves[i], Random.Range(0f, maxFallDuration));
            }
        }

        private void OnDisable()
        {
            if (leaves == null) return;

            foreach (RectTransform leaf in leaves)
            {
                if (leaf == null) continue;
                leaf.DOKill();
            }
            leaves = null;
        }

        private RectTransform CreateLeaf()
        {
            GameObject go = new GameObject("Leaf", typeof(RectTransform), typeof(Image));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);

            Image img = go.GetComponent<Image>();
            img.sprite = leafSprite;
            img.raycastTarget = false;
            img.preserveAspect = true;

            float size = Random.Range(minSize, maxSize);
            rt.sizeDelta = new Vector2(size, size);

            return rt;
        }

        /// <summary>One drift cycle for one leaf, then calls itself again for the next.</summary>
        private void StartFall(RectTransform leaf, float initialDelay)
        {
            if (leaf == null) return;

            float startX = Random.Range(-spawnWidth * 0.5f, spawnWidth * 0.5f);
            float duration = Random.Range(minFallDuration, maxFallDuration);
            float fadeTime = Mathf.Min(0.6f, duration * 0.2f);

            leaf.anchoredPosition = new Vector2(startX, startHeight);
            leaf.localRotation = Quaternion.identity;

            Image img = leaf.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.AppendInterval(initialDelay);
            seq.AppendCallback(delegate
            {
                if (leaf == null) return;
                leaf.anchoredPosition = new Vector2(startX, startHeight);
            });

            seq.Append(img.DOFade(0.9f, fadeTime).SetUpdate(true));
            seq.Join(leaf
                .DOAnchorPosY(startHeight - fallDistance, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true));
            seq.Join(leaf
                .DOAnchorPosX(startX + Random.Range(-swayAmount, swayAmount), duration)
                .SetEase(Ease.InOutSine)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true));
            seq.Join(leaf
                .DORotate(new Vector3(0f, 0f, Random.value < 0.5f ? spinDegrees : -spinDegrees), duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true));
            seq.Insert(initialDelay + duration - fadeTime, img.DOFade(0f, fadeTime).SetUpdate(true));

            seq.OnComplete(delegate { StartFall(leaf, 0f); });
        }
    }
}
