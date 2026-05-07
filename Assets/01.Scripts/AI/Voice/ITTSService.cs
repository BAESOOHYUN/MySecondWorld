using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MySecondWorld.AI.Voice
{
    public interface ITTSService
    {
        public event Action OnSpeakStarted;     // 재생 시작 알림
        public event Action OnSpeakFinished;    // 재생 완료 알림
        public UniTask SpeakAsync(string text, CancellationToken ct = default); // 비동기 음성 재생
        public void Stop(); // 재생 중지
    }
}