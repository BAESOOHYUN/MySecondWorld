using UnityEngine;

namespace MySecondWorld.Combat.Player
{
    // 플레이어 공격 판정 튜닝 수치. PlayerAttackController(클라 입력/예측)만 참조하고,
    // PlayerAttackAuthority(서버)는 이 값들을 직접 읽지 않고 Command 파라미터로 전달받은 값만 쓰므로
    // 두 컴포넌트가 같은 SO를 참조해야 한다는 정합성 제약은 없다.
    [CreateAssetMenu(fileName = "PlayerCombatStatsSO", menuName = "Combat/Player Combat Stats")]
    public class PlayerCombatStatsSO : ScriptableObject
    {
        [Header("근접 판정")]
        [SerializeField] private float _attackRadius = 1.5f;
        [SerializeField] private float _unarmedAttack = 10f; // 무기 미장착(맨주먹) 시 기본 공격력
        [SerializeField] private float _attackCooldown = 0.8f;

        [Header("원거리 판정 (투사체 — Bow/Staff 등 공용)")]
        [SerializeField] private float _projectileSpeed = 20f;
        [SerializeField] private Vector3 _projectileColliderCenter;
        [SerializeField] private float _projectileColliderRadius = 0.15f;
        [SerializeField] private float _shootRange = 15f;
        [SerializeField] private float _aimHeightOffset = 1.4f;

        [Header("전투 상태")]
        [SerializeField] private float _combatStateDuration = 3f; // 마지막 공격 후 이 시간 동안 "전투 중"으로 간주

        public float AttackRadius => _attackRadius;
        public float UnarmedAttack => _unarmedAttack;
        public float AttackCooldown => _attackCooldown;
        public float ProjectileSpeed => _projectileSpeed;
        public Vector3 ProjectileColliderCenter => _projectileColliderCenter;
        public float ProjectileColliderRadius => _projectileColliderRadius;
        public float ShootRange => _shootRange;
        public float AimHeightOffset => _aimHeightOffset;
        public float CombatStateDuration => _combatStateDuration;
    }
}