using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class ChartGraphic : MaskableGraphic
{
    [Header("Appearance")]
    public Color lineColor = new Color(0.6f, 0.8f, 1f);
    public Color fillColor = new Color(0.1f, 0.2f, 0.25f, 0.6f);
    public Color pointColor = Color.white;
    public float lineThickness = 4f;   // px
    public float pointSize = 12f;      // px
    [Range(0, 1)] public float cornerSmoothness = 0.2f; // not used heavily here

    [Header("Grid")]
    public bool drawGrid = false;
    public int horizontalGridLines = 3;
    public Color gridColor = new Color(1f,1f,1f,0.06f);
    public float gridThickness = 1f;

    [Tooltip("Pixels to pull the top of the filled area down so the line sits cleanly above it.")]
    public float fillTopInset = 2f;


    [Header("Data")]
    [Tooltip("X-ordered numeric values. Chart will use values.Count points spread evenly across width.")]
    public List<float> values = new List<float>();

    // internal
    private float minValue = 0f;
    private float maxValue = 1f;

    /// <summary>Call this after changing data to redraw</summary>
    public void Refresh()
    {
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        float width = rect.width;
        float height = rect.height;

        // draw grid first (so line sits on top)
        if (drawGrid && horizontalGridLines > 0)
            DrawGrid(vh, rect);

        if (values == null || values.Count == 0)
            return;

        // compute min/max
        minValue = float.MaxValue;
        maxValue = float.MinValue;
        for (int i = 0; i < values.Count; i++)
        {
            float v = values[i];
            if (v < minValue) minValue = v;
            if (v > maxValue) maxValue = v;
        }
        if (minValue > maxValue) { minValue = 0; maxValue = 1; }
        if (Mathf.Approximately(maxValue, minValue))
        {
            // avoid divide by zero => make a small range
            maxValue = minValue + 1f;
        }

        int n = values.Count;
        // precompute points in local rect coordinates (bottom-left is (0,0) relative to rect)
        Vector2[] pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float t = (n == 1) ? 0.5f : (float)i / (n - 1); // normalized X
            float x = rect.xMin + t * width;
            float norm = Mathf.Clamp01((values[i] - minValue) / (maxValue - minValue));
            float y = rect.yMin + norm * height;
            pts[i] = new Vector2(x, y);
        }

        // Fill polygon: bottom-left -> points left-to-right -> bottom-right
        DrawFilledArea(vh, pts, rect);

        // Draw thick polyline
        DrawPolyline(vh, pts, lineThickness, lineColor);

        // Draw point markers (circle-approximations)
        for (int i = 0; i < n; i++)
        {
            AddCircle(vh, pts[i], pointSize * 0.5f, 10, pointColor);
        }
    }

    void DrawGrid(VertexHelper vh, Rect rect)
    {
        // horizontal lines only
        for (int i = 0; i <= horizontalGridLines; i++)
        {
            float t = (float)i / horizontalGridLines;
            float y = Mathf.Lerp(rect.yMin, rect.yMax, t);
            AddLineSegment(vh, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y), gridThickness, gridColor);
        }
    }

    void DrawFilledArea(VertexHelper vh, Vector2[] pts, Rect rect)
    {
        int n = pts.Length;
        if (n == 0) return;

        // make a copy of top points and push them down by fillTopInset so the line is not covered by the fill
        Vector2[] topPts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float y = pts[i].y - fillTopInset;
            // ensure we don't go below the bottom of the rect
            y = Mathf.Max(y, rect.yMin);
            topPts[i] = new Vector2(pts[i].x, y);
        }

        Vector2 bl = new Vector2(rect.xMin, rect.yMin);
        Vector2 br = new Vector2(rect.xMax, rect.yMin);

        int baseIndex = vh.currentVertCount;
        UIVertex vert = UIVertex.simpleVert;
        vert.color = fillColor;

        // add bottom-left
        vert.position = bl;
        vh.AddVert(vert);

        // add all slightly-lowered top points
        for (int i = 0; i < n; i++)
        {
            vert.position = topPts[i];
            vh.AddVert(vert);
        }

        // add bottom-right
        vert.position = br;
        vh.AddVert(vert);

        // triangulate as fan from bottom-left: for i=0..n-1 triangle (0, i+1, i+2)
        for (int i = 0; i < n; i++)
        {
            vh.AddTriangle(baseIndex + 0, baseIndex + 1 + i, baseIndex + 1 + i + 1);
        }
    }


    void DrawPolyline(VertexHelper vh, Vector2[] pts, float thickness, Color col)
    {
        if (pts.Length < 2) return;

        // Draw each segment as a quad (approximate joins)
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[i + 1];
            AddLineSegment(vh, a, b, thickness, col);
        }

        // For nicer joints, add small circles at vertex positions using same color but slightly larger than point
        for (int i = 0; i < pts.Length; i++)
        {
            AddCircle(vh, pts[i], thickness * 0.5f + 1f, 12, col);
        }
    }

    // Add a rectangle quad for line segment between a and b
    void AddLineSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color col)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        UIVertex v = UIVertex.simpleVert;
        v.color = col;

        int idx = vh.currentVertCount;

        v.position = a + perp;
        vh.AddVert(v);
        v.position = a - perp;
        vh.AddVert(v);
        v.position = b + perp;
        vh.AddVert(v);
        v.position = b - perp;
        vh.AddVert(v);

        // two triangles: (0,1,2) and (2,1,3)
        vh.AddTriangle(idx + 0, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 1, idx + 3);
    }

    // Add a filled circle approx (fan) centered at 'center' radius 'r' with 'segments'
    void AddCircle(VertexHelper vh, Vector2 center, float radius, int segments, Color col)
    {
        if (segments < 4) segments = 4;
        int startIdx = vh.currentVertCount;

        UIVertex v = UIVertex.simpleVert;
        v.color = col;
        v.position = center;
        vh.AddVert(v); // center vertex

        float twoPi = Mathf.PI * 2f;
        for (int i = 0; i <= segments; i++)
        {
            float a = (i / (float)segments) * twoPi;
            Vector2 pos = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            v.position = pos;
            vh.AddVert(v);
        }

        // Triangles fan
        for (int i = 0; i < segments; i++)
        {
            vh.AddTriangle(startIdx + 0, startIdx + 1 + i, startIdx + 1 + i + 1);
        }
    }
}
