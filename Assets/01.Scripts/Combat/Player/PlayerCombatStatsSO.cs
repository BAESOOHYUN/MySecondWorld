using UnityEngine;

namespace MySecondWorld.Combat.Player
{
    // 플레이어 공격 튜닝 수치.
    //
    // 클라(PlayerAttackController)와 서버(PlayerAttackAuthority)가 함께 읽는다.
    // Phase 5에서 서버가 투사체 수치를 직접 읽게 됐다 — 클라가 보낸 값을 믿으면 조작 여지가 생긴다.
    // 따라서 두 컴포넌트는 반드시 같은 에셋을 참조해야 한다.
    // 특히 서버 연사 제한이 AttackCooldown 기준이라, 어긋나면 정상 플레이가 서버에서 막힌다.
    //
    // 클라 전용 값: MeleeFacingRange / AimHeightOffset / CombatStateDuration / AttackTimeout
    [CreateAssetMenu(fileName = "PlayerCombatStatsSO", menuName = "Combat/Player Combat Stats")]
    public class PlayerCombatStatsSO : ScriptableObject
    {
        #region Inspector

        [Header("근접 판정")]
        [SerializeField] private float _attackRadius = 1.5f; // 공격 반경

        [Tooltip("근접 공격 시 이 반경 안의 가장 가까운 대상 쪽으로 몸을 돌린다.\n" +
          "공격 반경(AttackRadius)보다 조금 넉넉해야 '살짝 빗나간 위치의 적'에게도 돌아선다.")]
        [SerializeField] private float _meleeFacingRange = 3f; // 회전 반경은 공격 반경보다 넉넉하게
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

        [Header("원거리 조준 게이지 (홀드 충전 — 활 전용)")]
        [Tooltip("홀드 시작부터 게이지 100%까지 걸리는 시간(초).")]
        [SerializeField] private float _aimChargeDuration = 1.2f;

        [Tooltip("게이지 100%일 때 화살이 초당 최대 몇 도(deg)까지 방향을 꺾어 대상을 쫓는가.")]
        [SerializeField] private float _homingMaxTurnRate = 180f;

        [Tooltip("게이지 진행도를 유도 강도로 바꾸는 지수. 1=선형, 클수록 후반에 급격히 강해진다.")]
        [SerializeField] private float _homingCurveExponent = 2.5f;

        [Tooltip("게이지 100%일 때 데미지 배수.")]
        [SerializeField] private float _chargeDamageMultiplierMax = 1.5f;

        [Tooltip("조준 중 이동할 때 초당 게이지 감소량(0~1 비율 기준). 1이면 1초 만에 완전히 빠진다.")]
        [SerializeField] private float _aimGaugeDrainRate = 1f;

        [Header("전투 상태")]
        [SerializeField] private float _combatStateDuration = 3f; // 마지막 공격 후 이 시간 동안 "전투 중"으로 간주
        // 데드락 방지 타임아웃 공격을 시작하고 이 시간 안에 발사(타격) 이벤트가 안 오면 그 회차를 강제 종료한다.
        // 애니메이션이 끊겨 이벤트가 씹혔을 때 공격이 영구히 잠기는 걸 막는 안전장치.
        [SerializeField] private float _attackTimeout = 1.5f;

        #endregion

        #region Property
        public float AttackRadius => _attackRadius;
        public float MeleeFacingRange => _meleeFacingRange;
        public float UnarmedAttack => _unarmedAttack;
        public float AttackCooldown => _attackCooldown;
        public float ProjectileSpeed => _projectileSpeed;
        public float ProjectileSweepRadius => _projectileSweepRadius;
        public LayerMask BlockLayerMask => _blockLayerMask;
        public float ShootRange => _shootRange;
        public float AimHeightOffset => _aimHeightOffset;
        public float CombatStateDuration => _combatStateDuration;
        public float AttackTimeout => _attackTimeout;

        public float AimChargeDuration => _aimChargeDuration;
        public float HomingMaxTurnRate => _homingMaxTurnRate;
        public float HomingCurveExponent => _homingCurveExponent;
        public float ChargeDamageMultiplierMax => _chargeDamageMultiplierMax;
        public float AimGaugeDrainRate => _aimGaugeDrainRate;

        #endregion
    }
}