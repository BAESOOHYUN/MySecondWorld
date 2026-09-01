using Cysharp.Threading.Tasks;
using UnityEngine;
using Mirror;
using MySecondWorld.Network;
using MySecondWorld.Inventory;

namespace MySecondWorld.Combat.Player
{
    // 서버 권위 공격 판정 전용 NetworkBehaviour.
    // NetworkWeaponEquip(Network 어셈블리)에 두면 Network→Combat 참조가 생겨
    // 기존 Combat→Network 참조와 순환 참조가 되므로, Combat 어셈블리 안에 별도로 둔다.
    public class PlayerAttackAuthority : NetworkBehaviour
    {
        [Header("스킬")]
        [Tooltip("근접 공격 스킬. 보통 Skill_MeleeAttack.")]
        [SerializeField] private SkillDefinition _meleeSkill;

        [Tooltip("원거리 공격 스킬. 보통 Skill_RangedShoot (축2가 ProjectileCastSO).")]
        [SerializeField] private SkillDefinition _rangedSkill;

        [Header("수치")]
        [Tooltip("서버 판정용 수치. 클라가 보낸 값을 믿지 않으려면 서버가 직접 읽어야 한다.\n" +
                 "PlayerAttackController와 같은 에셋을 써도 되지만, 일치해야 하는 제약은 없다.")]
        [SerializeField] private PlayerCombatStatsSO _stats;

        // 캐싱
        private NetworkWeaponEquip _weaponEquip;
        private ArrowNockPoint _nockPoint;             // 호스트에서 손 화살을 그대로 쏘기 위해 필요
        private NetworkProjectileRelay _relay;         // 투사체 프리팹 소유 + 명중 Rpc

        // 동기화
        // 원격 피어가 자기 발사 프레임에 읽는 조준 방향.
        // 발사 프레임이 아니라 '공격 입력 시점'에 채워지므로 애니메이션 길이만큼 도착 여유가 있다.
        [SyncVar] private Vector3 _aimDirection;
        public Vector3 AimDirection => _aimDirection;

        // 서버 전용 상태
        private float _serverNextShootTime; // 서버측 연사 제한. 클라 게이트를 믿지 않고 따로 잰다

        // 조준 게이지 — 서버가 직접 시간을 재서 만든다. 클라가 보낸 값을 그대로 믿으면
        // "언제나 100%를 보낸다"는 조작이 가능해진다.
        private bool _isAiming;
        private float _aimGaugeServer;      // 0~1. CmdBeginAim ~ CmdEndAim/CmdRequestShoot 사이에만 갱신
        private uint _aimTargetNetId;
        private Vector3 _aimLastPosition;   // 이동 감지용 — 서버가 직접 관측한 이전 프레임 위치

        // 검증 상수
        private const float MaxAttackReach = 4f; // 공격 반경(1.5) + 이동 오차 여유

        // 핵심은 - 1을 "아직 계산 안 함" 표시로 쓴다는 거. -1이 아니면 "이미 계산해서 저장해둔 값(_monsterLayerMask)"
        // LayerMask.GetMask가 실제로 돌려주는 값은 항상 0 이상이라, -1은 절대 나올 수 없는 값. -1이면 "아직 캐싱 안 됨(LayerMask.GetMask("Monster")로 캐싱)"
        private static int _monsterLayerMask = -1;
        private static int MonsterLayerMask =>
             _monsterLayerMask != -1                                                    
            ? _monsterLayerMask : (_monsterLayerMask = LayerMask.GetMask("Monster")); 

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMonsterLayerMaskCache() => _monsterLayerMask = -1;


        private void Awake()
        {
            _weaponEquip = GetComponent<NetworkWeaponEquip>();
            _nockPoint = GetComponent<ArrowNockPoint>();
            _relay = GetComponent<NetworkProjectileRelay>();

            if (_meleeSkill == null)
                Debug.LogError($"PlayerAttackAuthority: _meleeSkill 미할당 on {gameObject.name}", this);
        }

