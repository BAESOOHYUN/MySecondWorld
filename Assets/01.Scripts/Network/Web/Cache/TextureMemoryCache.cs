using UnityEngine;

namespace MySecondWorld.Network.Web
{ 
    public class TextureMemoryCache 
    {
        private readonly LRUCache<string, Texture2D> _cache;
        
        public TextureMemoryCache(int capacity = 50)
        {
            _cache = new LRUCache<string, Texture2D>(capacity, Object.Destroy);
        }

        public bool TryGet(string url, out Texture2D texture) => _cache.TryGet(url, out texture);

        public void Put(string url, Texture2D texture) => _cache.Put(url, texture);

        public Texture2D ByteToTexture(byte[] bytes)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(bytes); // 크기 자동 조정
            return tex;
        }
    }
}
