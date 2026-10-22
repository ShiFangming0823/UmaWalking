using System;

namespace UmaDesktop
{
    [Serializable] public sealed class DesktopAssetFile
    {
        public string path, sha256;
        public long bytes;
    }
    [Serializable] public sealed class DesktopAssetPlan
    {
        public int schemaVersion=1, characters, costumes;
        public string source, createdUtc, metaSha256, masterSha256;
        public long bytes;
        public DesktopAssetFile[] files;
    }
}