        // 조준 게이지:
        // ④ 이동 중엔 서서히 감소, 멈춰 있으면 서서히 충전
        // — 서버가 직접 관측한 위치 변화로 판단한다. 클라가 "나 안 움직였어요"라고 보낸 값을 믿지 않는다.
        private void Update()
        {
            if (!isServer || !_isAiming || _stats == null) return;

            float moved = Vector3.Distance(transform.position, _aimLastPosition);
            _aimLastPosition = transform.position;

            // 포탈 전환 등으로 한 프레임에 크게 튄 경우는 "이동"으로 치지 않는다 —
            // 그대로 두면 텔레포트하는 순간 게이지가 통째로 날아간다.
            if (moved > 5f) return;

            _aimGaugeServer = moved > 0.01f
                ? Mathf.Clamp01(_aimGaugeServer - _stats.AimGaugeDrainRate * Time.deltaTime)
                : Mathf.Clamp01(_aimGaugeServer + Time.deltaTime / _stats.AimChargeDuration);
        }

        // PlayerAttackController.OnAttackHit()이 위임하는 서버 권위 판정.
        // 몬스터의 Health/몸통 Collider는 서버 인스턴스에만 존재하므로(Mirror 몬스터 풀링 설계),
        // 원격 클라이언트가 로컬에서 직접 OverlapSphere를 해봐야 대상 자체를 못 찾는다 — 반드시 서버에서 판정해야 함.
        //
        // [Command]는 클라가 호출하지만 서버에서 실행된다 — 파라미터를 검증 없이 그대로 믿으면 조작 여지가 생기므로,
        // 값마다 처리 방식을 다르게 뒀다:
        //   - power       → 클라 입력 자체를 무시, 서버가 NetworkWeaponEquip에서 직접 조회
        //   - layerMask   → 클라 입력을 아예 안 받음, 서버 상수(MonsterLayerMask)로 고정
        //   - attackPoint → 공격 지점 자체는 클라만 아는 정보라 완전히 없앨 수 없어, 대신 자기 위치와의 거리로 검증
        [Command]
        public void CmdResolveAttackHit(Vector3 attackPointPosition, float attackRadius, float unarmedAttack, uint preferredNetId)
        {
            if (_meleeSkill == null) return; // 인스펙터 미할당 방어 — Command에서 예외가 나면 Mirror가 연결을 끊는다
            // 클라가 보낸 공격 위치가 클라 자신의 캐릭터 근처인지 검증 — 너무 멀면 조작 의심, 무시
            if (Vector3.Distance(transform.position, attackPointPosition) > MaxAttackReach) return;

            // 공격력은 클라 파라미터가 아니라 서버가 들고 있는 실제 장착 무기값에서 조회
            float power = _weaponEquip.GetMainHandAttack();
            if (power <= 0f) power = unarmedAttack;
            Debug.Log($"[Damage] 공격력 계산 — power={power} (무기 미보유 시 unarmedAttack={unarmedAttack})");

            // 클라가 보낸 netId는 "누굴 노렸나"일 뿐이다. 그 대상이 실제로 공격 구체 안에 있는지는
            // OverlapSphere가 판단하므로, 이 값으로 먼 대상을 때리는 조작은 불가능하다.
            Transform preferred = null;
            if (preferredNetId != 0 && NetworkServer.spawned.TryGetValue(preferredNetId, out var identity))
                preferred = identity.transform;

            SphereAttackResolver.ResolveAndExecute(
                this.gameObject, attackPointPosition, attackRadius, MonsterLayerMask, power, _meleeSkill, preferred);
        }

        // 공격 입력 시점에 호출된다. 발사보다 먼저 도착해야 원격 피어가 제 방향으로 쏜다.
        [Command]
        public void CmdSetAimDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            _aimDirection = direction.normalized;
        }

        // 홀드 시작 시 호출된다. 게이지는 여기서부터 서버 시간으로 직접 잰다.
        [Command]
        public void CmdBeginAim(uint targetNetId)
        {
            if (_stats == null) return;
            if (_weaponEquip.GetMainHandCategory() != WeaponCategory.Bow) return;
            if (Time.time < _serverNextShootTime) return; // 쿨다운 중엔 홀드 자체를 허용 안 함

            _isAiming = true;
            _aimGaugeServer = 0f;
            _aimTargetNetId = targetNetId;
            _aimLastPosition = transform.position;
        }

