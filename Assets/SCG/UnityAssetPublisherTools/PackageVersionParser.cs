using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SCG.UnityAssetPublisherTools
{
    /// <summary>
    /// Provides editor-only helpers for reading and updating package.json metadata.
    /// Intended for embedded or local packages where the JSON file is writable.
    /// Uses lightweight parsing that does not depend on a full JSON serializer.
    /// </summary>
    public class PackageVersionParser
    {
        #region Regex

        private static readonly Regex RxVersion = new(
            "\"version\"\\s*:\\s*\"(?<value>[^\"]*)\"(?<comma>\\s*,?)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        private static readonly Regex RxName = new(
            "\"name\"\\s*:\\s*\"(?<value>[^\"]*)\"(?<comma>\\s*,?)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        #endregion

        /// <summary>
        /// Updates the version field in the target package.json file.
        /// The method preserves an optional trailing comma after the version value.
        /// If the file cannot be located or the key is missing, the call becomes a no-op.
        /// </summary>
        /// <param name="packageAsset">TextAsset pointing to a writable package.json file.</param>
        /// <param name="version">New version value to write into the JSON document.</param>
        public void ChangeVersion(TextAsset packageAsset, string version)
        {
            if (packageAsset == null)
                return;

            if (string.IsNullOrWhiteSpace(version))
            {
                Debug.LogWarning($"[{nameof(PackageVersionParser)}] Version is empty. Skipping package.json update.");
                return;
            }

            var path = AssetDatabase.GetAssetPath(packageAsset);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning($"[{nameof(PackageVersionParser)}] package.json path is invalid.");
                return;
            }

            var json = File.ReadAllText(path);
            var m = RxVersion.Match(json);
            if (!m.Success)
            {
                Debug.LogWarning($"[{nameof(PackageVersionParser)}] Could not find \"version\": \"...\" in package.json.");
                return;
            }

            var oldVer = m.Groups["value"].Value;
            var comma = m.Groups["comma"].Value;

            var newSegment = $"\"version\": \"{version}\"{comma}";
            var newJson = RxVersion.Replace(json, newSegment, 1);

            if (newJson == json)
                return;

            File.SetAttributes(path, FileAttributes.Normal);
            File.WriteAllText(path, newJson);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            Debug.Log($"[{nameof(PackageVersionParser)}] package.json version updated: {oldVer} → {version}");
        }

        /// <summary>
        /// Reads the version field from a package.json asset.
        /// Returns an empty string when the asset path is invalid or the key is missing.
        /// The value is returned as-is without semantic validation.
        /// </summary>
        /// <param name="packageAsset">TextAsset pointing to a package.json file.</param>
        public string GetPackageVersion(TextAsset packageAsset) => GetStringValue(packageAsset, RxVersion, "version");

        /// <summary>
        /// Reads the name field from a package.json asset.
        /// Returns an empty string when the asset path is invalid or the key is missing.
        /// The value is returned as-is without additional normalization.
        /// </summary>
        /// <param name="packageAsset">TextAsset pointing to a package.json file.</param>
        public string GetPackageName(TextAsset packageAsset) => GetStringValue(packageAsset, RxName, "name");

		/// <summary>
        /// Reads a string value using the provided regex from the package.json referenced by the asset.
        /// Logs a warning when the asset path is invalid or when the key cannot be located.
        /// Returns an empty string when the read operation cannot be completed.
        /// </summary>
        /// <param name="packageAsset">TextAsset pointing to a package.json file.</param>
        /// <param name="rx">Regex that captures the target value into a named group.</param>
        /// <param name="key">JSON key used only for diagnostic messages.</param>
        private static string GetStringValue(TextAsset packageAsset, Regex rx, string key)
        {
            if (packageAsset == null)
                return string.Empty;

            var path = AssetDatabase.GetAssetPath(packageAsset);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning($"[{nameof(PackageVersionParser)}] package.json path is invalid.");
                return string.Empty;
            }

            var json = File.ReadAllText(path);
            var m = rx.Match(json);
            if (m.Success)
                return m.Groups["value"].Value.Trim();

            Debug.LogWarning($"[{nameof(PackageVersionParser)}] Could not find \"{key}\": \"...\" in package.json.");
            return string.Empty;
        }
    }
}
