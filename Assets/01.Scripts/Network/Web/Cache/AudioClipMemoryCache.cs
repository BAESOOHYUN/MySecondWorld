using UnityEngine;

namespace MySecondWorld.Network.Web
{
    public class AudioClipMemoryCache
    {
        private readonly LRUCache<string, AudioClip> _cache;

        public AudioClipMemoryCache(int capacity = 30)
        {
            _cache = new LRUCache<string, AudioClip>(capacity, Object.Destroy);
        }

        public bool TryGet(string url, out AudioClip clip) => _cache.TryGet(url, out clip);

        public void Put(string url, AudioClip clip) => _cache.Put(url, clip);

        // AudioClip의 byte[] 변환은 포맷(WAV/MP3/OGG)마다 다르게 처리해야 해서
        // WebResourceService에서 UnityWebRequestMultimedia로 처리할 예정.
        // 여기선 저장/조회만.
    }
}