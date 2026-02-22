using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller that converts SavedItem data -> chart values and updates both
/// ChartFillGraphic and ChartLineGraphic. Includes a simulate/debug mode,
/// grouping/summing per-day (uses item.date + item.day as grouping key),
/// and a coroutine to wait for layout passes before forcing redraw.
/// </summary>
[DisallowMultipleComponent]
public class ChartController : MonoBehaviour
{
    [Header("Chart graphics (assign both)")]
    public ChartFillGraphic chartFillGraphic;
    public ChartLineGraphic chartLineGraphic;

    private Coroutine refreshCoroutine;

    [Header("Data source (optional)")]
    [Tooltip("You can fill this list from code or wire it with data from your save system.")]
    public List<SavedItem> items = new List<SavedItem>();

    [Header("Simulate / debug")]
    public bool simulate = true;
    public int simulateDays = 10;
    public int simulateMinPerDay = 5;
    public int simulateMaxPerDay = 40;
    public Button regenerateButton; // optional: connect a UI button to regenerate

    [Header("Chart scaling")]
    [Tooltip("When true, the chart baseline is considered zero (spending >= 0).")]
    public bool forceZeroBaseline = true;

    [Tooltip("If true, log debug information about sizes/values at ApplyToChart")]
    public bool debugLog = false;

    void Awake()
    {
        if (regenerateButton != null)
            regenerateButton.onClick.AddListener(GenerateAndShowSimulatedData);
    }

    void Start()
    {
        if (simulate)
            GenerateAndShowSimulatedData();
        else
            RefreshFromItems();
    }

    void OnDestroy()
    {
        if (regenerateButton != null)
            regenerateButton.onClick.RemoveListener(GenerateAndShowSimulatedData);

        if (refreshCoroutine != null)
            StopCoroutine(refreshCoroutine);
    }

    /// <summary>
    /// Build daily totals from current items and put into chart.
    /// Uses the order of first appearance when grouping by date+day.
    /// Only counts spending items (isIncome == false).
    /// </summary>
    public void RefreshFromItems()
    {
        var orderedKeys = new List<string>();
        var sums = new Dictionary<string, long>();

        foreach (var it in items)
        {
            if (it == null) continue;
            if (it.isIncome) continue; // only spending

            string key = (it.date ?? "") + "|" + (it.day ?? "");
            if (!sums.ContainsKey(key))
            {
                sums[key] = 0;
                orderedKeys.Add(key);
            }
            sums[key] += it.value;
        }

        // now build totals with fixed length = simulateDays (10 by default)
        var totals = new List<float>(simulateDays);

        int count = Mathf.Min(orderedKeys.Count, simulateDays);
        for (int i = 0; i < count; i++)
            totals.Add((float)sums[orderedKeys[i]]);

        // pad the rest with zeros
        while (totals.Count < simulateDays)
            totals.Add(0f);

        ApplyToChart(totals);
    }

    /// <summary>
    /// Generate random simulated totals (useful for debugging).
    /// </summary>
    public void GenerateAndShowSimulatedData()
    {
        var totals = new List<float>(simulateDays);
        System.Random r = new System.Random();

        for (int i = 0; i < simulateDays; i++)
        {
            int v = r.Next(simulateMinPerDay, simulateMaxPerDay + 1);
            totals.Add((float)v);
        }

        ApplyToChart(totals);
    }

    /// <summary>
    /// Sets totals to both graphics and queues a safe refresh after layout is ready.
    /// </summary>
    void ApplyToChart(List<float> totals)
    {
        if (chartFillGraphic == null && chartLineGraphic == null) return;

        // supply totals to both graphics (if assigned)
        if (chartFillGraphic != null)
            chartFillGraphic.values = totals;
        if (chartLineGraphic != null)
            chartLineGraphic.values = totals;

        if (debugLog)
        {
            Rect r1 = chartFillGraphic != null ? (chartFillGraphic.rectTransform.rect) : new Rect();
            Rect r2 = chartLineGraphic != null ? (chartLineGraphic.rectTransform.rect) : new Rect();
            Debug.Log($"ApplyToChart: totals={totals?.Count ?? 0}, fillRect={r1.width}x{r1.height}, lineRect={r2.width}x{r2.height}, activeFill={(chartFillGraphic!=null?chartFillGraphic.gameObject.activeInHierarchy:false)}, activeLine={(chartLineGraphic!=null?chartLineGraphic.gameObject.activeInHierarchy:false)}");
        }

        // restart coroutine to refresh after layout has settled
        if (refreshCoroutine != null) StopCoroutine(refreshCoroutine);
        refreshCoroutine = StartCoroutine(RefreshChartNextFrame());
    }

    /// <summary>
    /// Coroutine that waits for layout passes, forces layout rebuilds and then tells each graphic to redraw.
    /// This prevents the "invisible at start" problem when RectTransform sizes are not yet computed.
    /// </summary>
    private IEnumerator RefreshChartNextFrame()
    {
        // Wait one frame (let layout/content size fitter do work)
        yield return null;

        // Force Unity to update canvas/layouts immediately
        Canvas.ForceUpdateCanvases();

        if (chartFillGraphic != null)
        {
            var rt = chartFillGraphic.rectTransform as RectTransform;
            if (rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
        if (chartLineGraphic != null)
        {
            var rt = chartLineGraphic.rectTransform as RectTransform;
            if (rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        // Now force the maskable graphics to rebuild their meshes
        if (chartFillGraphic != null)
        {
            chartFillGraphic.Refresh();
            chartFillGraphic.SetVerticesDirty();
            chartFillGraphic.SetAllDirty();
        }

        if (chartLineGraphic != null)
        {
            chartLineGraphic.Refresh();
            chartLineGraphic.SetVerticesDirty();
            chartLineGraphic.SetAllDirty();
        }

        // Extra safe frame (optional) so the UI has another chance to stabilize
        yield return null;

        refreshCoroutine = null;
    }

    /// <summary>
    /// Add a new SavedItem and refresh the chart.
    /// </summary>
    public void AddSavedItem(SavedItem s)
    {
        if (s == null) return;
        items.Add(s);
        RefreshFromItems();
    }

    /// <summary>
    /// Replace the controller's items with a saved list and refresh.
    /// Use this after loading from PlayerPrefs.
    /// </summary>
    public void SetFromSavedList(List<SavedItem> savedItems)
    {
        items = savedItems != null ? new List<SavedItem>(savedItems) : new List<SavedItem>();
        RefreshFromItems();
    }
}
