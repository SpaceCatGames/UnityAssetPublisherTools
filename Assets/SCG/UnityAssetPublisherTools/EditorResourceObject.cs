using UnityEditor;
using UnityEngine;

namespace SCG.UnityAssetPublisherTools
{
	/// <summary>
    /// Stores editor configuration used by Unity Asset Publisher Tools.
    /// The asset is expected to live in a Resources folder for easy discovery.
    /// Values are validated and synchronized from package.json when possible.
    /// </summary>
    [CreateAssetMenu(fileName = nameof(EditorResourceObject), menuName = nameof(SCG) + "/" + nameof(EditorResourceObject))]
    public sealed class EditorResourceObject : ScriptableObject
    {
        /// <summary>Package version written to package.json and PlayerSettings.bundleVersion.</summary>
        [field: SerializeField]
        [field: Tooltip("Package version written to package.json and PlayerSettings.bundleVersion.")]
        public string PackageVersion { get; set; }

        /// <summary>Project folder asset that contains the package root.</summary>
        [field: SerializeField]
        [field: Tooltip("Project folder asset that contains the package root.")]
        public Object BaseFolder { get; set; }

        /// <summary>TextAsset reference to the effective package.json file.</summary>
        [field: SerializeField]
        [field: Tooltip("TextAsset reference to the effective package.json file.")]
        public TextAsset PackageAsset { get; set; }

        /// <summary>Project-relative path to the asset root folder under Assets.</summary>
        [field: SerializeField]
        [field: Tooltip("Project-relative path to the asset root folder under Assets.")]
        public string AssetFolderName { get; set; } = nameof(SCG);
        /// <summary>Project-relative path or template for the package root under Packages.</summary>
        [field: SerializeField]
        [field: Tooltip("Project-relative path or template for the package root under Packages.")]
        public string PackageName { get; set; }

        private const string AssetsFolderPath = "Assets/";
#if UPM_PACKAGE
        private const string PackagesFolderPath = "Packages/";
#endif
        private const string PackageJsonFileName = "/package.json";

        private static readonly PackageVersionParser packageVersionParser = new();

        private static EditorResourceObject s_instance;

		/// <summary>
        /// Gets the cached resource instance.
        /// The method attempts to load the asset from Resources and falls back to a temporary instance.
        /// Callers should treat the fallback instance as non-persistent editor state.
        /// </summary>
        public static EditorResourceObject Instance
        {
            get
            {
                if (s_instance != null) return s_instance;
                s_instance = Resources.Load<EditorResourceObject>(nameof(EditorResourceObject));
                if (s_instance != null) return s_instance;

                s_instance = CreateInstance<EditorResourceObject>();
                return s_instance;
            }
        }

		/// <summary>
        /// Invoked when the asset instance becomes alive in the editor domain.
        /// Ensures that validation logic is applied when loaded from Resources.
        /// This method is editor-focused and does not persist changes at runtime.
        /// </summary>
        private void Awake() => OnValidate();

		/// <summary>
        /// Validates and populates references required by editor tooling.
        /// The method keeps package.json and PlayerSettings.bundleVersion synchronized with PackageVersion.
        /// It also resolves the package folder path template when the package name can be read.
        /// </summary>
        private void OnValidate()
        {
#if !UPM_PACKAGE
            if (PackageAsset == null) PackageAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetsFolderPath + AssetFolderName + PackageJsonFileName);

            if (BaseFolder == null) BaseFolder = AssetDatabase.LoadAssetAtPath<Object>(AssetsFolderPath + AssetFolderName);

            if (PackageAsset == null) return;

            PackageVersion = packageVersionParser.GetPackageVersion(PackageAsset);
            PlayerSettings.bundleVersion = PackageVersion;
            packageVersionParser.ChangeVersion(PackageAsset, PackageVersion);

            if (string.IsNullOrEmpty(PackageName))
                PackageName = packageVersionParser.GetPackageName(PackageAsset);
#else
            var path = PackagesFolderPath + PackageName;
            if (BaseFolder == null) BaseFolder = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (PackageAsset == null) PackageAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path + PackageJsonFileName);
#endif
        }

		/// <summary>
        /// Invoked when the asset is unloaded or destroyed by the editor.
        /// Clears the cached singleton so the next access can reload from Resources.
        /// This method does not delete the underlying asset file.
        /// </summary>
        private void OnDestroy() => s_instance = null;
    }
}
