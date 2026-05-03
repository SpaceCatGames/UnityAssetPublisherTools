using SCG.UnityAssetPublisherTools.Upm;
using UnityEditor;

namespace SCG.UnityAssetPublisherTools
{
    /// <summary>
    /// Automates switching a project folder into an embedded UPM package and back.
    /// The workflow stages files in the Temp folder first and only then moves them into Packages.
    /// While staged, the tool updates Packages/manifest.json to point to the final Packages location,
    /// which helps Package Manager resolve without requiring editor focus change.
    /// </summary>
    public static class UpmPackageBuilder
    {
        #region Menu Entry

#if !UPM_PACKAGE
        /// <summary>
        /// Starts the build flow that converts the configured project folder into an embedded UPM package.
        /// The operation first stages files in Temp and then moves them into Packages.
        /// The method updates manifest.json and forces Package Manager resolve to refresh the package list.
        /// </summary>
        [MenuItem(Constants.MenuRoot + "Build for UPM Package", priority = UpmConstants.MenuPriority)]
        public static void BuildOrReturn() => UpmSamplesWorkflow.PrepareSamplesAndSchedule(UpmBuildFlow.Build);
#else
        /// <summary>
        /// Starts the return flow that converts the embedded UPM package back into a project folder.
        /// The operation removes the dependency from manifest.json and restores files from Packages.
        /// The method also restores Samples~ visibility when it was toggled by this tool.
        /// </summary>
        [MenuItem(Constants.MenuRoot + "Return from UPM Package (to project)", priority = UpmConstants.MenuPriority)]
        public static void BuildOrReturn() => UpmSamplesWorkflow.PrepareSamplesAndSchedule(UpmBuildFlow.Return);
#endif

        #endregion

        #region Initialize

        /// <summary>
        /// Synchronizes the scripting define with the actual folder placement on editor load.
        /// The method also attempts to resume a pending staged operation after a domain reload.
        /// This keeps the menu label and compilation mode consistent across editor restarts.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            EditorApplication.delayCall += () =>
            {
                UpmBuildFlow.TryResumePendingWork();
                UpmDefineSynchronizer.SyncDefineWithPackagesFolder();
            };
        }

        #endregion
    }
}
