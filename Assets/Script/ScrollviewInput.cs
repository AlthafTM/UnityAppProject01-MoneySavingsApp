using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Globalization;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class SavedItem
{
    public long value;
    public string day;
    public string date;
    public bool isIncome; 
}

[Serializable]
public class SavedItemList
{
    public List<SavedItem> items = new List<SavedItem>();
    public long balance;
    public long maxBalanceReached;
    public long dayTotalSpent;
    public string dayTotalDate;

}


[DisallowMultipleComponent]
public class ScrollviewInput : MonoBehaviour
{
    private const string SaveKey = "ScrollviewInput_Save";

    [Header("References")]
    [Tooltip("Content RectTransform inside the ScrollView (usually ScrollView/Viewport/Content)")]
    public RectTransform content;

    [Tooltip("Optional: assign the ScrollRect that contains the content. If left null we try to auto-find one.")]
    public ScrollRect scrollRect;

    [Tooltip("Prefab to instantiate as a child of Content")]
    public GameObject prefab;

    [Header("Behavior")]
    [Tooltip("When true, new items are inserted at the top (sibling index 0). When false, they append at the bottom).")]
    public bool insertAtTop = true;

    [Header("Balance Input + Button")]
    [Tooltip("TextMeshPro InputField that accepts only integers for adding balance.")]
    public TMP_InputField balanceInputField;

    [Tooltip("Optional UI Button that will call AddBalanceFromInput when clicked.")]
    public Button addBalanceButton;


    [Header("References")]
    public ChartController chartController;




    [Header("Input + Button")]
    [Tooltip("TextMeshPro InputField that accepts only integers (we force it in Start)")]
    public TMP_InputField numericInputField;

    [Tooltip("Optional UI Button that will call AddFromInput when clicked. You can also wire it manually in the Inspector.")]
    public Button addButton;

    [Header("Targeting")]
    [Tooltip("If set, when a prefab instance is created we will look for a child object with this name (recursive) and set the main TMP_Text on it. If empty, we fall back to the first TMP_Text found.")]
    public string targetTextName = "";

    [Tooltip("Alternative to targetTextName: you can provide a path relative to the prefab root (e.g. \"Root/AmountText\"). It's checked before targetTextName.")]
    public string targetTextPath = "";

    [Tooltip("Name (or path) for the Day TMP inside the prefab. If path is set it is checked first.")]
    public string targetDayName = "";
    public string targetDayPath = "";

    [Tooltip("Name (or path) for the Date TMP inside the prefab. If path is set it is checked first.")]
    public string targetDateName = "";
    public string targetDatePath = "";

    [Header("Balance")]
    [Tooltip("TextMeshPro that displays the current balance (e.g. 'Rp 1.000.000')")]
    public TMP_Text balanceText;

    [Tooltip("Optional UI Image used as a fill bar (Image.type = Filled). Fill amount reflects currentBalance/startingBalance.")]
    public Image balanceFillImage;

    [Tooltip("Starting balance (in whole Rp units). Set this in the Inspector.")]
    public long startingBalance = 0;

    [Header("Day Total")]
    [Tooltip("Displays the total expenses (only '-') spent today.")]
    public TMP_Text dayTotalText;

    private long todaySpent = 0;
    private string todayDateKey = ""; // to track if day changes


    [Header("Settings")]
    [Min(0)] public int amount = 10;
    public bool populateOnStart = true;
    public bool clearBeforePopulate = true;

    [Header("Overshoot / Clamp")]
    [Tooltip("When true, clamps ScrollRect normalized position to [0,1] each frame to prevent overshoot")]
    public bool clampOvershoot = true;

    [Tooltip("If true and a ScrollRect is found, set its Movement Type to Clamped automatically.")]
    public bool forceClampedMovementType = false;

    // keep track of created instances (optional)
    private List<GameObject> instances = new List<GameObject>();

    // Indonesian culture for formatting (thousands separator = '.')
    private CultureInfo idCulture = new CultureInfo("id-ID");

