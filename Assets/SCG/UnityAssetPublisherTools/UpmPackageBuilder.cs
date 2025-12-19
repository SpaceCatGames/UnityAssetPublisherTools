using SCG.UnityAssetPublisherTools.Helpers;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

// ReSharper disable NotAccessedField.Local
#pragma warning disable IDE0079
#pragma warning disable IDE0051
#pragma warning restore IDE0079

namespace SCG.UnityAssetPublisherTools
{
    /// <summary>
    /// UPM build/return automation.
    /// - In non-UPM mode builds a local UPM package by moving the *entire base folder* to Temp/SCG,
    ///   taking both BaseFolder and the actual package.json path from EditorResourceObject;
    ///   ensures Samples~ are hidden, writes the effective package.json, and adds via Package Manager.
    /// - In UPM mode removes the local package, restores the folder + its .meta back,
    ///   restores package.json if it was swapped, and reverts Samples~ state.
    /// </summary>
    public static class UpmPackageBuilder
    {
        #region Constants

        /// <summary>
        /// Root menu name prefix (ends with slash).
        /// </summary>
        private const string RootName = nameof(SCG) + "/";

        /// <summary>
        /// Scripting define used to toggle menu label and flow.
        /// </summary>
        private const string Define = "UPM_PACKAGE";

        private const string PackageJsonName = "package.json";
        private const string FreePackageJsonName = "package.free.json";
        private const string StateFileName = nameof(SCG) + "_UpmBuildState.json";

        #endregion

        public static bool UpmPackage =>
#if UPM_PACKAGE
            true;
#else
            false;
#endif

        #region Menu Entry

#if !UPM_PACKAGE
        /// <summary>
        /// Builds a local UPM package:
        /// - Validates and toggles Samples~ if needed;
        /// - Moves the base folder (from EditorResourceObject.BaseFolder) to Temp/SCG, with root .meta;
        /// - Resolves the *actual* package json from EditorResourceObject.PackageJson and copies it to package.json;
        /// - Adds the package via Client.Add("file:...") and sets the UPM_PACKAGE define.
        /// </summary>
        [MenuItem(RootName + "Build for UPM Package", priority = 5000)]
        public static void BuildOrReturn() => PrepareSamplesAndSchedule(BuildFlow, true);
#else
        /// <summary>
        /// Returns everything back:
        /// - Removes the local package via Package Manager;
        /// - Restores swapped package.json if needed;
        /// - Moves the folder back from Temp to the original location, with .meta;
        /// - Restores Samples~ rename if toggled by the tool;
        /// - Removes the UPM_PACKAGE define.
        /// </summary>
        [MenuItem(RootName + "Return from UPM Package (to project)", priority = 5000)]
        public static void BuildOrReturn() => PrepareSamplesAndSchedule(ReturnFlow, false);
#endif

        #endregion

        #region Samples~ Handling

        /// <summary>
        /// Ensures Samples~ are in hidden state before the main action (build/return).
        /// Uses delayCall so Unity can safely reimport after rename.
        /// Persists whether we toggled Samples~ to mirror on return.
        /// </summary>
        private static void PrepareSamplesAndSchedule(Action action, bool buildFlow)
        {
            try
            {
                var toggled = false;
                if (!SamplesRenamer.FoldersWithTilda)
                {
                    SamplesRenamer.HideOrShowSamplesFolder();
                    toggled = true;
                }
                var st = LoadOrCreateState();
                st.SamplesWereToggledByTool = toggled;
                SaveState(st);

                if (buildFlow) DefineSymbolsManager.AddDefineSymbol(Define);
                else DefineSymbolsManager.RemoveDefineSymbol(Define);

                EditorApplication.delayCall += () => action();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UPM] Samples~ check/toggle failed: {ex.Message}");
            }
        }

        #endregion

        #region Build (non-UPM)

#if !UPM_PACKAGE
        private static AddRequest addRequest;

