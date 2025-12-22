using System;

namespace SCG.UnityAssetPublisherTools.Upm
{
    /// <summary>
    /// Represents the last completed step of the staged UPM workflow.
    /// The stage value is persisted to disk so the tooling can resume after a domain reload.
    /// The enum is internal because it is not part of the public editor tooling API.
    /// </summary>
    [Serializable]
    internal enum UpmStage
    {
        /// <summary>Files were moved from the original location into Temp.</summary>
        BuildMovedToTemp = 10,

        /// <summary>Packages/manifest.json was updated to point to the final package path.</summary>
        BuildManifestUpdated = 20,

        /// <summary>Files were moved from Temp into Packages.</summary>
        BuildMovedToPackages = 30,

        /// <summary>Package Manager resolve finished after the build flow.</summary>
        BuildResolved = 40,

        /// <summary>Packages/manifest.json dependency entry was removed during return.</summary>
        ReturnManifestRemoved = 110
    }
}
