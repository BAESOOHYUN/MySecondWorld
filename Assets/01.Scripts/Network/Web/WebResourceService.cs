using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MySecondWorld.Network.Web
{
    public class WebResourceService : IWebResourceService
    {
        private readonly WebResourceDownloader _downloader;
        private readonly TextureMemoryCache _textureCache;
        private readonly AudioClipMemoryCache _audioCache;
        private readonly DiskCacheService _diskCache;

        public WebResourceService(WebResourceDownloader downloader,
                                  TextureMemoryCache textureCache,
                                  AudioClipMemoryCache audioCache,
                                  DiskCacheService diskCache)
        {
            _downloader = downloader;
            _textureCache = textureCache;
            _audioCache = audioCache;
            _diskCache = diskCache;
        }


        public async UniTask<IResourceHandle<Texture2D>> AcquireTextureAsync(
            string url, IProgress<float> progress = null, CancellationToken ct = default)
        {
            // L1 메모리 캐시 조회
            if (_textureCache.TryGet(url, out var cached))
                return new ResourceHandle<Texture2D>(cached, () => { });

            // L2 디스크 조회
            var diskBytes = await _diskCache.TryGetAsync(url, ct);
            if (diskBytes != null)
            {
                var tex = _textureCache.ByteToTexture(diskBytes);
                _textureCache.Put(url, tex);
                return new ResourceHandle<Texture2D>(tex, () => { });
            }

            // L3 네트워크 다운로드
            var bytes = await _downloader.DownloadAsync(url, progress, ct);
            await _diskCache.SaveAsync(url, bytes, null, ct);
            var texture = _textureCache.ByteToTexture(bytes);
            _textureCache.Put(url, texture);
            return new ResourceHandle<Texture2D>(texture, () => { });
        }

        public async UniTask<IResourceHandle<AudioClip>> AcquireAudioClipAsync(
            string url, IProgress<float> progress = null, CancellationToken ct = default)
        {
            // L1 메모리 캐시 조회
            if (_audioCache.TryGet(url, out var cached))
                return new ResourceHandle<AudioClip>(cached, () => { });

            // L2 디스크 조회
            var diskBytes = await _diskCache.TryGetAsync(url, ct);
            if (diskBytes != null)
            {
                var clip = await BytesToAudioClipAsync(url, diskBytes, ct);
                _audioCache.Put(url, clip);
                return new ResourceHandle<AudioClip>(clip, () => { });
            }

            // L3 네트워크 다운로드
            var bytes = await _downloader.DownloadAsync(url, progress, ct);
            await _diskCache.SaveAsync(url, bytes, null, ct);
            var audioClip = await BytesToAudioClipAsync(url, bytes, ct);
            _audioCache.Put(url, audioClip);
            return new ResourceHandle<AudioClip>(audioClip, () => { });
        }

        // 영상은 캐시 없이 URL 그대로 반환 — VideoPlayer가 자체 스트리밍 처리
        public UniTask<string> GetVideoUrlAsync(string url, CancellationToken ct = default)
            => UniTask.FromResult(url);

        // UnityWebRequestMultimedia로 포맷 자동 감지 (WAV/MP3/OGG)
        private async UniTask<AudioClip> BytesToAudioClipAsync(
            string url, byte[] bytes, CancellationToken ct)
        {
            // byte[]를 직접 줄 수 없어서 file:// 경로 경유
            // temporaryCachePath는 Unity가 제공하는 임시 파일 저장 경로
            var tempPath = System.IO.Path.Combine(
                UnityEngine.Application.temporaryCachePath,
                System.IO.Path.GetFileName(url));

            System.IO.File.WriteAllBytes(tempPath, bytes);
            try
            {
                using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.UNKNOWN);
                await req.SendWebRequest().ToUniTask(cancellationToken: ct);
                return DownloadHandlerAudioClip.GetContent(req);
            }
            finally
            {
                // CT 취소 포함 어떤 경우에도 임시파일 삭제
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }
    }
}