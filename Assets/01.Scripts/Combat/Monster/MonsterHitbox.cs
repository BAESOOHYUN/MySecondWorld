using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 몬스터 근접 공격의 애니메이션 이벤트 훅.
    // 플레이어 쪽 Health/IDamageable이 아직 없어서(PLAN.md 제외 범위) 지금은 실제 데미지 판정을 하지 않는다.
    // 플레이어 피격 시스템이 추가되면 PlayerAttackController.OnAttackHit()과 대칭으로
    // Physics.OverlapSphere + SkillExecutor.Execute() 호출을 여기에 채운다.
    public class MonsterHitbox : MonoBehaviour
    {
        // Attack 애니메이션 클립의 타격 프레임에 Animation Event로 연결
        public void OnAttackHit()
        {
            // TODO: 플레이어 Health 추가 후 구현
        }
    }
}