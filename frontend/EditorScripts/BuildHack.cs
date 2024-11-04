using UnityEditor;

[InitializeOnLoad]
public static class BuildHack {
    [InitializeOnLoadMethod]
    static void UpdateMaximumRecursiveGenericDepth() {
        Debug.Log("[Build Patch] UpdateMaximumRecursiveGenericDepth");
        PlayerSettings.SetAdditionalIl2CppArgs("--maximum-recursive-generic-depth=50");
    }
}