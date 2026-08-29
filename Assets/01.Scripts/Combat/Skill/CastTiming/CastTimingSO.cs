using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // 축2 구현체를 SO 에셋으로 만들기 위한 베이스. 무상태 규칙은 TargetSelectorSO 주석 참고.
    public abstract class CastTimingSO : ScriptableObject, ICastTiming
    {
        public abstract UniTask WaitBeforeExecute(SkillContext ctx);
    }
}