    // Indonesian day and month names
    private static readonly string[] indonesianDays = new string[] { "Minggu", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu" };
    private static readonly string[] indonesianMonths = new string[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Des" };

    private Coroutine fillTweenCoroutine;
    private float fillTweenDuration = 0.5f; // adjust speed of animation

    private Coroutine balanceTweenCoroutine;
    [SerializeField] private float balanceTweenDuration = 1.0f; // configurable in Inspector
    private long displayedBalance; // the number shown on screen
    public bool loadedFromSave = false;

    // runtime balance
    private long currentBalance = 0;
    private long maxBalanceReached = 0;


    public List<SavedItem> items = new List<SavedItem>();
    public long balance;

    
    void OnApplicationQuit()
    {
        SaveData();
    }

    void Awake()
    {
        LoadData();
    }

    void Start()
    {
        if (!loadedFromSave)
        {
            // Only set startingBalance if no save was loaded
            currentBalance = Math.Max(0, startingBalance);
            displayedBalance = currentBalance;
            UpdateBalanceUI();
        }

        if (balanceInputField != null)
        {
            balanceInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            balanceInputField.onValueChanged.AddListener(OnBalanceInputValueChanged);

            if (addBalanceButton != null)
            {
                addBalanceButton.onClick.AddListener(AddBalanceFromInput);
            }
        }
        // capture any existing children as "instances" so SyncAmount works even if content already has children
        CacheExistingChildren();

        // try to auto-find a ScrollRect if none assigned (look in this GameObject or parents)
        if (scrollRect == null)
        {
            scrollRect = GetComponent<ScrollRect>();
            if (scrollRect == null && transform.parent != null)
                scrollRect = GetComponentInParent<ScrollRect>();
        }

        if (scrollRect != null && forceClampedMovementType)
        {
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        // Setup numeric input field to accept integers only
        if (numericInputField != null)
        {
            // Force integer-only content type
            numericInputField.contentType = TMP_InputField.ContentType.IntegerNumber;

            // Keep the text clean (strip non-digits) while typing
            numericInputField.onValueChanged.AddListener(OnInputValueChanged);

            // Optionally wire addButton automatically
            if (addButton != null)
            {
                addButton.onClick.AddListener(AddFromInput);
            }
        }

        todayDateKey = DateTime.Now.ToString("yyyy-MM-dd"); 
        UpdateDayTotalUI();

        if (populateOnStart) Populate();
    }

    void OnDestroy()
    {
        if (numericInputField != null)
            numericInputField.onValueChanged.RemoveListener(OnInputValueChanged);

        if (addButton != null)
            addButton.onClick.RemoveListener(AddFromInput);
        
        if (balanceInputField != null)
            balanceInputField.onValueChanged.RemoveListener(OnInputValueChanged);

        if (addBalanceButton != null)
            addBalanceButton.onClick.RemoveListener(AddBalanceFromInput);

    }


    void OnInputValueChanged(string raw)
    {
        if (numericInputField == null) return;

        // Always enforce prefix while typing (but allow empty)
        if (string.IsNullOrEmpty(raw))
        {
            numericInputField.text = "";
            return;
        }
        raw = EnsureRpPrefix(raw);

        string cleaned = CleanNumericString(raw.Replace("Rp ", ""));
        if (string.IsNullOrEmpty(cleaned))
        {
            numericInputField.text = "";
            return;
        }

        if (long.TryParse(cleaned, out long number))
        {
            string formatted = "Rp " + number.ToString("N0", idCulture);
            if (numericInputField.text != formatted)
            {
                numericInputField.text = formatted;
                numericInputField.stringPosition = numericInputField.text.Length;
                numericInputField.caretPosition = numericInputField.text.Length;
            }
        }
    }


    string CleanNumericString(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("Rp", "").Trim();
        System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsDigit(s[i])) sb.Append(s[i]);
        }
        return sb.ToString();
    }

    public void AddFromInput()
    {
        string currentDay = DateTime.Now.ToString("yyyy-MM-dd");
        if (currentDay != todayDateKey)
        {
            todayDateKey = currentDay;
            todaySpent = 0;
        }

        if (numericInputField == null)
        {
            Debug.LogWarning("[ScrollviewInput] numericInputField is not assigned.");
            return;
        }

        string raw = CleanNumericString(numericInputField.text);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[ScrollviewInput] Input is empty or contains no digits.");
            return;
        }

        if (!long.TryParse(raw, out long value))
        {
            Debug.LogWarning("[ScrollviewInput] Failed to parse input to a number.");
            return;
        }

        var newItem = new SavedItem {
            value = value,
            day = GetIndonesianDay(DateTime.Now),
            date = GetIndonesianMonthDay(DateTime.Now),
            isIncome = false
        };

        // save into your local items list
        items.Add(newItem);

        // send to chart
        if (chartController != null)
            chartController.AddSavedItem(newItem);


        // decrement balance (clamp to zero)
        long newTargetBalance = Math.Max(0, currentBalance - value);
        StartBalanceTween(newTargetBalance);

        currentBalance = newTargetBalance; // logical balance always correct

        AddItemWithValue(value);

        todaySpent += value;
        UpdateDayTotalUI();

        // Clear the input field after adding (per your request)
        numericInputField.text = "";
    }

