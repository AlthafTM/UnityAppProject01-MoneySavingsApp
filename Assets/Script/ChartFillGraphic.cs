using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class ChartFillGraphic : MaskableGraphic
{
    [Header("Fill")]
    public Color fillColor = new Color(0.08f, 0.16f, 0.2f, 0.6f);
    [Tooltip("Pixels to pull the top of the filled area down so the line sits cleanly above it.")]
    public float fillTopInset = 2f;

    [Header("Grid (optional)")]
    public bool drawGrid = false;
    public int horizontalGridLines = 3;
    public Color gridColor = new Color(1f,1f,1f,0.04f);
    public float gridThickness = 1f;

    [Header("Data")]
    public List<float> values = new List<float>();

    private float minValue = 0f;
    private float maxValue = 1f;

    public void Refresh() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;

        if (drawGrid && horizontalGridLines > 0)
            DrawGrid(vh, rect);

        if (values == null || values.Count == 0) return;

        // compute min/max
        minValue = float.MaxValue; maxValue = float.MinValue;
        for (int i = 0; i < values.Count; i++)
        {
            float vi = values[i];
            if (vi < minValue) minValue = vi;
            if (vi > maxValue) maxValue = vi;
        }
        if (minValue > maxValue) { minValue = 0; maxValue = 1; }
        if (Mathf.Approximately(maxValue, minValue)) maxValue = minValue + 1f;

        int n = values.Count;

        // compute the top points (lowered by fillTopInset)
        Vector2[] topPts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float t = (n == 1) ? 0.5f : (float)i / (n - 1);
            float x = rect.xMin + t * rect.width;
            float norm = Mathf.Clamp01((values[i] - minValue) / (maxValue - minValue));
            float y = rect.yMin + norm * rect.height;
            y = Mathf.Max(rect.yMin, y - fillTopInset); // lowered top to avoid covering the line
            topPts[i] = new Vector2(x, y);
        }

        UIVertex vert = UIVertex.simpleVert;
        vert.color = fillColor;

        // If only one point, create a single triangle from left-bottom -> top -> right-bottom
        if (n == 1)
        {
            int baseIndex = vh.currentVertCount;
            Vector2 bl = new Vector2(rect.xMin, rect.yMin);
            Vector2 br = new Vector2(rect.xMax, rect.yMin);

            vert.position = bl; vh.AddVert(vert);
            vert.position = topPts[0]; vh.AddVert(vert);
            vert.position = br; vh.AddVert(vert);

            vh.AddTriangle(baseIndex + 0, baseIndex + 1, baseIndex + 2);
            return;
        }

        // Build a quad for each adjacent pair: (bottom_i, top_i, top_i+1, bottom_i+1).
        // This avoids fan triangulation and thus prevents long crossing triangles.
        for (int i = 0; i < n - 1; i++)
        {
            // bottom vertices use the x coordinates of the top points to make vertical slice
            Vector2 blSeg = new Vector2(topPts[i].x, rect.yMin);
            Vector2 tl = topPts[i];
            Vector2 tr = topPts[i + 1];
            Vector2 brSeg = new Vector2(topPts[i + 1].x, rect.yMin);

            int baseIndex = vh.currentVertCount;

            vert.position = blSeg; vh.AddVert(vert);  // 0
            vert.position = tl;    vh.AddVert(vert);  // 1
            vert.position = tr;    vh.AddVert(vert);  // 2
            vert.position = brSeg; vh.AddVert(vert);  // 3

            // two triangles per quad: (0,1,2) and (2,3,0)
            vh.AddTriangle(baseIndex + 0, baseIndex + 1, baseIndex + 2);
            vh.AddTriangle(baseIndex + 2, baseIndex + 3, baseIndex + 0);
        }
    }

    void DrawGrid(VertexHelper vh, Rect rect)
    {
        for (int i = 0; i <= horizontalGridLines; i++)
        {
            float t = (float)i / horizontalGridLines;
            float y = Mathf.Lerp(rect.yMin, rect.yMax, t);
            AddLineSegment(vh, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y), gridThickness, gridColor);
        }
    }

    // simple line quad for grid (reused)
    void AddLineSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color col)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        UIVertex vert = UIVertex.simpleVert;
        vert.color = col;

        int idx = vh.currentVertCount;

        vert.position = a + perp; vh.AddVert(vert);
        vert.position = a - perp; vh.AddVert(vert);
        vert.position = b + perp; vh.AddVert(vert);
        vert.position = b - perp; vh.AddVert(vert);

        vh.AddTriangle(idx + 0, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 1, idx + 3);
    }
}
