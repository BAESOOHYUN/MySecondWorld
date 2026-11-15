using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace MySecondWorld.Combat
{
    public static class SkillTimelinePlayer
    {
        // 클립 목록을 시간순으로 훑으면서, 각 클립의 시각이 될 때까지 기다렸다가 실행
        public static async UniTask Play(IReadOnlyList<SkillClip> clips, SkillContext ctx)
        {
            var sorted = new List<SkillClip>(clips);
            sorted.RemoveAll(c => c == null);
            sorted.Sort((a,b) => a.StartTime.CompareTo(b.StartTime));

            // elapsed(경과시간)로 "지난 시간"을 직접 추적하는 이유는
            // 클립마다 처음부터 기다리면 시간이 누적돼서 밀리기 때문
            float elapsed = 0f;
            foreach (var clip in sorted)
            {
                float wait = clip.StartTime - elapsed;
                if (wait > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(wait));
                    elapsed = clip.StartTime;
                }
                clip.Execute(ctx);
            }
        }
    }
}

// StartTime
//모션 시작: 0.0초
//폭발 이펙트: 0.4초
//데미지: 0.4초

// "다음 클립 시각까지 남은 시간만큼 자고, 깨어나면 실행"

//시간축(초)   0.0 ─────────────── 0.4 ───────────────
//정렬된 clips[모션]              [이펙트][데미지]
//                │                  │        │
//elapsed       0.0                   0.4      0.4
//wait          0.0 - 0.0 = 0         0.4-0.0  0.4-0.4
//              = 0                   = 0.4초  = 0
//동작          바로 실행 ▶         0.4초 잠듦 → 실행 ▶ → (잠 안 자고) 바로 실행 ▶