    void AddItemWithValue(long value)
    {
        if (content == null || prefab == null)
        {
            Debug.LogWarning("[ScrollviewInput] content or prefab is not assigned.");
            return;
        }

        var go = Instantiate(prefab);
        go.transform.SetParent(content, false);

        // Ensure local scale/transform is sane for UI
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.anchoredPosition = Vector2.zero;
        }
        else
        {
            go.transform.localScale = Vector3.one;
        }

        // If you want newest items at the top, set sibling index 0
        if (insertAtTop)
            go.transform.SetSiblingIndex(0); // <-- makes the new item appear above older ones

        // Try to find the TMP_Text using the configured path/name, then fallback to first TMP_Text
        TMP_Text tmp = GetTargetTextFromInstance(go, targetTextPath, targetTextName);
        if (tmp != null)
        {
            tmp.text = "<color=#C0392B>-</color> " + FormatRp(value);
        }
        else
        {
            Debug.LogWarning("[ScrollviewInput] Prefab does not contain a TextMeshPro (TMP_Text) component in the requested child or children for main amount.");
        }

        // Day
        TMP_Text dayTmp = GetTargetTextFromInstance(go, targetDayPath, targetDayName);
        if (dayTmp != null)
        {
            dayTmp.text = GetIndonesianDay(DateTime.Now);
        }

        // Date (e.g. "Jan 23")
        TMP_Text dateTmp = GetTargetTextFromInstance(go, targetDatePath, targetDateName);
        if (dateTmp != null)
        {
            dateTmp.text = GetIndonesianMonthDay(DateTime.Now);
        }

        items.Add(new SavedItem {
            value = value,
            day = GetIndonesianDay(DateTime.Now),
            date = GetIndonesianMonthDay(DateTime.Now),
            isIncome = false
        });

        go.SetActive(true);
        instances.Add(go);

        ForceRebuild();

