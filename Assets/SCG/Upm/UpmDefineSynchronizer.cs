using System.IO;
using SCG.UnityAssetPublisherTools.Helpers;

namespace SCG.UnityAssetPublisherTools.Upm
{
    /// <summary>
    /// Keeps the <see cref="UpmConstants.UpmDefine"/> scripting define symbol synchronized with the current project state.
    /// The define is enabled when a package.json exists under Packages and disabled otherwise.
    /// This helps avoid a mismatched compilation mode when the user manually moves or deletes folders.
    /// </summary>
    internal static class UpmDefineSynchronizer
    {
        /// <summary>
        /// Applies define symbol changes based on whether the configured package.json exists under Packages.
        /// The method is safe to call repeatedly and updates only when a change is required.
        /// It uses best-effort behavior for different build targets via <see cref="DefineSymbolsManager"/>.
        /// </summary>
        public static void SyncDefineWithPackagesFolder()
        {
            var cfg = AssetPublisherToolsSettings.Instance;
            var folderName = UpmPathUtility.GetSafeFolderName(cfg.AssetRootFolder);
			/*var folderName = !string.IsNullOrWhiteSpace(cfg.PackageRootFolder)
                ? UpmPathUtility.GetSafeFolderName(cfg.PackageRootFolder)
                : UpmPathUtility.GetSafeFolderName(cfg.AssetRootFolder);*/
            var packagesAbs = Path.Combine(UpmPathUtility.ProjectRootAbs, UpmConstants.PackagesFolderName, folderName);

            var packageJsonAbs = Path.Combine(packagesAbs, UpmConstants.PackageJsonFileName);
            if (File.Exists(packageJsonAbs))
                DefineSymbolsManager.AddDefineSymbol(UpmConstants.UpmDefine);
            else
                DefineSymbolsManager.RemoveDefineSymbol(UpmConstants.UpmDefine);
        }
    }
}
