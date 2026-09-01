using System;
using System.Collections.Generic;

namespace MySecondWorld.Network.Web
{
    [Serializable]
    public class DiskCacheManifest
    {
        public List<DiskCacheEntry> Entries = new();
    }
}