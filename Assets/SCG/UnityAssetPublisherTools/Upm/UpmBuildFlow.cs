using SCG.UnityAssetPublisherTools.Helpers;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace SCG.UnityAssetPublisherTools.Upm
{
    /// <summary>
    /// Implements the staged UPM build and return flows.
    /// The build flow moves the asset folder into Temp, updates manifest.json to point to Packages, then moves into Packages.
    /// The return flow removes the manifest dependency, resolves packages, then moves the folder back into Assets.
    /// All steps persist a small state so the flow can resume after a domain reload.
    /// </summary>
    internal static class UpmBuildFlow
    {
        #region Public API

        /// <summary>
        /// Starts the build flow for converting the configured project folder into an embedded package.
        /// The method performs file operations synchronously and then triggers Package Manager resolve.
        /// On completion, the UPM define symbol is enabled to recompile into package mode.
        /// </summary>
        public static void Build()
        {
            var cfg = AssetPublisherToolsSettings.Instance;

            if (!cfg.TrySyncImmediately())
            { 
                throw new InvalidOperationException("Unity editor is busy. Try again after compilation or import completes."); 
            }

            var folderName = UpmPathUtility.GetSafeFolderName(cfg.AssetRootFolder);

            var originalRootAbs = UpmPathUtility.ResolveOriginalRootAbs(cfg, folderName);
            if (string.IsNullOrEmpty(originalRootAbs) || !Directory.Exists(originalRootAbs))
                throw new InvalidOperationException($"Source folder does not exist: {originalRootAbs}");

            var tempRootAbs = Path.Combine(UpmPathUtility.ProjectRootAbs, UpmConstants.TempFolderName, folderName);
            var packagesRootAbs = Path.Combine(UpmPathUtility.ProjectRootAbs, UpmConstants.PackagesFolderName, folderName);

            if (Directory.Exists(packagesRootAbs))
                throw new InvalidOperationException($"Target folder already exists: {packagesRootAbs}");

            UpmFileOperations.EnsureCleanDirectory(tempRootAbs);
            UpmFileOperations.MoveFolderWithMeta(originalRootAbs, tempRootAbs);

            SamplesMetaBaker.Bake(tempRootAbs);

            var tempPackageJsonAbs = UpmPackageJsonStaging.EnsureEffectivePackageJson(cfg, originalRootAbs, tempRootAbs);
            SyncPackageJsonFromSettings(cfg, tempPackageJsonAbs);
            var packageId = PackageJsonUtility.GetPackageName(tempPackageJsonAbs);
            if (string.IsNullOrWhiteSpace(packageId))
                throw new InvalidOperationException("package.json does not contain a valid \"name\" field.");

            var st = UpmBuildStateStorage.LoadOrCreate();
            st.AssetRootFolder = folderName;
            st.OriginalRootAbs = originalRootAbs;
            st.TempRootAbs = tempRootAbs;
            st.PackagesRootAbs = packagesRootAbs;
            st.PackageId = packageId;
            st.ManifestDependencyValue = UpmManifestDependency.BuildDependencyValue(folderName);
            st.Stage = UpmStage.BuildMovedToTemp;
            UpmBuildStateStorage.Save(st);

            UpmManifestDependency.SetOrUpdate(packageId, st.ManifestDependencyValue);
            st.Stage = UpmStage.BuildManifestUpdated;
            UpmBuildStateStorage.Save(st);

            EditorApplication.delayCall += MoveTempToPackagesAndResolve;
        }

        /// <summary>
        /// Starts the return flow for converting the embedded package back into a project folder.
        /// The flow removes the manifest dependency, resolves packages, then moves the folder out of Packages.
        /// On completion, the UPM define symbol is removed to recompile into project mode.
        /// </summary>
        public static void Return()
        {
#if !UPM_PACKAGE
            Debug.LogError($"[{nameof(UpmBuildFlow)}] Return requires the {UpmConstants.UpmDefine} define.");
#else
            var cfg = AssetPublisherToolsSettings.Instance;

            if (!cfg.TrySyncImmediately())
            { 
                throw new InvalidOperationException("Unity editor is busy. Try again after compilation or import completes.");
            }

            var folderName = UpmPathUtility.GetSafeFolderName(cfg.AssetRootFolder);

            var st = UpmBuildStateStorage.LoadOrCreate();
            if (string.IsNullOrWhiteSpace(st.AssetRootFolder))
                st.AssetRootFolder = folderName;

            if (string.IsNullOrWhiteSpace(st.PackageId))
            {
                var packageJsonAbs = Path.Combine(
                    UpmPathUtility.ProjectRootAbs,
                    UpmConstants.PackagesFolderName,
                    folderName,
                    UpmConstants.PackageJsonFileName);

                st.PackageId = PackageJsonUtility.GetPackageName(packageJsonAbs);
            }

            if (string.IsNullOrWhiteSpace(st.PackageId))
                throw new InvalidOperationException("Could not resolve package id for return.");

            UpmManifestDependency.Remove(st.PackageId);
            st.Stage = UpmStage.ReturnManifestRemoved;
            UpmBuildStateStorage.Save(st);

            Client.Resolve();
            EditorApplication.delayCall += MovePackagesBackToProject;
#endif
        }

        /// <summary>
        /// Attempts to resume a pending staged operation based on the persisted state file.
        /// This is used after a domain reload that can happen when define symbols are toggled.
        /// The method schedules the next required action via delayCall and avoids blocking editor startup.
        /// </summary>
        public static void TryResumePendingWork()
        {
            var st = UpmBuildStateStorage.LoadOrCreate();

            switch (st.Stage)
            {
                case UpmStage.BuildManifestUpdated:
                    EditorApplication.delayCall += MoveTempToPackagesAndResolve;
                    return;
                case UpmStage.BuildMovedToPackages:
                    EditorApplication.delayCall += ResolveAfterMove;
                    return;
                case UpmStage.BuildMovedToTemp:
                case UpmStage.BuildResolved:
                case UpmStage.ReturnManifestRemoved:
                default:
                    break;
            }
        }

        #endregion

        #region Build Steps

        private static void MoveTempToPackagesAndResolve()
        {
            var st = UpmBuildStateStorage.LoadOrCreate();
            if (st.Stage != UpmStage.BuildManifestUpdated)
                return;

            if (!Directory.Exists(st.TempRootAbs))
                throw new InvalidOperationException($"Temp folder is missing: {st.TempRootAbs}");

            UpmFileOperations.MoveFolderWithMeta(st.TempRootAbs, st.PackagesRootAbs);
            st.Stage = UpmStage.BuildMovedToPackages;
            UpmBuildStateStorage.Save(st);

            AssetDatabase.Refresh();
            ResolveAfterMove();
        }

        private static void ResolveAfterMove()
        {
            Client.Resolve();
            EditorApplication.delayCall += () =>
            {
                var st = UpmBuildStateStorage.LoadOrCreate();
                st.Stage = UpmStage.BuildResolved;
                UpmBuildStateStorage.Save(st);

                DefineSymbolsManager.AddDefineSymbol(UpmConstants.UpmDefine);
            };
        }

        #endregion

        #region Return Steps

#if UPM_PACKAGE
        private static void MovePackagesBackToProject()
        {
            var cfg = AssetPublisherToolsSettings.Instance;
            var folderName = UpmPathUtility.GetSafeFolderName(cfg.AssetRootFolder);

            var st = UpmBuildStateStorage.LoadOrCreate();
            var packagesAbs = Path.Combine(UpmPathUtility.ProjectRootAbs, UpmConstants.PackagesFolderName, folderName);
            if (!Directory.Exists(packagesAbs))
            {
                UpmBuildStateStorage.Save(st);

                UpmSamplesWorkflow.RestoreIfNeeded(st);
                DefineSymbolsManager.RemoveDefineSymbol(UpmConstants.UpmDefine);
                UpmBuildStateStorage.Clear();
                Debug.Log($"[{nameof(UpmBuildFlow)}] Return completed without package folder move.");
                return;
            }

            var originalRootAbs = !string.IsNullOrWhiteSpace(st.OriginalRootAbs)
                ? st.OriginalRootAbs
                : UpmPathUtility.ToAbsolute(UpmConstants.AssetsFolderName + "/" + folderName);

            var tempRootAbs = Path.Combine(UpmPathUtility.ProjectRootAbs, UpmConstants.TempFolderName, folderName);
            UpmFileOperations.EnsureCleanDirectory(tempRootAbs);
            UpmFileOperations.MoveFolderWithMeta(packagesAbs, tempRootAbs);

            EditorApplication.delayCall += () =>
            {
                UpmFileOperations.MoveFolderWithMeta(tempRootAbs, originalRootAbs);
                AssetDatabase.Refresh();

                UpmBuildStateStorage.Save(st);

                UpmSamplesWorkflow.RestoreIfNeeded(st);
                DefineSymbolsManager.RemoveDefineSymbol(UpmConstants.UpmDefine);
                UpmBuildStateStorage.Clear();
                Debug.Log($"[{nameof(UpmBuildFlow)}] Returned package folder to: {originalRootAbs}");
            };
        }
#endif

        #endregion

        #region Package.json Sync

        private static void SyncPackageJsonFromSettings(AssetPublisherToolsSettings cfg, string packageJsonAbs)
        {
            if (cfg == null)
                return;

            if (!string.IsNullOrWhiteSpace(cfg.PackageVersion))
                PackageJsonUtility.SetPackageVersion(packageJsonAbs, cfg.PackageVersion);

            if (!string.IsNullOrWhiteSpace(cfg.PackageId))
                PackageJsonUtility.SetPackageName(packageJsonAbs, cfg.PackageId);

            if (!string.IsNullOrWhiteSpace(cfg.PackageDisplayName))
                PackageJsonUtility.SetPackageDisplayName(packageJsonAbs, cfg.PackageDisplayName);

            if (!string.IsNullOrWhiteSpace(cfg.PackageDescription))
                PackageJsonUtility.SetPackageDescription(packageJsonAbs, cfg.PackageDescription);
        }

        #endregion
    }
}
