using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    [CreateAssetMenu(fileName = "RangedMonsterStatsSO", menuName = "Combat/Ranged Monster Stats")]
    public class RangedMonsterStatsSO : MonsterStatsSO
    {
        // 상속 필드 중 이 클래스에서 '이름은 그대로인데 의미가 달라지는' 것들.
        // 인스펙터만 봐서는 알 수 없고 컴파일러도 안 잡아주므로 여기 적어둔다.
        //   AttackRange    → 사격 사거리 (근접 몹은 팔 닿는 거리)
        //   AttackCooldown → 발사 쿨다운
        //   AttackSkill    → Skill_RangedShoot (근접 몹은 Skill_MeleeAttack)

        [SerializeField] private float _retreatDistance = 3f; // 이 거리보다 가까워지면 후퇴
        [SerializeField] private float _projectileSpeed = 15f;

        [Header("투사체 판정")]
        // 스윕 구의 반경. 콜라이더가 아니라 SphereCast의 굵기다.
        // 판정 '위치'는 프리팹의 Tip 마커가 들고 있어서 더 이상 데이터로 관리하지 않는다.
        [SerializeField] private float _projectileSweepRadius = 0.1f;
        [SerializeField] private LayerMask _blockLayerMask; // 벽·지형 — 맞으면 데미지 없이 소멸

        public float RetreatDistance => _retreatDistance;
        public float ProjectileSpeed => _projectileSpeed;
        public float ProjectileSweepRadius => _projectileSweepRadius;
        public LayerMask BlockLayerMask => _blockLayerMask;
    }
}