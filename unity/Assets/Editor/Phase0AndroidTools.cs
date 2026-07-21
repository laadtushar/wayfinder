using UnityEditor;
using UnityEngine;

/// <summary>
/// Points Unity's Android external tools at the Android Studio SDK/NDK/JBR
/// already on this machine, since the standalone Android Support installer
/// ships no SDK/NDK/JDK. Runs headless (-executeMethod) or from the menu.
/// JBR is JDK 21 vs Unity's preferred 17 — if Gradle balks at build time,
/// install the OpenJDK module via Unity Hub instead and clear JdkPath.
/// </summary>
namespace Wayfinder.EditorTools
{
    public static class Phase0AndroidTools
    {
        const string SdkPath = @"C:\Users\tusha\AppData\Local\Android\Sdk";
        const string NdkPath = @"C:\Users\tusha\AppData\Local\Android\Sdk\ndk\27.1.12297006";
        const string JdkPath = @"C:\Program Files\Android\Android Studio\jbr";

        [MenuItem("Wayfinder/Configure Android SDK-NDK-JDK Paths")]
        public static void Configure()
        {
            EditorPrefs.SetBool("SdkUseEmbedded", false);
            EditorPrefs.SetBool("NdkUseEmbedded", false);
            EditorPrefs.SetBool("JdkUseEmbedded", false);
            EditorPrefs.SetString("AndroidSdkRoot", SdkPath);
            EditorPrefs.SetString("AndroidNdkRoot", NdkPath);
            EditorPrefs.SetString("JdkPath", JdkPath);
            Debug.Log("[Phase0AndroidTools] SDK/NDK/JDK paths set. Verify in Preferences > External Tools.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
