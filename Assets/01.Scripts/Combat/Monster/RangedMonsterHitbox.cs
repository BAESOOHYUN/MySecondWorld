using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 원거리 몹의 발사 애니메이션 이벤트 훅.
    // 이 컴포넌트는 서버에만 존재한다(MonsterFactory가 서버 스폰 경로에서 AddComponent).
    public class RangedMonsterHitbox : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private MonsterAI _monsterAI;
        [SerializeField] private RangedMonsterStatsSO _stats;
        [SerializeField] private NetworkProjectileRelay _relay;

        [Header("원거리 판정")]
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private float _aimHeightOffset = 1.4f; // 플레이어 피벗이 발밑이라 가슴 높이로 보정

        private void Awake()
        {
            if (_monsterAI == null || _stats == null || _spawnPoint == null)
            {
                Debug.LogError($"RangedMonsterHitbox 참조 누락(_monsterAI / _stats / _spawnPoint) on {name}", this);
                enabled = false;
                return;
            }

            if (_relay == null || _relay.ProjectilePrefab == null)
            {
                Debug.LogError($"RangedMonsterHitbox: NetworkProjectileRelay 또는 투사체 프리팹 미할당 on {name}", this);
                enabled = false;
                return;
            }

            if (_stats.AttackSkill == null)
            {
                Debug.LogError($"RangedMonsterHitbox: {_stats.name}에 AttackSkill 미할당", _stats);
                enabled = false;
                return;
            }
        }

        // MonsterFactory가 AddComponent 직후(비활성 상태에서) 호출한다. Initialize와 Awake 순서 이유는 MonsterAI.Initialize 참고.
        // 투사체 프리팹은 NetworkProjectileRelay가 소유한다
        // — 클라이언트 사본에도 존재해야 하는데 클라는 스탯 SO를 안 거치고 프리팹에 authoring된 것만 갖기 때문.
        public void Initialize(Transform spawnPoint, RangedMonsterStatsSO stats)
        {
            _monsterAI = GetComponent<MonsterAI>();
            _spawnPoint = spawnPoint;
            _stats = stats;
            _relay = GetComponent<NetworkProjectileRelay>(); // 프리팹에 미리 붙어있어야 한다
        }

        // 같은 클립의 Draw 프레임 이벤트. 몬스터는 손에 화살을 들지 않고
        // 발사 순간 풀에서 대여하므로 할 일이 없다.
        // "receiver 없음" 경고만 막기 위한 빈 훅이다.
        public void OnAttackNock() { }

        // Shoot 애니메이션 클립의 발사 프레임에 Animation Event로 연결
        public void OnAttackRelease()
        {
            // Animation Event(SendMessage)는 enabled=false여도 호출된다 — Awake에서 꺼진 경우 방어
            if (_relay == null || _stats == null || _stats.AttackSkill == null) return;

            var targetPosition = _monsterAI.CurrentTargetPosition;
            if (targetPosition == null) return; // 타겟을 잃음(사거리 이탈 등) — 발사 취소

            var origin = _spawnPoint.position;
            var aimPoint = targetPosition.Value + Vector3.up * _aimHeightOffset;
            var direction = (aimPoint - origin).normalized;

            // 빗나갔을 때 조준 지점 근처에서 사라지도록 비행 시간을 거리에서 뽑는다.
            // 고정값(3초 등)을 쓰면 빗나간 화살이 수십 미터를 더 날아간다.
            float distance = Vector3.Distance(origin, aimPoint);
            float lifetime = distance / Mathf.Max(_stats.ProjectileSpeed, 0.01f) + 0.3f;

            // 클라 표현 브로드캐스트 — 판정과 독립된 경로다
            _relay.RpcLaunchPresentation(origin, direction, _stats.ProjectileSpeed, lifetime);

            var request = new ProjectileRequest
            {
                Prefab = _relay.ProjectilePrefab,
                NockSource = null, // 몬스터는 손 화살 개념이 없다 → 런처가 풀에서 대여한다
                Origin = origin,
                Direction = direction,
                Speed = _stats.ProjectileSpeed,
                SweepRadius = _stats.ProjectileSweepRadius,
                MaxLifetime = lifetime,
                HitMask = LayerMask.GetMask("Player", "NPC"),
                BlockMask = _stats.BlockLayerMask,
                // 명중 순간 클라에 알려 표현 화살을 같은 지점에서 정리한다.
                // 몬스터가 이미 죽어 디스폰됐으면 Rpc가 예외를 내므로 null 검사가 필요하다.
                OnResolved = point =>
                {
                    if (_relay != null && _relay.netIdentity != null)
                        _relay.RpcConfirmHit(point);
                },
            };

            // 후보는 비워서 보낸다 — 누구를 맞혔는지는 축2가 화살이 도착한 뒤에 채운다.
            var ctx = new SkillContext(gameObject, Array.Empty<IDamageable>(), _stats.Attack, request);
            _stats.AttackSkill.Execute(ctx).Forget();
        }
    }
}