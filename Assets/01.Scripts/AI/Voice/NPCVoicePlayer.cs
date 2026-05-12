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
        private readonly Queue<string> _sentenceQueue = new();
        private bool _isPlaying = false;
        private ITTSService _tts;

        [Inject]
        // VContainer가 ITTSService 타입 보고 → .As<ITTSService>()로 등록된 EdgeTTSService 인스턴스를 넣어줌
        private void Construct(ITTSService tts, ISubscriber<NPCTTSSentenceEvent> ttsSub, ISubscriber<NPCStopTTSEvent> stopSub)
        {
            _tts = tts;

            // 이벤트 올 때마다 큐에 Enqueue한 뒤 재생 중이 아니면 큐를 드레인하는 방식
            ttsSub.Subscribe(evt =>
            {
                if (evt.NpcGuid != _promptSO.NpcGuid) return;
                _sentenceQueue.Enqueue(evt.Text);
                if (!_isPlaying)
                {
                   PlayQueue(destroyCancellationToken).Forget();
                }

            }).AddTo(destroyCancellationToken);

            stopSub.Subscribe(evt =>
            {
                if (evt.NpcGuid != _promptSO.NpcGuid) return;
                tts.Stop();
            }).AddTo(destroyCancellationToken);
        }

        private async UniTaskVoid PlayQueue(CancellationToken ct)
        {
            _isPlaying = true;
            while (_sentenceQueue.Count > 0)
            {
                string sentence = _sentenceQueue.Dequeue();
               await _tts.SpeakAsync(sentence, ct);
            }
            _isPlaying = false;
        }
    }
}
