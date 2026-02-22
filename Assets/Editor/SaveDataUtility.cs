using UnityEngine;
using UnityEditor;

public static class SaveDataUtility
{
    [MenuItem("Tools/Save Data/Clear All PlayerPrefs")]
    public static void ClearAllPlayerPrefs()
    {
        if (EditorUtility.DisplayDialog("Clear All PlayerPrefs?",
            "This will delete ALL saved values for your game (balance, items, etc).\n\nAre you sure?",
            "Yes, delete everything", "Cancel"))
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            // Reset your save flag too
            ScrollviewInput[] inputs = Object.FindObjectsOfType<ScrollviewInput>();
            foreach (var input in inputs)
            {
                var so = new SerializedObject(input);
                so.FindProperty("loadedFromSave").boolValue = false; // force reset
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(input);
            }

            Debug.Log("✅ Cleared all PlayerPrefs and reset loadedFromSave.");
        }
    }
}
