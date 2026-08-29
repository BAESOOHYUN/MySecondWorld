using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // 축1 구현체를 SO 에셋으로 만들기 위한 베이스.
    //
    // ★ 무상태 규칙: 인스턴스 필드를 두지 않는다.
    //   SO는 프로젝트 전역 단일 인스턴스라 모든 캐스터가 같은 객체를 공유한다.
    //   필드에 실행 중 값을 쓰면 다른 캐스터의 실행을 덮어쓰고,
    //   에디터 플레이 모드에서 쓴 값은 플레이를 멈춰도 에셋에 남는다.
    //   캐스터마다 다른 값은 전부 SkillContext로 전달한다.
    public abstract class TargetSelectorSO : ScriptableObject, ITargetSelector
    {
        public abstract IEnumerable<IDamageable> GetTargets(SkillContext ctx);
    }
}