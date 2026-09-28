using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// A tree or bush that bends in the wind instead of rotating as one rigid piece.
    ///
    /// Draws its own sprite mesh sliced into horizontal strips (see OnPopulateMesh)
    /// and pushes each strip sideways by an amount that grows with height above
    /// <see cref="bendStart"/> -- the trunk near the base barely moves, the canopy
    /// higher up sways more, the same way a real tree bends. A single rigid
    /// RectTransform rotation (see SwayingFoliage) cannot do this on one flat sprite;
    /// this replaces that rotation with a per-vertex offset instead.
    ///
    /// Replaces the RectTransform's Image -- this component draws the sprite itself,
    /// so remove the Image component when adding this one (both would otherwise draw
    /// the same art twice).
    ///
    /// Expects the RectTransform's pivot and rect to sit with y=0 at the base of the
    /// art, same convention as SwayingFoliage. That is the default, vertically-rooted
    /// case (<see cref="cutHorizontally"/> on) -- for something lying on its side
    /// instead (a horizontal branch, a banner rooted at one edge), switch it off:
    /// the slices run the other way and the bend runs across the width instead of
    /// the height, rooted at the rect's left edge (xMin) rather than its base (yMin).
    /// </summary>
    public class BendingFoliage : MaskableGraphic
    {
        [SerializeField] private Sprite sprite;

        [Tooltip("On (default): sliced into horizontal strips stacked bottom to top, each pushed sideways by height -- a tree/bush rooted at its base. Off: sliced into vertical strips side by side instead, each pushed up/down by horizontal distance from the left edge -- for something rooted at a side rather than a base.")]
        [SerializeField] private bool cutHorizontally = true;

        [Tooltip("How many strips the sprite is sliced into (rows if cutHorizontally, columns otherwise). More strips = smoother bend, at a small extra vertex cost. This is a small foreground decoration, so this can stay low.")]
        [SerializeField] [Range(2, 32)] private int segments = 12;

        [Tooltip("Normalized distance (0 = rooted edge, 1 = free end) above/across which bending starts. Below this the base/root stays rigid.")]
        [SerializeField] [Range(0f, 1f)] private float bendStart = 0.3f;

        [Tooltip("Sideways offset in pixels at the very top of the sprite, at the peak of the sway.")]
        [SerializeField] private float maxBendOffset = 18f;

        [SerializeField] private float swayDuration = 2.2f;

        [Tooltip("Seconds before this instance starts swaying -- staggering a tree and a bush a little keeps them from moving in lockstep.")]
        [SerializeField] private float startDelay = 0f;

        public override Texture mainTexture
        {
            get { return sprite != null ? sprite.texture : s_WhiteTexture; }
        }

        private float bendT; // -1..1, driven by the tween below
        private Tween tween;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            tween = DOTween
                .To(() => bendT, SetBend, 1f, swayDuration)
                .SetDelay(startDelay)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        protected override void OnDisable()
        {
            if (tween != null)
            {
                tween.Kill();
                tween = null;
            }

            bendT = 0f;
            base.OnDisable();
        }

        private void SetBend(float value)
        {
            bendT = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (sprite == null) return;

            Rect r = GetPixelAdjustedRect();
            Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);

            int strips = Mathf.Max(2, segments);
            float bendRange = Mathf.Max(0.001f, 1f - bendStart);

            for (int i = 0; i <= strips; i++)
            {
                float f = (float)i / strips; // 0 at the rooted edge, 1 at the free end

                float bendFactor = Mathf.Clamp01((f - bendStart) / bendRange);
                bendFactor *= bendFactor; // eases in just past the bend point instead of kinking
                float offset = bendT * maxBendOffset * bendFactor;

                if (cutHorizontally)
                {
                    // horizontal strips, stacked bottom to top -- base fixed, canopy
                    // pushed sideways (the original tree/bush case)
                    float y = Mathf.Lerp(r.yMin, r.yMax, f);
                    float v = Mathf.Lerp(uv.y, uv.w, f);

                    AddStripVert(vh, new Vector3(r.xMin + offset, y, 0f), new Vector2(uv.x, v));
                    AddStripVert(vh, new Vector3(r.xMax + offset, y, 0f), new Vector2(uv.z, v));
                }
                else
                {
                    // vertical strips, side by side from the left edge -- root fixed,
                    // free end pushed up/down instead of sideways
                    float x = Mathf.Lerp(r.xMin, r.xMax, f);
                    float u = Mathf.Lerp(uv.x, uv.z, f);

                    AddStripVert(vh, new Vector3(x, r.yMin + offset, 0f), new Vector2(u, uv.y));
                    AddStripVert(vh, new Vector3(x, r.yMax + offset, 0f), new Vector2(u, uv.w));
                }
            }

            for (int i = 0; i < strips; i++)
            {
                int i0 = i * 2;
                int i1 = i * 2 + 1;
                int i2 = i * 2 + 2;
                int i3 = i * 2 + 3;
                vh.AddTriangle(i0, i2, i1);
                vh.AddTriangle(i2, i3, i1);
            }
        }

        private void AddStripVert(VertexHelper vh, Vector3 position, Vector2 uv0)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = position;
            v.uv0 = uv0;
            v.color = color;
            vh.AddVert(v);
        }
    }
}
