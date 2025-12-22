using System;
using System.IO;

namespace SCG.UnityAssetPublisherTools.Upm
{
    /// <summary>
    /// Resolves and prepares the effective package.json file inside the Temp staging folder.
    /// The helper supports both explicit PackageAsset references and convention-based discovery.
    /// When a selected PackageAsset points to a file within the moved folder, the relative path is preserved.
    /// </summary>
    internal static class UpmPackageJsonStaging
    {
        /// <summary>
        /// Copies the effective package json into Temp/package.json.
        /// The method may map an inspector-selected file into the moved Temp folder.
        /// If no explicit file is provided, the method prefers package.free.json when present.
        /// </summary>
        /// <param name="cfg">Settings instance used to locate optional package json asset.</param>
        /// <param name="originalRootAbs">Absolute original root folder path.</param>
        /// <param name="tempRootAbs">Absolute temp root folder path.</param>
        public static string EnsureEffectivePackageJson(AssetPublisherToolsSettings cfg, string originalRootAbs, string tempRootAbs)
        {
            if (string.IsNullOrEmpty(tempRootAbs))
                throw new ArgumentException("Temp root is empty.", nameof(tempRootAbs));

            var tempPackageJsonAbs = Path.Combine(tempRootAbs, UpmConstants.PackageJsonFileName);
            var sourceAbs = ResolveEffectivePackageJsonAbs(cfg, originalRootAbs, tempRootAbs);

            if (string.IsNullOrEmpty(sourceAbs) || !File.Exists(sourceAbs))
                throw new InvalidOperationException("Effective package json could not be resolved.");

            if (!UpmPathUtility.PathsEqual(sourceAbs, tempPackageJsonAbs))
                File.Copy(sourceAbs, tempPackageJsonAbs, true);

            return tempPackageJsonAbs;
        }

        private static string ResolveEffectivePackageJsonAbs(AssetPublisherToolsSettings cfg, string originalRootAbs, string tempRootAbs)
        {
            if (cfg != null && cfg.PackageAsset != null)
            {
                var assetPath = UnityEditor.AssetDatabase.GetAssetPath(cfg.PackageAsset);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var abs = UpmPathUtility.ToAbsolute(assetPath);
                    var mapped = UpmPathUtility.MapMovedFileToTemp(originalRootAbs, tempRootAbs, abs);
                    if (File.Exists(mapped))
                        return mapped;

                    var fallbackByName = Path.Combine(tempRootAbs, Path.GetFileName(abs));
                    if (File.Exists(fallbackByName))
                        return fallbackByName;
                }
            }

            var free = Path.Combine(tempRootAbs, UpmConstants.FreePackageJsonFileName);
            if (File.Exists(free))
                return free;

            var pkg = Path.Combine(tempRootAbs, UpmConstants.PackageJsonFileName);
            return File.Exists(pkg) ? pkg : null;
        }
    }
}