        // Optionally make scroll go to top so the new item is visible
        if (insertAtTop && scrollRect != null)
        {
            // verticalNormalizedPosition = 1 => top in Unity UI
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    void UpdateBalanceUI()
    {
        if (balanceText != null)
        {
            balanceText.text = FormatRp(currentBalance);
        }

        if (balanceFillImage != null)
        {
            maxBalanceReached = Math.Max(maxBalanceReached, currentBalance);
            float fill = maxBalanceReached > 0 ? (float)currentBalance / maxBalanceReached : 0f;
            TweenBalanceFill(Mathf.Clamp01(fill));
        }
    }



    TMP_Text GetTargetTextFromInstance(GameObject instance, string path, string name)
    {
        if (instance == null) return null;

        // 1) If a path is provided, try Transform.Find which supports hierarchical paths
        if (!string.IsNullOrEmpty(path))
        {
            var found = instance.transform.Find(path);
            if (found != null)
            {
                var t = found.GetComponent<TMP_Text>();
                if (t != null) return t;
            }
        }

        // 2) If a name is provided, search recursively for a child with that name
        if (!string.IsNullOrEmpty(name))
        {
            var found = FindDeepChildByName(instance.transform, name);
            if (found != null)
            {
                var t = found.GetComponent<TMP_Text>();
                if (t != null) return t;
            }
        }

        // 3) fallback: first TMP_Text in prefab (behaviour prior to change)
        return instance.GetComponentInChildren<TMP_Text>(true);
    }

    // Recursive search for a child by name
    Transform FindDeepChildByName(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name) return child;
            var deeper = FindDeepChildByName(child, name);
            if (deeper != null) return deeper;
        }
        return null;
    }

    string FormatRp(long value)
    {
        // Uses Indonesian culture so thousands separator becomes '.' and no decimals
        return "Rp " + value.ToString("N0", idCulture);
    }

    string GetIndonesianDay(DateTime dt)
    {
        // DayOfWeek in .NET: Sunday = 0 ... Saturday = 6
        return indonesianDays[(int)dt.DayOfWeek];
    }

    string GetIndonesianMonthDay(DateTime dt)
    {
        int monthIndex = dt.Month - 1; // zero-based for array
        string mon = indonesianMonths[Mathf.Clamp(monthIndex, 0, indonesianMonths.Length - 1)];
        return mon + " " + dt.Day.ToString();
    }

    void CacheExistingChildren()
    {
        instances.Clear();
        if (content == null) return;
        for (int i = 0; i < content.childCount; i++)
        {
            instances.Add(content.GetChild(i).gameObject);
        }
    }


    [ContextMenu("Populate")]
    public void Populate()
    {
        if (content == null || prefab == null)
        {
            Debug.LogWarning("[ScrollViewPopulator] content or prefab is not assigned.");
            return;
        }

        if (clearBeforePopulate)
            Clear();

        for (int i = 0; i < amount; i++)
    {
        var go = Instantiate(prefab);
        go.transform.SetParent(content, false);

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.anchoredPosition = Vector2.zero;
        }
        else
        {
            go.transform.localScale = Vector3.one;
        }

        if (insertAtTop)
            go.transform.SetSiblingIndex(0);

        var tmp = GetTargetTextFromInstance(go, targetTextPath, targetTextName);
        if (tmp != null)
        {
            tmp.text = FormatRp(0);
        }

        // assign day/date placeholders too
        var dayTmp = GetTargetTextFromInstance(go, targetDayPath, targetDayName);
        if (dayTmp != null) dayTmp.text = GetIndonesianDay(DateTime.Now);

        var dateTmp = GetTargetTextFromInstance(go, targetDatePath, targetDateName);
        if (dateTmp != null) dateTmp.text = GetIndonesianMonthDay(DateTime.Now);

        go.SetActive(true);
        instances.Add(go);
    }

        ForceRebuild();
    }

    /// <summary>
    /// Adjusts the current number of children to match 'amount' without recreating everything.
    /// Adds new instances or removes extras as needed.
    /// </summary>
    [ContextMenu("SyncAmount")]
    public void SyncAmount()
    {
        if (content == null || prefab == null)
        {
            Debug.LogWarning("[ScrollViewPopulator] content or prefab is not assigned.");
            return;
        }

        // ensure instances list matches current content children
        if (instances.Count == 0 && content.childCount > 0)
            CacheExistingChildren();

        int current = instances.Count;

        if (current < amount)
        {
            int toAdd = amount - current;
            for (int i = 0; i < toAdd; i++)
            {
                var go = Instantiate(prefab);
                go.transform.SetParent(content, false);

                var rt = go.GetComponent<RectTransform>();
                if (rt != null) rt.localScale = Vector3.one;
                else go.transform.localScale = Vector3.one;

                go.SetActive(true);
                instances.Add(go);
            }
        }
        else if (current > amount)
        {
            int toRemove = current - amount;
            for (int i = 0; i < toRemove; i++)
            {
                // remove last
                var last = instances[instances.Count - 1];
                instances.RemoveAt(instances.Count - 1);
                if (last != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(last);
                    else
                        Destroy(last);
#else
                    Destroy(last);
#endif
                }
            }
        }

        ForceRebuild();
    }

    /// <summary>
    /// Clear all instantiated children in content.
    /// </summary>
    [ContextMenu("Clear")]
    public void Clear()
    {
        if (content == null) return;
        // destroy children we've tracked; if there are any leftover children, remove them too
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            var go = instances[i];
            instances.RemoveAt(i);
            if (go != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(go);
                else
                    Destroy(go);
#else
                Destroy(go);
#endif
            }
        }

        // Also destroy any children that weren't in instances list (safety)
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var c = content.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(c);
            else
                Destroy(c);
