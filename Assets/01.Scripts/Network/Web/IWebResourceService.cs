using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Network.Web
{
    public interface IWebResourceService
    {
        // 이미지 URL → Texture2D (L1메모리 → L2디스크 → L3네트워크 순서로 조회)
        UniTask<IResourceHandle<Texture2D>> AcquireTextureAsync(
            string url, IProgress<float> progress = null, CancellationToken ct = default);

        // 오디오 URL → AudioClip
        UniTask<IResourceHandle<AudioClip>> AcquireAudioClipAsync(
            string url, IProgress<float> progress = null, CancellationToken ct = default);

        // 영상은 풀링 대상 아님 — VideoPlayer.url에 직접 세팅용
        UniTask<string> GetVideoUrlAsync(string url, CancellationToken ct = default);
    }
}
