using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class ChartLineGraphic : MaskableGraphic
{
    [Header("Line & Points")]
    public Color lineColor = new Color(0.6f, 0.78f, 1f, 1f);
    public Color pointColor = Color.white;
    public float lineThickness = 4f;
    public float pointSize = 12f;
    public List<float> values = new List<float>();

    private float minValue = 0f;
    private float maxValue = 1f;

    public void Refresh() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;
        if (values == null || values.Count == 0) return;

        minValue = float.MaxValue; maxValue = float.MinValue;
        for (int i = 0; i < values.Count; i++)
        {
            float vv = values[i];
            if (vv < minValue) minValue = vv;
            if (vv > maxValue) maxValue = vv;
        }
        if (minValue > maxValue) { minValue = 0; maxValue = 1; }
        if (Mathf.Approximately(maxValue, minValue)) maxValue = minValue + 1f;

        int n = values.Count;
        Vector2[] pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float t = (n == 1) ? 0.5f : (float)i / (n - 1);
            float x = rect.xMin + t * rect.width;
            float norm = Mathf.Clamp01((values[i] - minValue) / (maxValue - minValue));
            float y = rect.yMin + norm * rect.height;
            pts[i] = new Vector2(x, y);
        }

        // draw polyline segments
        for (int i = 0; i < n - 1; i++)
            AddLineSegment(vh, pts[i], pts[i + 1], lineThickness, lineColor);

        // draw round-ish caps/joints as small filled circles
        for (int i = 0; i < n; i++)
            AddCircle(vh, pts[i], pointSize * 0.5f, 12, pointColor);
    }

    void AddLineSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color col)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        UIVertex v = UIVertex.simpleVert;
        v.color = col;

        int idx = vh.currentVertCount;

        v.position = a + perp; vh.AddVert(v);
        v.position = a - perp; vh.AddVert(v);
        v.position = b + perp; vh.AddVert(v);
        v.position = b - perp; vh.AddVert(v);

        vh.AddTriangle(idx + 0, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 1, idx + 3);
    }

    void AddCircle(VertexHelper vh, Vector2 center, float radius, int segments, Color col)
    {
        if (segments < 4) segments = 4;
        int start = vh.currentVertCount;

        UIVertex v = UIVertex.simpleVert;
        v.color = col;
        v.position = center; vh.AddVert(v);

        float twoPi = Mathf.PI * 2f;
        for (int i = 0; i <= segments; i++)
        {
            float a = (i / (float)segments) * twoPi;
            v.position = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            vh.AddVert(v);
        }

        for (int i = 0; i < segments; i++)
            vh.AddTriangle(start + 0, start + 1 + i, start + 1 + i + 1);
    }
}
