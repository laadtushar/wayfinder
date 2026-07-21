using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One-shot Phase 0 configuration for Wayfinder (run via
/// Unity -batchmode -executeMethod Wayfinder.EditorTools.Phase0Setup.Configure).
/// Pins the platform settings CLAUDE.md mandates: Android target, Vulkan-only,
/// IL2CPP, ARM64. XR plug-in management / OpenXR feature toggles are left to the
/// editor UI because their settings assets are created by the XR packages on first
/// interactive load.
/// </summary>
namespace Wayfinder.EditorTools
{
    public static class Phase0Setup
    {
        public static void Configure()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.wayfinder.game");

            AssetDatabase.SaveAssets();
            Debug.Log("[Phase0Setup] Android target, Vulkan-only, IL2CPP, ARM64 configured.");
            EditorApplication.Exit(0);
        }
    }
}
