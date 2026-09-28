using UnityEngine;
using UnityEngine.UI;

namespace TMKOC.BridgeQuest
{
    /// <summary>
    /// A continuous travelling wave across a sprite -- water, a flag, anything that
    /// should ripple rather than sit dead flat. Slices the sprite into vertical
    /// columns (see OnPopulateMesh) and bobs each column up and down by a sine wave
    /// whose phase shifts with time, so the crest visibly travels sideways across the
    /// surface instead of the whole image just shifting position.
    ///
    /// The wave amplitude fades to zero at the left and right edges (see
    /// <see cref="edgeFadeFraction"/>), so the rect's own boundary never visibly
    /// reveals whatever sits behind it -- unlike a plain position sway, this is safe
    /// to run at a noticeable amplitude.
    ///
    /// Runs on unscaled time so it keeps rippling while Bridge Quest freezes the
    /// world behind a card, the same convention as every other ambient effect here.
    ///
    /// Replaces the RectTransform's Image -- this component draws the sprite itself,
    /// so remove the Image component when adding this one.
    /// </summary>
    public class WaveDeform : MaskableGraphic
    {
        [SerializeField] private Sprite sprite;

        [Tooltip("How many vertical columns the sprite is sliced into. More columns = smoother wave, at a small extra vertex cost.")]
        [SerializeField] [Range(4, 64)] private int columns = 24;

        [Tooltip("How many rows -- only 2 are needed for a flat sprite; more only matters if you also want vertical detail.")]
        [SerializeField] [Range(2, 8)] private int rows = 2;

        [Tooltip("Pixels the surface rises and falls at the peak of the wave.")]
        [SerializeField] private float amplitude = 10f;

        [Tooltip("How many full waves fit across the width of the sprite.")]
        [SerializeField] private float waveCount = 2.5f;

        [Tooltip("How fast the wave travels sideways. Higher = faster ripple.")]
        [SerializeField] private float speed = 0.6f;

        [Tooltip("Fraction of the width, at each edge, over which the wave fades out to zero so the rect's boundary never visibly moves.")]
        [SerializeField] [Range(0f, 0.5f)] private float edgeFadeFraction = 0.08f;

        public override Texture mainTexture
        {
            get { return sprite != null ? sprite.texture : s_WhiteTexture; }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        private void Update()
        {
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (sprite == null) return;

            Rect r = GetPixelAdjustedRect();
            Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);

            int cols = Mathf.Max(2, columns);
            int rws = Mathf.Max(1, rows);

            float time = Time.unscaledTime * speed;
            float width = Mathf.Max(0.001f, r.width);

            // build a grid of verts, (cols+1) x (rws+1)
            for (int row = 0; row <= rws; row++)
            {
                float fy = (float)row / rws;
                float y = Mathf.Lerp(r.yMin, r.yMax, fy);
                float v = Mathf.Lerp(uv.y, uv.w, fy);

                for (int col = 0; col <= cols; col++)
                {
                    float fx = (float)col / cols;
                    float x = Mathf.Lerp(r.xMin, r.xMax, fx);
                    float u = Mathf.Lerp(uv.x, uv.z, fx);

                    float edgeFade = 1f;
                    if (edgeFadeFraction > 0f)
                    {
                        float distFromEdge = Mathf.Min(fx, 1f - fx);
                        edgeFade = Mathf.Clamp01(distFromEdge / edgeFadeFraction);
                        edgeFade = edgeFade * edgeFade * (3f - 2f * edgeFade); // smoothstep
                    }

                    float phase = fx * waveCount * Mathf.PI * 2f + time * Mathf.PI * 2f;
                    float yOffset = Mathf.Sin(phase) * amplitude * edgeFade;

                    UIVertex vert = UIVertex.simpleVert;
                    vert.position = new Vector3(x, y + yOffset, 0f);
                    vert.uv0 = new Vector2(u, v);
                    vert.color = color;
                    vh.AddVert(vert);
                }
            }

            int rowStride = cols + 1;
            for (int row = 0; row < rws; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int i0 = row * rowStride + col;
                    int i1 = i0 + 1;
                    int i2 = i0 + rowStride;
                    int i3 = i2 + 1;

                    vh.AddTriangle(i0, i2, i1);
                    vh.AddTriangle(i2, i3, i1);
                }
            }
        }
    }
}
