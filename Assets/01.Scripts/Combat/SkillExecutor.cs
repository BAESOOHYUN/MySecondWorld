using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace MySecondWorld.Combat
{
    // 축1(타겟팅)/축2(타이밍)/축3(효과)를 순서대로 조합해 스킬 하나를 실제로 실행하는 실행기
    public class SkillExecutor : IDisposable
    {
        private readonly ITargetSelector _targetSelector;
        private readonly ICastTiming _castTiming;
        private readonly IReadOnlyList<ISkillEffect> _effects;

        public SkillExecutor(
            ITargetSelector targetSelector,
            ICastTiming castTiming,
            IReadOnlyList<ISkillEffect> effects)
        {
            _targetSelector = targetSelector;
            _castTiming = castTiming;
            _effects = effects;
        }

        // 캐스트 타이밍이 확정한 목록을 타겟 셀렉터에 그대로 넘기는 배선
        public async UniTask Execute(SkillContext ctx)
        {
            // 대기 후 "실제로 확정된 대상"을 받아온 뒤, 그 목록을 기준으로 타겟팅한다
            // (캐스트 시작 시점과 명중 시점 사이의 시간차로 인한 오판정 방지)
            var resolved = await _castTiming.ResolveTargets(ctx);
            foreach (var target in _targetSelector.GetTargets(ctx, resolved))
            {
                foreach (var effect in _effects)
                    effect.Apply(target, ctx);
            }
        }

        public void Dispose()
        {
            _castTiming.Dispose();
        }
    }
}