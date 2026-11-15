using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // 스킬 하나 = 축2(타겟팅) + 축1(타이밍) + 축3(효과)의 조합.
    // 새 스킬을 만들 때 코드가 늘어나지 않는다 — 에셋에서 세 축을 골라 끼우면 끝.
    [CreateAssetMenu(fileName = "Skill_", menuName = "Combat/Skill Definition")]
    public class SkillDefinition : ScriptableObject, ISkill
    {
        [SerializeField] private CastTimingSO _castTiming;         // 축1
        [SerializeField] private TargetSelectorSO _targetSelector; // 축2
        [SerializeField] private SkillEffectSO[] _effects;         // 축3
        [SerializeReference] private System.Collections.Generic.List<SkillClip> _timeline = new(); // 시퀀스: 스킬 안의 시간 배치

        // 타임라인 툴이 클립 종류·시작 시간·길이를 읽는 통로(읽기 전용).
        public System.Collections.Generic.IReadOnlyList<SkillClip> Timeline => _timeline;

        public async UniTask Execute(SkillContext ctx)
        {
            // ★ 축1이 반드시 먼저다.
            //   투사체처럼 "도착하는 순간에야 대상이 정해지는" 스킬은,
            //   타겟팅을 먼저 하면 발사 시점의 낡은 후보를 때리게 된다.
            //   즉시발동(InstantCast)은 대기가 없어 순서가 결과에 영향을 주지 않는다.
            await _castTiming.WaitBeforeExecute(ctx);

            if (_timeline.Count > 0)
                await SkillTimelinePlayer.Play(_timeline, ctx);

            foreach (var target in _targetSelector.GetTargets(ctx))
                foreach (var effect in _effects)
                    effect.Apply(target, ctx);
        }
    }
}