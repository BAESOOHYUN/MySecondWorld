using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;
using System;

namespace MySecondWorld.AI.Voice
{ 
    public class NPCVoicePlayer : MonoBehaviour
    {
        [SerializeField] private NPCSystemPromptSO _promptSO;
        [SerializeField] private AudioSource[] _sources = new AudioSource[2];
        public AudioSource[] Sources => _sources;
        private readonly Queue<(string text, UniTask<AudioClip> fetch)> _fetchQueue = new(); //문장 도착 즉시 fetch 시작
        private bool _isPlaying = false;
        public bool IsPlaying => _isPlaying;
        public event Action OnSpeakStarted;
        public event Action OnSpeakFinished;
        private ITTSService _tts;
        private IPublisher<NPCSpeakFinishedEvent> _speakFinishedPub;
        private IPublisher<NPCSentenceStartedEvent> _sentenceStartedPub;

       [Inject]
        // NPCScope VContainer가 ITTSService 타입 보고 → .As<ITTSService>()로 등록된 EdgeTTSService 인스턴스를 넣어줌
        private void Construct(ITTSService tts, 
            ISubscriber<NPCTTSSentenceEvent> ttsSub, 
            ISubscriber<NPCStopTTSEvent> stopSub,
            IPublisher<NPCSpeakFinishedEvent> speakFinishedPub,
            IPublisher<NPCSentenceStartedEvent> sentenceStartedPub
            )
        {
            _tts = tts;
            _speakFinishedPub = speakFinishedPub;
            _sentenceStartedPub = sentenceStartedPub;

            // 이벤트 올 때마다 큐에 Enqueue한 뒤 재생 중이 아니면 큐를 드레인하는 방식
            ttsSub.Subscribe(evt =>
            {
                if (evt.NpcGuid != _promptSO.NpcGuid) return;
                _fetchQueue.Enqueue((evt.Text, _tts.FetchAsync(evt.Text, destroyCancellationToken)));
                Debug.Log($"[VoicePlayer] Enqueue. isPlaying={_isPlaying}");
                if (!_isPlaying)
                   PlayQueue(destroyCancellationToken).Forget();
            }).AddTo(destroyCancellationToken);

            stopSub.Subscribe(evt =>
            {
                if (evt.NpcGuid != _promptSO.NpcGuid) return;
                _fetchQueue.Clear(); //Stop 시 쌓인 문장도 비움
                _sources[0].Stop();
                _sources[1].Stop();
            }).AddTo(destroyCancellationToken);
        }

        private async UniTaskVoid PlayQueue(CancellationToken ct)
        {
            _isPlaying = true;

            // dspTime : Unity 오디오 엔진 전용 타이머. 게임 시작 후 오디오 엔진이 돌아간 누적 초.
            // + 0.05 : 오디오 엔진이 버퍼를 준비할 틈 50ms(0.05초) look-ahead 를 줘서 glitch 방지
            // nextStartTime 은 초기값만 dspTime+0.05 이고, 이후 루프에서 "이전 클립 끝 시각"으로 계속 갱신됨.
            double nextStartTime = AudioSettings.dspTime + 0.05;
            int i = 0;

            // 예외·취소 시에도 _isPlaying = false 와 OnSpeakFinished 발행을 보장
            try
            {
                while (_fetchQueue.Count > 0)
                {
                    var (text, fetchTask) =  _fetchQueue.Dequeue();
                    AudioClip clip = await fetchTask;

                    if (clip == null)
                    {
                        // TTS 서버 꺼진 경우 — 버블은 즉시 표시
                        _sentenceStartedPub.Publish(new NPCSentenceStartedEvent(_promptSO.NpcGuid, text));
                        continue;
                    }

                    // Math.Max — 과거 보정. startTime은 항상 미래의 시각을 보장
                    //   fetch 빠름  → nextStartTime 이 미래 → nextStartTime 채택 (문장 사이 무음 없음)
                    //   fetch 느림  → nextStartTime 이 과거 → dspTime+0.05 채택 (현재 기준으로 재스케줄)
                    double startTime = System.Math.Max(nextStartTime, AudioSettings.dspTime + 0.05);

                    // waitUntil = startTime - 0.1  (재생 100ms 전 시각)
                    // 자신의 재생 직전(-0.1)에만 clip을 꽂으므로 오디오 덮어쓰기 방지
                    double waitUntil = startTime - 0.1;
                    if (waitUntil > AudioSettings.dspTime)
                    {
                        await UniTask.Delay(
                            TimeSpan.FromSeconds(waitUntil - AudioSettings.dspTime),
                            cancellationToken: ct);
                    }

                    _sources[i % 2].clip = clip;

                    // await 이후 startTime이 과거가 됐을 경우 [재보정
                    // PlayScheduled에 과거 시각 전달 시 즉시 재생되어 이전 클립과 겹칠 수 있음
                    startTime = System.Math.Max(startTime, AudioSettings.dspTime + 0.05);
                    // 예약만 함. Play() 별도 호출 불필요(Play()시 오히려 예약 취소됨)
                    _sources[i % 2].PlayScheduled(startTime);
                    Debug.Log($"[VoicePlayer] PlayScheduled source={i % 2}, startTime={startTime}");
                    
                    // 재생 예약 직후 버블 표시 (실제 재생과 ~100ms 차이, 체감 불가)
                    _sentenceStartedPub.Publish(new NPCSentenceStartedEvent(_promptSO.NpcGuid, text));

                    if (i == 0) 
                        OnSpeakStarted?.Invoke(); // 첫 문장 예약 시 발행

                    // 다음 문장 시작 시각 = 이 문장이 끝나는 시각 
                    // 위에서 말한 "이전 클립 끝 시각"으로 계속 갱신
                    nextStartTime = startTime + clip.length; 
                    i++;
                }

                // 루프가 끝났어도 마지막 clip 은 아직 재생 중일 수 있음.
                // remaining = 마지막 clip 끝 시각 - 지금. 아직 남았으면 대기 후 OnSpeakFinished.
                double remaining = nextStartTime - AudioSettings.dspTime;
                if (remaining > 0)
                    await UniTask.Delay(TimeSpan.FromSeconds(remaining), cancellationToken: ct);
            }
            finally
            {
                _isPlaying = false;
                // remaining 대기 중 도착한 문장이 있으면 이어서 재생
                if (_fetchQueue.Count > 0)
                    PlayQueue(destroyCancellationToken).Forget();
                else
                { 
                    OnSpeakFinished?.Invoke();
                    _speakFinishedPub.Publish(new NPCSpeakFinishedEvent(_promptSO.NpcGuid));
                }
            }
        }
    }
}
