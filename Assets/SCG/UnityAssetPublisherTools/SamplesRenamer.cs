using System.IO;
using SCG.UnityAssetPublisherTools.Helpers;
using UnityEditor;
using UnityEngine;

namespace SCG.UnityAssetPublisherTools
{
	/// <summary>
    /// Toggles visibility of sample and documentation folders for an embedded package.
    /// Renames "Samples~" and "Documentation~" folders to their non-tilde variants and back.
    /// Uses a scripting define to keep menu items and state consistent across reloads.
    /// </summary>
    public static class SamplesRenamer
    {
        private const string Define = "SAMPLES_RENAMED";

        public const string SamplesBase = "Samples~";
        public const string SamplesRenamed = "Samples";
        public const string DocumentationBase = "Documentation~";
        public const string DocumentationRenamed = "Documentation";

        private const string RootName = nameof(SCG) + "/";

		/// <summary>Indicates whether the project currently uses tilde-suffixed folders.</summary>
        public static bool FoldersWithTilda =>
#if !SAMPLES_RENAMED
            true;
#else
            false;
#endif

		/// <summary>
        /// Renames samples and documentation folders to hide or show them in the Project view.
        /// The method mirrors folder renames for both Samples and Documentation to keep structure aligned.
        /// Triggers AssetDatabase refresh via delayed call to let Unity reimport safely.
        /// </summary>
#if !SAMPLES_RENAMED
        [MenuItem(RootName + "Show Samples and Documentation folders", priority = 10000)]
#else
        [MenuItem(RootName + "Hide Samples and Documentation folders", priority = 10000)]
#endif
        public static void HideOrShowSamplesFolder()
        {
            var adAggregatorResource = EditorResourceObject.Instance;
            if (adAggregatorResource == null)
            {
                Debug.LogError("AdAggregatorResource is missing.");
                return;
            }
            var folderPath = AssetDatabase.GetAssetPath(adAggregatorResource.BaseFolder);

            var directorySeparatorChar = '\\';
            var altDirectorySeparatorChar = '/';

            var pathToSamplesWithTilda = Path.Combine(folderPath, SamplesBase).Replace(directorySeparatorChar, altDirectorySeparatorChar);
            var pathToSamplesWoTilda = Path.Combine(folderPath, SamplesRenamed).Replace(directorySeparatorChar, altDirectorySeparatorChar);

            var pathToDocumentationWithTilda = Path.Combine(folderPath, DocumentationBase).Replace(directorySeparatorChar, altDirectorySeparatorChar);
            var pathToDocumentationWoTilda = Path.Combine(folderPath, DocumentationRenamed).Replace(directorySeparatorChar, altDirectorySeparatorChar);

            // Hidden folder
            if (Directory.Exists(pathToSamplesWithTilda))
            {
                Directory.Move(pathToSamplesWithTilda, pathToSamplesWoTilda);
                if (Directory.Exists(pathToDocumentationWithTilda))
                {
                    Directory.Move(pathToDocumentationWithTilda, pathToDocumentationWoTilda);
                }

                DefineSymbolsManager.AddDefineSymbol(Define);
                EditorApplication.delayCall += AssetDatabase.Refresh;
            }
            // Visibled folder
            else if (Directory.Exists(pathToSamplesWoTilda))
            {
                Directory.Move(pathToSamplesWoTilda, pathToSamplesWithTilda);
                if (Directory.Exists(pathToDocumentationWoTilda))
                {
                    Directory.Move(pathToDocumentationWoTilda, pathToDocumentationWithTilda);
                }

                if (File.Exists(pathToSamplesWoTilda + ".meta")) File.Delete(pathToSamplesWoTilda + ".meta");
                if (File.Exists(pathToDocumentationWoTilda + ".meta")) File.Delete(pathToDocumentationWoTilda + ".meta");
                DefineSymbolsManager.RemoveDefineSymbol(Define);
                EditorApplication.delayCall += AssetDatabase.Refresh;
            }
            else
            {
                throw new System.Exception("Folders are not found.");
            }
        }

#if !UNITY_ASTOOLS_EXPERIMENTAL
        [MenuItem(RootName + "Set UNITY_ASTOOLS_EXPERIMENTAL Define", priority = 10000)]
        public static void SetExperimentalDefine() => DefineSymbolsManager.AddDefineSymbol("UNITY_ASTOOLS_EXPERIMENTAL");
#endif
    }
}