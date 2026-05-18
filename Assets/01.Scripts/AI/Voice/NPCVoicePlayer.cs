using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;
using MySecondWorld.AI.Voice;
using System;

namespace MySecondWorld.AI
{ 
    public class NPCVoicePlayer : MonoBehaviour
    {
        [SerializeField] private NPCSystemPromptSO _promptSO;
        [SerializeField] private AudioSource[] _sources = new AudioSource[2];
        private readonly Queue<UniTask<AudioClip>> _fetchQueue = new(); //문장 도착 즉시 fetch 시작
        private bool _isPlaying = false;
        private ITTSService _tts;
        public event Action OnSpeakStarted;
        public event Action OnSpeakFinished;

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

            // [dspTime이란?]
            // Unity 오디오 엔진 전용 타이머. 게임 시작 후 오디오 엔진이 돌아간 누적 초.
            // Time.time 은 프레임 단위(60fps = 약 16ms 오차)지만, dspTime 은 샘플 단위(0.04ms 오차).
            // PlayScheduled 는 dspTime 기준으로 재생 시각을 예약하므로 반드시 dspTime 을 써야 함.

            // [nextStartTime 초기값에 왜 0.05를 더하나?]
            // PlayScheduled 를 호출하는 지금 이 순간을 재생 시각으로 주면 오디오 엔진이 버퍼를 준비할 틈이 없음.
            // Unity 공식 권장값인 50ms(0.05초) look-ahead 를 줘서 엔진이 미리 준비하게 함.
            // nextStartTime 은 초기값만 dspTime+0.05 이고, 이후 루프에서 "이전 클립 끝 시각"으로 계속 갱신됨.
            double nextStartTime = AudioSettings.dspTime + 0.05;
            int i = 0;

            // 예외·취소 시에도 _isPlaying = false 와 OnSpeakFinished 발행을 보장
            try
            {
                while (_fetchQueue.Count > 0)
                {
                    AudioClip clip = await _fetchQueue.Dequeue();
                    if (clip == null) continue;

                    // [Math.Max — 과거 보정]
                    // fetch 가 오래 걸리면 nextStartTime 이 이미 지나버린 과거가 될 수 있음.
                    // 예) nextStartTime = 100.05 인데 fetch 중에 dspTime 이 103.00 이 됐다면
                    //   → PlayScheduled(100.05) 는 과거 시각이므로 무시되거나 즉시 재생됨(비정상)
                    // 두 값 중 더 큰 것(= 더 미래)을 골라 항상 미래 시각을 보장:
                    //   fetch 빠름  → nextStartTime 이 미래 → nextStartTime 채택 (문장 사이 무음 없음)
                    //   fetch 느림  → nextStartTime 이 과거 → dspTime+0.05 채택 (현재 기준으로 재스케줄)
                    double startTime = System.Math.Max(nextStartTime, AudioSettings.dspTime + 0.05);

                    // [waitUntil — clip 배정 타이밍 문제]
                    // PlayScheduled 는 "언제 재생할지" 예약만 함. 실제 재생 시점에 source.clip 을 읽음.
                    // 문제: 클립 12개가 거의 동시에 fetch 완료되면 루프가 빠르게 돌면서
                    //   source[0].clip 을 6번 덮어쓴 뒤 PlayScheduled(먼 미래) 를 예약함.
                    //   결국 source[0] 에는 마지막에 배정된 clip 만 남아 그것만 재생됨.
                    // 해결: 재생 시각 100ms 전까지 Delay 로 기다렸다가 clip 을 배정.
                    //   이렇게 하면 각 루프 반복이 자신의 재생 시각 직전에만 clip 을 꽂으므로 덮어쓰기 없음.
                    //
                    // waitUntil = startTime - 0.1  (재생 100ms 전 시각)
                    // waitUntil 이 현재 dspTime 보다 미래면 → 거기까지 Delay
                    //   Delay 인자 = waitUntil - 현재 dspTime = "지금부터 몇 초 뒤에 깰지"
                    // waitUntil 이 이미 지났으면 → 바로 clip 배정 (fetch 가 느렸던 경우)
                    double waitUntil = startTime - 0.1;
                    if (waitUntil > AudioSettings.dspTime)
                        await UniTask.Delay(
                            TimeSpan.FromSeconds(waitUntil - AudioSettings.dspTime),
                            cancellationToken: ct);

                    _sources[i % 2].clip = clip;
                    _sources[i % 2].PlayScheduled(startTime); // 예약만 함. Play() 별도 호출 불필요(오히려 예약 취소됨)
                    Debug.Log($"[VoicePlayer] PlayScheduled source={i % 2}, startTime={startTime}");

                    if (i == 0) OnSpeakStarted?.Invoke(); // 첫 문장 예약 시 발행
                    nextStartTime = startTime + clip.length; // 다음 문장 시작 시각 = 이 문장이 끝나는 시각 //위에서 말한 "이전 클립 끝 시각"으로 계속 갱신
                    i++;
                }

                // [루프 종료 후 마지막 클립 재생 완료 대기]
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
                    OnSpeakFinished?.Invoke();
            }
        }
    }
}
