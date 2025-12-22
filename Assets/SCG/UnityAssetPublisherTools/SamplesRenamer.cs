using System;
using System.IO;
using SCG.UnityAssetPublisherTools.Helpers;
using UnityEditor;
using UnityEngine;
using static SCG.UnityAssetPublisherTools.Constants;

namespace SCG.UnityAssetPublisherTools
{
    /// <summary>
    /// Toggles visibility of sample and documentation folders for an embedded package.
    /// Renames "Samples~" and "Documentation~" folders to their non-tilde variants and back.
    /// Uses a scripting define to keep menu items and state consistent across reloads.
    /// </summary>
    public static class SamplesRenamer
    {
        /// <summary>
        /// Checks whether Samples and Documentation folders are currently hidden.
        /// The result requires both a matching scripting define state and a matching on-disk folder state.
        /// The method returns true only when Samples are in the tilde-suffixed form and Documentation matches.
        /// </summary>
        public static bool AreFoldersHiddenWithTilda()
        {
            if (DefineSymbolsManager.HasDefineSymbol(SamplesRenamedDefineSymbol))
                return false;

            if (!TryGetPackageRootPath(out var rootPath))
                return false;

            var samples = GetPairState(rootPath, SamplesBase, SamplesRenamed);
            if (samples != FolderPairState.Base)
                return false;

            var docs = GetPairState(rootPath, DocumentationBase, DocumentationRenamed);
            return docs is FolderPairState.Base or FolderPairState.Missing;
        }

        /// <summary>
        /// Ensures Samples and Documentation folders are hidden with tilde suffixes.
        /// The operation fixes partial states by applying the requested visibility to both folders.
        /// Returns true when any folder or define was changed.
        /// </summary>
        public static bool EnsureHidden()
        {
            if (!TryGetPackageRootPath(out var rootPath))
                return false;

            var changed = EnsurePairState(rootPath, SamplesBase, SamplesRenamed, shouldBeVisible: false);
            changed |= EnsurePairState(rootPath, DocumentationBase, DocumentationRenamed, shouldBeVisible: false);

            changed |= DefineSymbolsManager.RemoveDefineSymbol(SamplesRenamedDefineSymbol);
            if (changed)
                ScheduleRefresh();

            return changed;
        }

        /// <summary>
        /// Ensures Samples and Documentation folders are visible without tilde suffixes.
        /// The operation fixes partial states by applying the requested visibility to both folders.
        /// Returns true when any folder or define was changed.
        /// </summary>
        public static bool EnsureVisible()
        {
            if (!TryGetPackageRootPath(out var rootPath))
                return false;

            var changed = EnsurePairState(rootPath, SamplesBase, SamplesRenamed, shouldBeVisible: true);
            changed |= EnsurePairState(rootPath, DocumentationBase, DocumentationRenamed, shouldBeVisible: true);

            changed |= DefineSymbolsManager.AddDefineSymbol(SamplesRenamedDefineSymbol);
            if (changed)
                ScheduleRefresh();

            return changed;
        }

        /// <summary>
        /// Renames samples and documentation folders to hide or show them in the Project view.
        /// The method mirrors folder renames for both Samples and Documentation to keep structure aligned.
        /// Triggers AssetDatabase refresh via delayed call to let Unity reimport safely.
        /// </summary>
#if !SAMPLES_RENAMED
        [MenuItem(MenuRoot + "Show Samples and Documentation folders", priority = 10000)]
#else
        [MenuItem(MenuRoot + "Hide Samples and Documentation folders", priority = 10000)]
#endif
        public static void HideOrShowSamplesFolder()
        {
            var defineIsSet = DefineSymbolsManager.HasDefineSymbol(SamplesRenamedDefineSymbol);

            if (defineIsSet)
            {
                EnsureHidden();
                return;
            }

            EnsureVisible();
        }

        #region Internals

        private enum FolderPairState
        {
            Missing = 0,
            Base = 1,
            Renamed = 2,
            Conflict = 3
        }

        private static bool TryGetPackageRootPath(out string rootPath)
        {
            rootPath = string.Empty;

            var settings = AssetPublisherToolsSettings.Instance;
            if (settings == null)
            {
                Debug.LogError("AssetPublisherToolsSettings is missing.");
                return false;
            }

            if (settings.BaseFolder == null)
            {
                Debug.LogError("BaseFolder is missing in AssetPublisherToolsSettings.");
                return false;
            }

            rootPath = AssetDatabase.GetAssetPath(settings.BaseFolder);
            if (!string.IsNullOrEmpty(rootPath)) return true;

            Debug.LogError("BaseFolder path is empty.");
            return false;
        }

        private static FolderPairState GetPairState(string rootPath, string baseName, string renamedName)
        {
            var basePath = Path.Combine(rootPath, baseName);
            var renamedPath = Path.Combine(rootPath, renamedName);

            var hasBase = Directory.Exists(basePath);
            var hasRenamed = Directory.Exists(renamedPath);

            return hasBase switch
            {
                true when hasRenamed => FolderPairState.Conflict,
                true => FolderPairState.Base,
                _ => hasRenamed ? FolderPairState.Renamed : FolderPairState.Missing
            };
        }

        private static bool EnsurePairState(string rootPath, string baseName, string renamedName, bool shouldBeVisible)
        {
            var state = GetPairState(rootPath, baseName, renamedName);

            switch (state)
            {
                case FolderPairState.Missing:
                    return false;
                case FolderPairState.Conflict:
                    throw new InvalidOperationException($"Both '{baseName}' and '{renamedName}' exist. Resolve the conflict manually.");
                case FolderPairState.Base:
                case FolderPairState.Renamed:
                default:
                    break;
            }

            var basePath = Path.Combine(rootPath, baseName);
            var renamedPath = Path.Combine(rootPath, renamedName);

            if (shouldBeVisible)
            {
                if (state == FolderPairState.Renamed)
                    return false;

                MoveFolderWithMeta(basePath, renamedPath);
                return true;
            }

            if (state == FolderPairState.Base)
                return false;

            MoveFolderWithMeta(renamedPath, basePath);
            return true;
        }

        private static void MoveFolderWithMeta(string srcPath, string dstPath)
        {
            if (!Directory.Exists(srcPath))
                return;

            try
            {
                FileUtil.MoveFileOrDirectory(srcPath, dstPath);
            }
            catch (Exception)
            {
                Debug.LogError("Failed to move files. " +
                    "Close any applications that may lock project files " +
                    "(File Explorer windows, IDEs/code editors, VCS clients, antivirus scanners, and any external processes touching the folder) " +
                    "and try again.");
                throw;
            }

            var srcMeta = srcPath + ".meta";
            if (!File.Exists(srcMeta))
                return;

            var dstMeta = dstPath + ".meta";
            if (File.Exists(dstMeta))
                File.Delete(dstMeta);

            FileUtil.MoveFileOrDirectory(srcMeta, dstMeta);
        }

        private static void ScheduleRefresh()
        {
            EditorApplication.delayCall -= RefreshAssets;
            EditorApplication.delayCall += RefreshAssets;
        }

        private static void RefreshAssets() => AssetDatabase.Refresh();

        #endregion
    }
}