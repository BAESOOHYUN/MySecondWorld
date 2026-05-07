using UnityEngine;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;
using MySecondWorld.AI.Voice;

namespace MySecondWorld.AI
{ 
    public class NPCVoicePlayer : MonoBehaviour
    {
        [SerializeField] private NPCSystemPromptSO _promptSO;
        [Inject]
        // VContainer가 ITTSService 타입 보고 → .As<ITTSService>()로 등록된 EdgeTTSService 인스턴스를 넣어줌
        private void Construct(ITTSService tts, ISubscriber<NPCResponseReceivedEvent> responseSub, ISubscriber<NPCStopTTSEvent> stopSub)
        {
            responseSub.Subscribe(evt =>
            {
                if (evt.NpcGuid != _promptSO.NpcGuid) return;
                tts.SpeakAsync(evt.Text).Forget();
            }).AddTo(destroyCancellationToken);

            stopSub.Subscribe(evt =>
            {
                if (evt.NpcGuid != _promptSO.NpcGuid) return;
                tts.Stop();
            }).AddTo(destroyCancellationToken);
        }
    }
}
