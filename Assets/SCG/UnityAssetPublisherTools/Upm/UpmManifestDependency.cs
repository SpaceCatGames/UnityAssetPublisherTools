using System.IO;
using SCG.UnityAssetPublisherTools.Helpers;

namespace SCG.UnityAssetPublisherTools.Upm
{
    /// <summary>
    /// Updates Packages/manifest.json to add or remove a local file dependency for the staged package.
    /// The dependency uses a relative file URI that points to the final Packages location.
    /// The helper is internal because it is specific to the staged UPM workflow.
    /// </summary>
    internal static class UpmManifestDependency
    {
        /// <summary>
        /// Builds a manifest dependency value for the given folder name.
        /// The returned value uses a file reference relative to the manifest file.
        /// This formatting matches Unity's local package dependency style.
        /// </summary>
        /// <param name="folderName">Folder name under Packages.</param>
        public static string BuildDependencyValue(string folderName) =>
            "file:../" + UpmConstants.PackagesFolderName + "/" + folderName;

        /// <summary>
        /// Adds or updates the dependency for the specified package id inside Packages/manifest.json.
        /// The method performs a minimal text update limited to the dependencies object.
        /// The call throws when manifest.json cannot be located.
        /// </summary>
        /// <param name="packageId">Package id used as dependency key.</param>
        /// <param name="dependencyValue">Dependency value to write.</param>
        public static void SetOrUpdate(string packageId, string dependencyValue)
        {
            var manifestAbs = Path.Combine(UpmPathUtility.ProjectRootAbs, UpmConstants.PackagesFolderName, UpmConstants.ManifestFileName);
            ManifestJsonUtility.SetDependency(manifestAbs, packageId, dependencyValue);
        }

        /// <summary>
        /// Removes the dependency entry for the specified package id from Packages/manifest.json.
        /// The method performs a minimal text update limited to the dependencies object.
        /// The call throws when manifest.json cannot be located.
        /// </summary>
        /// <param name="packageId">Package id used as dependency key.</param>
        public static void Remove(string packageId)
        {
            var manifestAbs = Path.Combine(UpmPathUtility.ProjectRootAbs, UpmConstants.PackagesFolderName, UpmConstants.ManifestFileName);
            ManifestJsonUtility.RemoveDependency(manifestAbs, packageId);
        }
    }
}
