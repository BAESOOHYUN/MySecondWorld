using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;
using MySecondWorld.AI.Voice;

namespace MySecondWorld.AI
{ 
    public class NPCVoicePlayer : MonoBehaviour
    {
        [SerializeField] private NPCSystemPromptSO _promptSO;
        private readonly Queue<UniTask<AudioClip>> _fetchQueue = new(); //문장 도착 즉시 fetch 시작
        private bool _isPlaying = false;
        private ITTSService _tts;

        [Inject]
        // NPCScope VContainer가 ITTSService 타입 보고 → .As<ITTSService>()로 등록된 EdgeTTSService 인스턴스를 넣어줌
        private void Construct(ITTSService tts, ISubscriber<NPCTTSSentenceEvent> ttsSub, ISubscriber<NPCStopTTSEvent> stopSub)
        {
            _tts = tts;

            // 이벤트 올 때마다 큐에 Enqueue한 뒤 재생 중이 아니면 큐를 드레인하는 방식
            ttsSub.Subscribe(evt =>
            {
                if (evt.NpcGuid != _promptSO.NpcGuid) return;
                _fetchQueue.Enqueue(_tts.FetchAsync(evt.Text, destroyCancellationToken));
                if (!_isPlaying)
                   PlayQueue(destroyCancellationToken).Forget();
            }).AddTo(destroyCancellationToken);

            stopSub.Subscribe(evt =>
            {
                if (evt.NpcGuid != _promptSO.NpcGuid) return;
                _fetchQueue.Clear(); //Stop 시 쌓인 문장도 비움
                tts.Stop();
            }).AddTo(destroyCancellationToken);
        }

        private async UniTaskVoid PlayQueue(CancellationToken ct)
        {
            _isPlaying = true;

            // 예외나 취소 시에도 _isPlaying = false 보장
            try
            {
                while (_fetchQueue.Count > 0)
                {
                    AudioClip clip = await _fetchQueue.Dequeue();
                    if (clip != null)
                        await _tts.PlayAsync(clip, ct);
                }
            }
            finally
            {

                _isPlaying = false;
            }
        }
    }
}