        /// <summary>
        /// Full build flow based on EditorResourceObject.
        /// </summary>
        private static void BuildFlow()
        {
            var st = LoadOrCreateState();

            // Load resource strictly typed; abort if missing
            var res = EditorResourceObject.Instance;
            if (res == null)
            {
                // Keep define consistent with failure:
                DefineSymbolsManager.RemoveDefineSymbol(Define);
                throw new InvalidOperationException("EditorResourceObject not found in Resources.");
            }

            // Resolve base folder from the resource (must be set in the asset)
            var baseFolder = ResolveBaseFolderFromResource(res);
            if (string.IsNullOrEmpty(baseFolder) || !Directory.Exists(baseFolder))
            {
                DefineSymbolsManager.RemoveDefineSymbol(Define);
                throw new InvalidOperationException($"BaseFolder not found or invalid: {baseFolder}");
            }

            // Prepare Temp destination and move the *entire* folder with root .meta
            var tempRoot = Path.Combine(ProjectRoot, "Temp", nameof(SCG));
            EnsureCleanDirectory(tempRoot);
            MoveFolderWithMeta(baseFolder, tempRoot);
            SamplesMetaBaker.Bake(tempRoot);

            st.OriginalRoot = baseFolder.Replace('\\', '/');
            st.TempRoot = tempRoot.Replace('\\', '/');

            // Determine the actual package json from the resource and ensure 'package.json' exists in Temp
            var sourcePackageJsonAbs = TryGetPackageJsonPathFromResource(res);
            if (string.IsNullOrEmpty(sourcePackageJsonAbs) || !File.Exists(sourcePackageJsonAbs))
            {
                // Fallback: try inside moved temp root
                var tryPkg = Path.Combine(tempRoot, PackageJsonName);
                var tryFree = Path.Combine(tempRoot, FreePackageJsonName);
                if (File.Exists(tryFree))
                    sourcePackageJsonAbs = tryFree;
                else if (File.Exists(tryPkg))
                    sourcePackageJsonAbs = tryPkg;
            }

            if (string.IsNullOrEmpty(sourcePackageJsonAbs) || !File.Exists(sourcePackageJsonAbs))
            {
                DefineSymbolsManager.RemoveDefineSymbol(Define);
                throw new InvalidOperationException("package.json (actual) not found. Set it in EditorResourceObject.PackageJson.");
            }

            // If the source is not already 'Temp/.../package.json', copy it over package.json
            var tempPackageJson = Path.Combine(tempRoot, PackageJsonName);
            if (!PathsEqual(sourcePackageJsonAbs, tempPackageJson))
            {
                var hadExisting = File.Exists(tempPackageJson);
                if (hadExisting)
                {
                    File.Copy(tempPackageJson, tempPackageJson + ".bak", true);
                    st.HadPackageJsonBackup = true;
                }
                File.Copy(sourcePackageJsonAbs, tempPackageJson, true);
                st.SwappedFreeToPackageJson = true; // generalized flag: we replaced the on-disk package.json
            }

            // Read package name from the source JSON we actually use
            st.PackageName = res.PackageName;
            if (string.IsNullOrEmpty(st.PackageName))
            {
                DefineSymbolsManager.RemoveDefineSymbol(Define);
                throw new InvalidOperationException("Cannot read 'name' from package.json (effective).");
            }

            SaveState(st);

            // Add local package via PM
            var pathArg = "file:" + tempRoot.Replace('\\', '/');
            addRequest = Client.Add(pathArg);
            EditorApplication.update += OnAddProgress;
        }

        /// <summary>
        /// PM add progress callback.
        /// </summary>
        private static void OnAddProgress()
        {
            if (addRequest is not { IsCompleted: true }) return;

            EditorApplication.update -= OnAddProgress;

            if (addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[UPM] Added local package: {addRequest.Result?.name} ({addRequest.Result?.version})");
            }
            else
            {
                Debug.LogError($"[UPM] Add failed: {addRequest.Error?.message}");
                // Optional: remove define on failure to keep editor state sane
                DefineSymbolsManager.RemoveDefineSymbol(Define);
            }

            AssetDatabase.Refresh();
        }
#endif

        #endregion

        #region Return (UPM)

#if UPM_PACKAGE
        private static RemoveRequest removeRequest;

        /// <summary>
        /// Start return flow: remove PM entry, then restore files.
        /// </summary>
        private static void ReturnFlow()
        {
            var st = LoadOrCreateState();

            if (!string.IsNullOrEmpty(st.PackageName))
            {
                removeRequest = Client.Remove(st.PackageName);
                EditorApplication.update += OnRemoveProgress;
            }
            else
            {
                Debug.LogWarning("[UPM] Package name not found in state; restoring files only.");
                CompleteReturnFileOps();
            }
        }

        /// <summary>
        /// PM remove progress callback → continues with restoration.
        /// </summary>
        private static void OnRemoveProgress()
        {
            if (removeRequest is not { IsCompleted: true }) return;

            EditorApplication.update -= OnRemoveProgress;

            if (removeRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[UPM] Removed package: {removeRequest.PackageIdOrName}");
            }
            else
            {
                Debug.LogWarning($"[UPM] Remove failed or not installed: {removeRequest.Error?.message}");
            }

            CompleteReturnFileOps();
        }

        /// <summary>
        /// Restores package.json backup if any, moves folder back, and reverts Samples~ state.
        /// </summary>
        private static void CompleteReturnFileOps()
        {
            var st = LoadOrCreateState();

            if (st.SwappedFreeToPackageJson && !string.IsNullOrEmpty(st.TempRoot))
            {
                var pkg = Path.Combine(st.TempRoot, PackageJsonName);
                var bak = pkg + ".bak";
                if (File.Exists(bak))
                {
                    File.Copy(bak, pkg, true);
                    File.Delete(bak);
                }
                st.SwappedFreeToPackageJson = false;
                st.HadPackageJsonBackup = false;
                SaveState(st);
            }

            if (!string.IsNullOrEmpty(st.TempRoot) && !string.IsNullOrEmpty(st.OriginalRoot))
            {
                MoveFolderWithMeta(st.TempRoot, st.OriginalRoot);
            }

            try
            {
                if (st.SamplesWereToggledByTool && SamplesRenamer.FoldersWithTilda)
                {
                    SamplesRenamer.HideOrShowSamplesFolder();
                }
                st.SamplesWereToggledByTool = false;
                SaveState(st);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UPM] Samples~ revert failed: {ex.Message}");
            }

