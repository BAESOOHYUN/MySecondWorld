using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 원거리 몹의 발사 애니메이션 이벤트 훅. PlayerAttackController와 대칭 구조.
    public class RangedMonsterHitbox : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private MonsterAI _monsterAI;
        [SerializeField] private RangedMonsterStatsSO _stats;

        [Header("원거리 판정")]
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private GameObject _projectilePrefab; // 순수 프리팹(스크립트 없음) — ProjectileFactory가 스폰 시점에 Projectile을 붙임
        [SerializeField] private float _aimHeightOffset = 1.4f; // 플레이어 피벗이 발밑이라 가슴 높이 정도로 보정

        // 스킬 실행기
        private SkillExecutor _skillExecutor;
        private ProjectileCast _cast; // OnProjectileSpawn()에서 Spawn()을 직접 호출하기 위한 구체 타입 참조

        // 런타임 상태
        private Vector3? _pendingAimPoint; // OnProjectileSpawn에서 계산한 조준점 — OnShootRelease에서 재사용(재계산 시 화살 각도 스냅 방지)


        // MonsterFactory가 AddComponent 직후(비활성 상태에서) 호출한다. Initialize와 Awake 순서 이유는 MonsterAI.Initialize 참고.
        // _projectilePrefab은 스탯 SO(RangedMonsterStatsSO.ProjectilePrefab)에서 가져온다 — 몹마다 투사체가 다를 수 있어 데이터로 관리.
        public void Initialize(Transform spawnPoint, RangedMonsterStatsSO stats)
        {
            _monsterAI = GetComponent<MonsterAI>();
            _spawnPoint = spawnPoint;
            _stats = stats;
            _projectilePrefab = stats.ProjectilePrefab;
        }

        private void Awake()
        {
            if (_monsterAI == null || _stats == null || _spawnPoint == null || _projectilePrefab == null)
            {
                Debug.LogError($"RangedMonsterHitbox 참조 누락 on {this.gameObject.name}", this);
                enabled = false;
                return;
            }

            _cast = new ProjectileCast(_projectilePrefab, _spawnPoint,
                            _stats.ProjectileSpeed, _stats.ProjectileColliderCenter, _stats.ProjectileColliderRadius,
                            LayerMask.GetMask("Player"));
            _skillExecutor = new SkillExecutor(
                new SingleTargetSelector(),
                _cast,
                new List<ISkillEffect> { new DamageEffect() });
        }

        private void OnDestroy()
        {
            _skillExecutor?.Dispose();
        }

        // 투사체를 스폰 위치에 미리 배치만 함(비주얼 노킹 단계, 아직 안 날아감).
        // Shoot 애니메이션 클립의 "손에 화살이 나타나는(노킹)" 프레임에 Animation Event로 연결.
        public void OnProjectileSpawn()
        {
            if (_skillExecutor == null) return;

            var targetPosition = _monsterAI.CurrentTargetPosition;
            if (targetPosition == null) return; // 타겟을 잃은 상태(사거리 이탈 등)면 노킹 취소
            var aimPoint = targetPosition.Value + Vector3.up * _aimHeightOffset;
            _pendingAimPoint = aimPoint;
            var direction = (aimPoint - _spawnPoint.position).normalized;

            _cast.Spawn(direction);
        }

        // 실제 발사 — 시각 브로드캐스트 + 미리 배치해둔 투사체 Launch(= 데미지 판정 시작).
        // 몬스터는 스폰된 투사체 자체가 서버 권위 판정이라, 플레이어처럼 별도 서버 확정 Command가 없다.
        // Shoot 애니메이션 클립의 "화살이 손을 떠나는" 프레임에 Animation Event로 연결.
        public void OnShootRelease()
        {
            // Animation Event(SendMessage)는 enabled=false여도 무시하고 호출됨 — Awake에서 참조 누락으로 꺼진 경우 방어
            if (_skillExecutor == null) return;

            Vector3 aimPoint;
            if (_pendingAimPoint != null)
            {
                // OnProjectileSpawn에서 이미 조준했다면 그 값을 그대로 재사용 — 발사 순간 화살이 다른 방향으로 스냅되는 것 방지.
                aimPoint = _pendingAimPoint.Value;
                _pendingAimPoint = null;
            }
            else
            {
                // 드물게 OnProjectileSpawn 이벤트가 안 걸린 클립이면 여기서 즉석으로 다시 계산
                var targetPosition = _monsterAI.CurrentTargetPosition;
                if (targetPosition == null) return; // 타겟을 잃은 상태(사거리 이탈 등)면 발사 취소
                aimPoint = targetPosition.Value + Vector3.up * _aimHeightOffset;
            }

            // 클라 시각 이펙트 브로드캐스트 — 실제 판정(SkillExecutor)과는 독립적인 별개 경로
            if (Mirror.NetworkServer.active && TryGetComponent(out MonsterProjectileVisualRelay relay))
            {
                var dir = (aimPoint - _spawnPoint.position).normalized;
                relay.RpcPlayProjectileVisual(_spawnPoint.position, dir, _stats.ProjectileSpeed);
            }

            ProjectileSpawnResolver.SpawnAndExecute(gameObject, aimPoint, _stats.Attack, _skillExecutor);
        }
    }
}