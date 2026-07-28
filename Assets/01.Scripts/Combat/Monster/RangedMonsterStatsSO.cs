using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    [CreateAssetMenu(fileName = "RangedMonsterStatsSO", menuName = "Combat/Ranged Monster Stats")]
    public class RangedMonsterStatsSO : MonsterStatsSO
    {
        // MaxHP/Attack/AttackRange(사격 사거리로 재해석)
        // AttackCooldown(발사 쿨다운으로 재해석)
        // DetectRange/MoveSpeed는 베이스 그대로 재사용.
        [SerializeField] private float _retreatDistance = 3f; // 이 거리보다 가까워지면 후퇴
        [SerializeField] private float _projectileSpeed = 15f;

        public float RetreatDistance => _retreatDistance;
        public float ProjectileSpeed => _projectileSpeed;
    }
}