        // 무기 교체 등으로 홀드가 강제로 접힐 때. 발사로 이어지지 않는다.
        [Command]
        public void CmdCancelAim()
        {
            _isAiming = false;
            _aimGaugeServer = 0f;
        }

        // 버튼을 뗀 시점 — 게이지 값은 얼려두고 갱신만 멈춘다.
        // 실제 발사(CmdRequestShoot)는 애니메이션의 발사 프레임에서 별도로 온다.
        [Command]
        public void CmdEndAim() => _isAiming = false;

        // 발사 프레임에 로컬 플레이어가 호출한다. 여기서 실제 데미지 파이프라인이 돈다.
        //
        // 클라 파라미터 검증 원칙은 근접(CmdResolveAttackHit)과 동일하다:
        //   power / 투사체 수치 / 레이어 → 클라 입력을 아예 안 받고 서버가 자기 데이터로 채운다
        //   origin                      → 자기 캐릭터 근처인지 거리로 검증
        //   발사 간격                    → 클라 게이트를 믿지 않고 서버가 따로 잰다
        [Command]
        public void CmdRequestShoot(Vector3 origin, Vector3 direction)
        {
            if (_rangedSkill == null || _stats == null || _relay == null) return;
            if (Time.time < _serverNextShootTime) return;
            if (Vector3.Distance(transform.position, origin) > MaxAttackReach) return;
            if (direction.sqrMagnitude < 0.0001f) return;
            if (_weaponEquip.GetMainHandCategory() != WeaponCategory.Bow) return; // 서버가 무기를 재확인

            // 네트워크 지연 흔들림을 감안해 클라 쿨다운보다 10% 느슨하게 — 정상 플레이가 막히면 안 된다
            _serverNextShootTime = Time.time + _stats.AttackCooldown * 0.9f;

            // 게이지는 여기서 확정하고 닫는다 — 다음 CmdBeginAim이 올 때까지 0으로 둔다.
            float gauge = _aimGaugeServer;
            _isAiming = false;
            _aimGaugeServer = 0f;

            // ③ 후반에 급격히 강해지는 곡선 — 지수를 키워 앞부분은 완만하게, 100%에 가까울수록 가파르게.
            float homingStrength = Mathf.Pow(gauge, _stats.HomingCurveExponent);
            float turnRate = homingStrength * _stats.HomingMaxTurnRate;
            float damageMultiplier = Mathf.Lerp(1f, _stats.ChargeDamageMultiplierMax, homingStrength);

            Transform homingTarget = null;
            if (_aimTargetNetId != 0 && NetworkServer.spawned.TryGetValue(_aimTargetNetId, out var targetIdentity))
                homingTarget = targetIdentity.transform;

            float power = _weaponEquip.GetMainHandAttack();
            if (power <= 0f) power = _stats.UnarmedAttack;
            power *= damageMultiplier; //  게이지가 데미지에 영향을 줌

            var request = new ProjectileRequest
            {
                Prefab = _relay.ProjectilePrefab,
                // 호스트면 손에 든 화살을 그대로 쏜다. 전용 서버는 손 화살이 없어 풀에서 대여된다.
                NockSource = _nockPoint,
                Origin = origin,
                Direction = direction.normalized,
                Speed = _stats.ProjectileSpeed,
                SweepRadius = _stats.ProjectileSweepRadius,
                MaxLifetime = _stats.ShootRange / Mathf.Max(_stats.ProjectileSpeed, 0.01f) + 0.3f,
                HitMask = MonsterLayerMask,
                BlockMask = _stats.BlockLayerMask,
                HomingTarget = homingTarget,
                HomingTurnRateDegPerSec = turnRate,
                OnResolved = point =>
                {
                    if (_relay != null && _relay.netIdentity != null)
                        _relay.RpcConfirmHit(point);
                },
            };

            var ctx = new SkillContext(gameObject, System.Array.Empty<IDamageable>(), power, request);
            _rangedSkill.Execute(ctx).Forget();
        }
    }
}