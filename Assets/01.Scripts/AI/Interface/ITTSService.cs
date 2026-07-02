using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.AI.Voice
{
    public interface ITTSService
    {
        // 문장1 재생 중에 문장2 다운로드를 병렬로 돌려, 문장1이 끝나면 문장2 clip이 이미 준비돼 있어서 공백 없이 바로 이어지게 함
        public UniTask<AudioClip> FetchAsync(string text, CancellationToken ct = default); // Edge-TTS 서버에 요청 → MP3 받기 → AudioClip 만들기. 재생 없음.
    }
}