#else
            Destroy(c);
#endif
        }

        instances.Clear();
        ForceRebuild();
    }

    void ForceRebuild()
    {
        if (content == null) return;

        // Ensure Canvas & Layout systems update immediately so ScrollRect gets correct bounds
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    // LateUpdate runs after ScrollRect update so clamping here won't fight internal updates.
    void LateUpdate()
    {
        if (!clampOvershoot || scrollRect == null || content == null) return;

        // Only clamp normalized positions on axes that the ScrollRect actually uses.
        if (scrollRect.vertical && scrollRect.content != null)
        {
            // Clamp to [0,1] to prevent overshoot (inertia or elastic movement going out of bounds)
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        }

        if (scrollRect.horizontal && scrollRect.content != null)
        {
            scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(scrollRect.horizontalNormalizedPosition);
        }
    }

    private void TweenBalanceFill(float targetFill)
    {
        if (balanceFillImage == null) return;

        // Stop any running tween to avoid overlap
        if (fillTweenCoroutine != null)
            StopCoroutine(fillTweenCoroutine);

        fillTweenCoroutine = StartCoroutine(TweenFillCoroutine(targetFill));
    }

    private IEnumerator TweenFillCoroutine(float target)
    {
        float start = balanceFillImage.fillAmount;
        float time = 0f;

        while (time < fillTweenDuration)
        {
            time += Time.deltaTime;
            float t = time / fillTweenDuration;

            // Ease in-out quadratic
            t = t * t * (3f - 2f * t);

            balanceFillImage.fillAmount = Mathf.Lerp(start, target, t);
            yield return null;
        }

        balanceFillImage.fillAmount = target;
        fillTweenCoroutine = null;
    }


    private void StartBalanceTween(long targetBalance)
    {
        // Track highest balance ever reached
        maxBalanceReached = Math.Max(maxBalanceReached, targetBalance);

        if (balanceTweenCoroutine != null)
            StopCoroutine(balanceTweenCoroutine);

        balanceTweenCoroutine = StartCoroutine(BalanceTweenCoroutine(targetBalance));
    }


    private IEnumerator BalanceTweenCoroutine(long targetBalance)
    {
        long startValue = displayedBalance == 0 ? currentBalance : displayedBalance;
        long endValue = targetBalance;

        // If both start and end are < 1000 → skip tween, snap instantly
        if (startValue < 1000 && endValue < 1000)
        {
            displayedBalance = endValue;
            if (balanceText != null) balanceText.text = FormatRp(endValue);

            if (balanceFillImage != null && startingBalance > 0)
                balanceFillImage.fillAmount = Mathf.Clamp01((float)endValue / startingBalance);

            balanceTweenCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        maxBalanceReached = Math.Max(maxBalanceReached, endValue);

        while (elapsed < balanceTweenDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / balanceTweenDuration;

            // Ease-out quadratic
            t = 1f - Mathf.Pow(1f - t, 2f);

            // Animate only thousands and above
            long startThousands = startValue / 1000;
            long endThousands = endValue / 1000;
            long currentThousands = (long)Mathf.Lerp(startThousands, endThousands, t);

            // Hundreds & below snap to target
            long finalRemainder = endValue % 1000;

            long current = (currentThousands * 1000) + finalRemainder;
            displayedBalance = current;

            // Update UI
            if (balanceText != null)
                balanceText.text = FormatRp(current);

            if (balanceFillImage != null)
            {
                float fill = maxBalanceReached > 0 ? (float)current / maxBalanceReached : 0f;
                balanceFillImage.fillAmount = Mathf.Clamp01(fill);
            }


            yield return null;
        }

        // Snap to final target
        displayedBalance = endValue;
        if (balanceText != null)
            balanceText.text = FormatRp(endValue);

        if (balanceFillImage != null)
        {
            balanceFillImage.fillAmount = maxBalanceReached > 0 ? (float)endValue / maxBalanceReached : 0f;
        }

        balanceTweenCoroutine = null;
    }


    public void SaveData()
    {
        SavedItemList data = new SavedItemList();
        data.balance = currentBalance;
        data.maxBalanceReached = maxBalanceReached;

        // If we have a maintained items list (preferred), use that directly.
        if (items != null && items.Count > 0)
        {
            // Make a copy so serialization is clean
            data.items = new List<SavedItem>(items);
        }
        else
        {
            // Fallback: parse visible instances (keeps backward compatibility)
            foreach (var go in instances)
            {
                if (go == null) continue;

                TMP_Text tmp = GetTargetTextFromInstance(go, targetTextPath, targetTextName);
                TMP_Text dayTmp = GetTargetTextFromInstance(go, targetDayPath, targetDayName);
                TMP_Text dateTmp = GetTargetTextFromInstance(go, targetDatePath, targetDateName);

                if (tmp != null)
                {
                    SavedItem item = new SavedItem();
                    item.value = ParseValueFromRp(tmp.text);
                    item.day = dayTmp != null ? dayTmp.text : "";
                    item.date = dateTmp != null ? dateTmp.text : "";

                    // Detect income by checking plain text (after stripping tags)
                    string plain = System.Text.RegularExpressions.Regex.Replace(tmp.text, "<.*?>", "");
                    item.isIncome = plain.Contains("+");

                    data.items.Add(item);
                }
            }
        }

        data.dayTotalSpent = todaySpent;
        data.dayTotalDate = todayDateKey;


        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
        Debug.Log("Saved: " + json);
    }

    private long ParseValueFromRp(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        // Remove any rich-text tags like <color=...>...</color>
        string withoutTags = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "");

        // Remove "Rp" and other non-digit characters, keep only digits
        System.Text.StringBuilder sb = new System.Text.StringBuilder(withoutTags.Length);
        for (int i = 0; i < withoutTags.Length; i++)
        {
            if (char.IsDigit(withoutTags[i])) sb.Append(withoutTags[i]);
        }

        if (sb.Length == 0) return 0;

        if (long.TryParse(sb.ToString(), out long value))
            return value;

        return 0;
    }


    public void LoadData()
    {
        if (!PlayerPrefs.HasKey(SaveKey)) return;

        string json = PlayerPrefs.GetString(SaveKey);
        SavedItemList data = JsonUtility.FromJson<SavedItemList>(json);
        items = data.items != null ? new List<SavedItem>(data.items) : new List<SavedItem>();


        Clear();

        currentBalance = data.balance;
        displayedBalance = currentBalance;
        maxBalanceReached = Math.Max(currentBalance, data.maxBalanceReached);

        if (balanceText != null)
            balanceText.text = FormatRp(currentBalance);

        if (balanceFillImage != null)
        {
            balanceFillImage.fillAmount = maxBalanceReached > 0 
                ? (float)currentBalance / maxBalanceReached 
                : 0f;
        }

        foreach (var item in data.items)
        {
            var go = Instantiate(prefab, content, false);
            if (insertAtTop) go.transform.SetSiblingIndex(0);

            TMP_Text tmp = GetTargetTextFromInstance(go, targetTextPath, targetTextName);
            if (tmp != null)
            {
                if (tmp != null)
                {
                    if (item.isIncome)
                        tmp.text = "<color=#4CAE81>+</color> " + FormatRp(item.value);
                    else
                        tmp.text = "<color=#C0392B>-</color> " + FormatRp(item.value);
                }

            }

            TMP_Text dayTmp = GetTargetTextFromInstance(go, targetDayPath, targetDayName);
            if (dayTmp != null) dayTmp.text = item.day;

            TMP_Text dateTmp = GetTargetTextFromInstance(go, targetDatePath, targetDateName);
            if (dateTmp != null) dateTmp.text = item.date;

            go.SetActive(true);
            instances.Add(go);
        }

        if (data.dayTotalDate == DateTime.Now.ToString("yyyy-MM-dd"))
        {
            todaySpent = data.dayTotalSpent;
        }
        else
        {
            todaySpent = 0; // new day, reset
            todayDateKey = DateTime.Now.ToString("yyyy-MM-dd");
        }
        UpdateDayTotalUI();

        // ALSO send them to chart
        if (chartController != null)
            chartController.SetFromSavedList(data.items);

        ForceRebuild();
        Debug.Log("Loaded: " + json);

        loadedFromSave = true;
    }


    public void AddBalanceFromInput()
    {
        if (balanceInputField == null)
        {
            Debug.LogWarning("[ScrollviewInput] balanceInputField is not assigned.");
            return;
        }

        string raw = CleanNumericString(balanceInputField.text);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[ScrollviewInput] Balance input is empty or contains no digits.");
            return;
        }

        if (!long.TryParse(raw, out long value))
        {
            Debug.LogWarning("[ScrollviewInput] Failed to parse balance input to a number.");
            return;
        }

        // increment balance
        long newTargetBalance = currentBalance + value;
        StartBalanceTween(newTargetBalance);

        currentBalance = newTargetBalance; // logical balance always correct

        AddBalanceItemWithValue(value);

        balanceInputField.text = "";
    }

    void AddBalanceItemWithValue(long value)
    {
        if (content == null || prefab == null)
        {
            Debug.LogWarning("[ScrollviewInput] content or prefab is not assigned.");
            return;
        }

        var go = Instantiate(prefab);
        go.transform.SetParent(content, false);

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.anchoredPosition = Vector2.zero;
        }
        else
        {
            go.transform.localScale = Vector3.one;
        }

        if (insertAtTop)
            go.transform.SetSiblingIndex(0);

        TMP_Text tmp = GetTargetTextFromInstance(go, targetTextPath, targetTextName);
        if (tmp != null)
        {
            tmp.text = "<color=#4CAE81>+</color> " + FormatRp(value);
        }

        TMP_Text dayTmp = GetTargetTextFromInstance(go, targetDayPath, targetDayName);
        if (dayTmp != null) dayTmp.text = GetIndonesianDay(DateTime.Now);

        TMP_Text dateTmp = GetTargetTextFromInstance(go, targetDatePath, targetDateName);
        if (dateTmp != null) dateTmp.text = GetIndonesianMonthDay(DateTime.Now);

        items.Add(new SavedItem {
            value = value,
            day = GetIndonesianDay(DateTime.Now),
            date = GetIndonesianMonthDay(DateTime.Now),
            isIncome = true
        });


        go.SetActive(true);
        instances.Add(go);

        ForceRebuild();

        if (insertAtTop && scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    void OnBalanceInputValueChanged(string raw)
    {
        if (balanceInputField == null) return;

        if (string.IsNullOrEmpty(raw))
        {
            balanceInputField.text = "";
            return;
        }
        raw = EnsureRpPrefix(raw);

        string cleaned = CleanNumericString(raw.Replace("Rp ", ""));
        if (string.IsNullOrEmpty(cleaned))
        {
            balanceInputField.text = "";
            return;
        }

        if (long.TryParse(cleaned, out long number))
        {
            string formatted = "Rp " + number.ToString("N0", idCulture);
            if (balanceInputField.text != formatted)
            {
                balanceInputField.text = formatted;
                balanceInputField.stringPosition = balanceInputField.text.Length;
                balanceInputField.caretPosition = balanceInputField.text.Length;
            }
        }
    }

    private string EnsureRpPrefix(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Rp ";
        if (!raw.StartsWith("Rp "))
            return "Rp " + raw.Replace("Rp", "").TrimStart();
        return raw;
    }

    private void UpdateDayTotalUI()
    {
        if (dayTotalText != null)
        {
            dayTotalText.text = "Total : " + FormatRp(todaySpent);
        }
    }


}
