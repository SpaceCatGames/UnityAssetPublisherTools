using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SCG.UnityAssetPublisherTools.Upm
{
    /// <summary>
    /// Provides file operations used by the staged UPM workflow.
    /// The helpers handle folder moves, meta file moves, and temp folder cleanup.
    /// Exceptions are propagated so the caller can stop the flow while keeping the state for inspection.
    /// </summary>
    internal static class UpmFileOperations
    {
        /// <summary>
        /// Ensures the directory exists and is empty.
        /// When the directory already exists, it is deleted recursively and then recreated.
        /// This helper is used for Temp staging to avoid mixing files between runs.
        /// </summary>
        /// <param name="absPath">Absolute directory path.</param>
        public static void EnsureCleanDirectory(string absPath)
        {
            if (string.IsNullOrEmpty(absPath))
                return;

            if (Directory.Exists(absPath))
                Directory.Delete(absPath, true);

            Directory.CreateDirectory(absPath);
        }

        /// <summary>
        /// Moves a folder and its root .meta file from src to dst.
        /// The method creates parent directories for dst and replaces an existing destination folder.
        /// When the source and destination paths are equal after normalization, no work is performed.
        /// </summary>
        /// <param name="srcAbs">Absolute source directory path.</param>
        /// <param name="dstAbs">Absolute destination directory path.</param>
        public static void MoveFolderWithMeta(string srcAbs, string dstAbs)
        {
            if (!Directory.Exists(srcAbs))
                throw new DirectoryNotFoundException(srcAbs);

            if (UpmPathUtility.PathsEqual(srcAbs, dstAbs))
                return;

            var dstParent = Path.GetDirectoryName(dstAbs);
            if (!string.IsNullOrEmpty(dstParent) && !Directory.Exists(dstParent))
                Directory.CreateDirectory(dstParent);

            if (Directory.Exists(dstAbs))
                Directory.Delete(dstAbs, true);

            try
            {
                FileUtil.MoveFileOrDirectory(srcAbs, dstAbs);
            }
            catch (Exception)
            {
                Debug.LogError("Failed to move files. " +
                    "Close any applications that may lock project files " +
                    "(File Explorer windows, IDEs/code editors, VCS clients, antivirus scanners, and any external processes touching the folder) " +
                    "and try again.");
                throw;
            }

            MoveMetaIfPresent(srcAbs, dstAbs);
        }

        private static void MoveMetaIfPresent(string srcAbs, string dstAbs)
        {
            var metaSrc = srcAbs.TrimEnd('\\', '/') + ".meta";
            if (!File.Exists(metaSrc))
                return;

            var metaDst = dstAbs.TrimEnd('\\', '/') + ".meta";
            if (File.Exists(metaDst))
                File.Delete(metaDst);

            FileUtil.MoveFileOrDirectory(metaSrc, metaDst);
        }
    }
}