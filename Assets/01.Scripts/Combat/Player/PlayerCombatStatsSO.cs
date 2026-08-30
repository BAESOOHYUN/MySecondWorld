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
        // 스윕 구의 반경. 콜라이더가 아니라 SphereCast의 굵기다.
        // 판정 '위치'는 프리팹의 Tip 마커가 들고 있어 더 이상 데이터로 관리하지 않는다.
        [SerializeField] private float _projectileSweepRadius = 0.15f;
        [SerializeField] private LayerMask _blockLayerMask; // 벽·지형 — 맞으면 데미지 없이 소멸
        [SerializeField] private float _shootRange = 15f;
        [SerializeField] private float _aimHeightOffset = 1.4f;

        [Header("전투 상태")]
        [SerializeField] private float _combatStateDuration = 3f; // 마지막 공격 후 이 시간 동안 "전투 중"으로 간주
        // 데드락 방지 타임아웃.공격을 시작하고 이 시간 안에 발사(타격) 이벤트가 안 오면 그 회차를 강제 종료한다.
        // 애니메이션이 끊겨 이벤트가 씹혔을 때 공격이 영구히 잠기는 걸 막는 안전장치.
        [SerializeField] private float _attackTimeout = 1.5f;

        public float AttackRadius => _attackRadius;
        public float UnarmedAttack => _unarmedAttack;
        public float AttackCooldown => _attackCooldown;
        public float ProjectileSpeed => _projectileSpeed;
        public float ProjectileSweepRadius => _projectileSweepRadius;
        public LayerMask BlockLayerMask => _blockLayerMask;
        public float ShootRange => _shootRange;
        public float AimHeightOffset => _aimHeightOffset;
        public float CombatStateDuration => _combatStateDuration;
        public float AttackTimeout => _attackTimeout;
    }
}