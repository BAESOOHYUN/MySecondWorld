using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace MySecondWorld.Combat
{
    // 축1(타겟팅)/축2(타이밍)/축3(효과)를 순서대로 조합해 스킬 하나를 실제로 실행하는 실행기
    public class SkillExecutor
    {
        private readonly ITargetSelector _targetSelector;
        private readonly ICastTiming _castTiming;
        private readonly IReadOnlyList<ISkillEffect> _effects;

        public SkillExecutor(ITargetSelector targetSelector, ICastTiming castTiming, IReadOnlyList<ISkillEffect> effects)
        {
            _targetSelector = targetSelector;
            _castTiming = castTiming;
            _effects = effects;
        }

        public async UniTask Execute(SkillContext ctx)
        {
            await _castTiming.WaitBeforeExecute(ctx);
            foreach (var target in _targetSelector.GetTargets(ctx))
            {
                foreach (var effect in _effects)
                    effect.Apply(target, ctx);
            }
        }
    }
}