            AssetDatabase.Refresh();
            Debug.Log("[UPM] Return completed.");
        }
#endif

        #endregion

        #region Resource-based Resolution

        /// <summary>
        /// Resolves base folder absolute path from EditorResourceObject.BaseFolder.
        /// </summary>
        private static string ResolveBaseFolderFromResource(EditorResourceObject res)
        {
            if (res == null || res.BaseFolder == null)
                return null;

            var assetPath = AssetDatabase.GetAssetPath(res.BaseFolder);
            return string.IsNullOrEmpty(assetPath) ? null : ToAbsolute(assetPath);
        }

        /// <summary>
        /// Tries to resolve the absolute path to the *actual* package json from EditorResourceObject.
        /// Expects a field/property named "PackageJson" that references a file asset.
        /// If your field is named differently, adjust the asset path fetch here.
        /// </summary>
        private static string TryGetPackageJsonPathFromResource(EditorResourceObject res) =>
            AssetDatabase.GetAssetPath(res.PackageAsset);

        #endregion

        #region File Ops

        /// <summary>
        /// Moves a folder and its root .meta from src to dst. Replaces existing dst.
        /// </summary>
        private static void MoveFolderWithMeta(string src, string dst)
        {
            if (!Directory.Exists(src))
                throw new DirectoryNotFoundException(src);

            var dstParent = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dstParent) && !Directory.Exists(dstParent))
                Directory.CreateDirectory(dstParent);

            if (Directory.Exists(dst))
                Directory.Delete(dst, true);

            try
            {
                FileUtil.MoveFileOrDirectory(src, dst);
            }
            catch (Exception)
            {
                Debug.LogError("Please, close ALL files that may block move operations.");
                DefineSymbolsManager.RemoveDefineSymbol(Define);
                throw;
            }

            var metaSrc = src.TrimEnd('\\', '/') + ".meta";
            var metaDst = dst.TrimEnd('\\', '/') + ".meta";
            if (!File.Exists(metaSrc)) return;
            if (File.Exists(metaDst)) File.Delete(metaDst);
            FileUtil.MoveFileOrDirectory(metaSrc, metaDst);
        }

        /// <summary>
        /// Ensures directory exists and is empty.
        /// </summary>
        private static void EnsureCleanDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            Directory.CreateDirectory(path);
        }

        #endregion

        #region Paths & State

        /// <summary>
        /// Project root (parent of Assets).
        /// </summary>
        private static string ProjectRoot
        {
            get
            {
                var dir = Directory.GetParent(Application.dataPath);
                return dir != null ? dir.FullName : Application.dataPath;
            }
        }

        /// <summary>
        /// Converts project-relative to absolute; passes through absolute.
        /// </summary>
        private static string ToAbsolute(string path) => Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(ProjectRoot, path));

        /// <summary>
        /// Compares two paths for equality after normalization.
        /// </summary>
        private static bool PathsEqual(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            var na = Path.GetFullPath(a).Replace('\\', '/').TrimEnd('/');
            var nb = Path.GetFullPath(b).Replace('\\', '/').TrimEnd('/');
            return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
        }

        [Serializable]
        private sealed class UpmBuildState
        {
            /// <summary>Original project folder absolute path.</summary>
            public string OriginalRoot;

            /// <summary>Temp folder absolute path.</summary>
            public string TempRoot;

            /// <summary>UPM package name read from effective package.json.</summary>
            public string PackageName;

            /// <summary>True if we replaced/copy-over effective package.json.</summary>
            public bool SwappedFreeToPackageJson;

            /// <summary>True if a backup for package.json existed and was created.</summary>
            public bool HadPackageJsonBackup;

            /// <summary>True if Samples~ were toggled by this tool.</summary>
            public bool SamplesWereToggledByTool;
        }

        private static UpmBuildState LoadOrCreateState()
        {
            var path = Path.Combine(ProjectRoot, "Temp", StateFileName);
            if (!File.Exists(path)) return new UpmBuildState();
            try
            {
                var json = File.ReadAllText(path);
                var st = JsonUtility.FromJson<UpmBuildState>(json);
                return st ?? new UpmBuildState();
            }
            catch
            {
                return new UpmBuildState();
            }
        }

        private static void SaveState(UpmBuildState st)
        {
            var dir = Path.Combine(ProjectRoot, "Temp");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, StateFileName);
            var json = JsonUtility.ToJson(st, true);
            File.WriteAllText(path, json);
        }

        #endregion
    }
}
