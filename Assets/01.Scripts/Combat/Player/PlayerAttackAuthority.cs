using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Mirror;
using MySecondWorld.Network;

namespace MySecondWorld.Combat.Player
{
    // 서버 권위 공격 판정 전용 NetworkBehaviour.
    // NetworkWeaponEquip(Network 어셈블리)에 두면 Network→Combat 참조가 생겨
    // 기존 Combat→Network 참조와 순환 참조가 되므로, Combat 어셈블리 안에 별도로 둔다.
    public class PlayerAttackAuthority : NetworkBehaviour
    {
        private SkillExecutor _skillExecutor;
        private NetworkWeaponEquip _weaponEquip;
        private const float MaxAttackReach = 4f; // 공격 반경(1.5) + 이동 오차 여유
        private const float MaxShootRange = 20f; // 원거리(투사체) _shootRange 밸런스 상한 여유 — 클라가 값을 부풀려 보내도 이 이상은 판정하지 않음
        private static int _monsterLayerMask = -1;

        // 핵심은 - 1을 "아직 계산 안 함" 표시로 쓴다는 거. -1이 아니면 "이미 계산해서 저장해둔 값(_monsterLayerMask)"
        // LayerMask.GetMask가 실제로 돌려주는 값은 항상 0 이상이라, -1은 절대 나올 수 없는 값. -1이면 "아직 캐싱 안 됨(LayerMask.GetMask("Monster")로 캐싱)"
        private static int MonsterLayerMask =>
             _monsterLayerMask != -1                                                    
            ? _monsterLayerMask : (_monsterLayerMask = LayerMask.GetMask("Monster"));  
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMonsterLayerMaskCache() => _monsterLayerMask = -1;

        private void Awake()
        {
            _skillExecutor = new SkillExecutor(
                new SingleTargetSelector(),
                new InstantCast(),
                new List<ISkillEffect> { new DamageEffect() });

            _weaponEquip = GetComponent<NetworkWeaponEquip>();
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
        public void CmdResolveAttackHit(Vector3 attackPointPosition, float attackRadius, float unarmedAttack)
        {
            // 클라가 보낸 공격 위치가 클라 자신의 캐릭터 근처인지 검증 — 너무 멀면 조작 의심, 무시
            if (Vector3.Distance(transform.position, attackPointPosition) > MaxAttackReach) return;

            // 공격력은 클라 파라미터가 아니라 서버가 들고 있는 실제 장착 무기값에서 조회
            float power = _weaponEquip.GetMainHandAttack();
            if (power <= 0f) power = unarmedAttack;
            Debug.Log($"[Damage] 공격력 계산 — power={power} (무기 미보유 시 unarmedAttack={unarmedAttack})");

            // Physics.OverlapSphere : 물리 충돌이 아니라 순수 공간 질의. "그 안에 지금 걸쳐있는 콜라이더들이 뭐가 있어?"
            var candidates = Physics.OverlapSphere(attackPointPosition, attackRadius, MonsterLayerMask, QueryTriggerInteraction.Ignore) // 공격 지점 반경 안의 몬스터 레이어 콜라이더 전부 탐지(트리거 콜라이더는 무조건 무시)
                .OrderBy(c => Vector3.Distance(attackPointPosition, c.transform.position)) // 가까운 순 정렬 (SingleTargetSelector가 첫 번째를 "가장 가까운 대상"으로 씀)
                .Select(c => c.GetComponentInParent<IDamageable>()) // 콜라이더 → 자신+조상 중 IDamageable 붙은 가장 가까운 오브젝트로 변환
                .Where(d => d != null)                              // IDamageable이 없는 대상(오탐) 제거
                .ToList();                                          // 지연 평가를 여기서 즉시 실행해 리스트로 확정 (Candidates가 인덱스 접근 필요)

            var ctx = new SkillContext(this.gameObject, candidates, power);
            _skillExecutor.Execute(ctx).Forget();
        }

        // PlayerAttackController.OnShootRelease()가 위임하는 서버 권위 원거리(투사체) 판정.
        // 근접(CmdResolveAttackHit)과 마찬가지로 클라가 보낸 값을 그대로 믿지 않는다:
        //   - power              → 클라 입력 무시, 서버가 NetworkWeaponEquip에서 직접 조회
        //   - layerMask          → 클라 입력 안 받음, 서버 상수(MonsterLayerMask)로 고정
        //   - spawnPointPosition → 자기 위치와의 거리로 검증(MaxAttackReach)
        //   - shootRange         → 클라 값을 받되 MaxShootRange로 상한을 한 번 더 씌움
        //   - "누구를 맞혔는가"   → 클라가 주장하는 대상을 아예 받지 않고, 서버가 같은 방향으로
        //                          Projectile 동일한 반경의 SphereCast를 직접 쏴서 재확인한다.
        [Command]
        public void CmdResolveShootHit(Vector3 spawnPointPosition, Vector3 aimDirection, float shootRange, float projectileColliderRadius, float unarmedAttack)
        {
            // 클라가 보낸 발사 지점이 자기 캐릭터 근처인지 검증 — CmdResolveAttackHit과 동일한 방어
            if (Vector3.Distance(transform.position, spawnPointPosition) > MaxAttackReach) return;
            if (aimDirection.sqrMagnitude < 0.0001f) return; // 0벡터 등 이상값 방어

            float power = _weaponEquip.GetMainHandAttack();
            if (power <= 0f) power = unarmedAttack;

            float clampedRange = Mathf.Min(shootRange, MaxShootRange);
            var direction = aimDirection.normalized;

            // Projectile SphereCollider 반경과 동일한 두께로 재확인 — 클라 비주얼(투사체)과
            // 서버 판정의 "맞았다/안 맞았다" 체감이 최대한 일치하도록 맞춘다.
            if (!Physics.SphereCast(spawnPointPosition, projectileColliderRadius, direction, out var hit, clampedRange, MonsterLayerMask, QueryTriggerInteraction.Ignore))
                return; // 서버 재확인 결과 아무것도 안 맞음 — 클라가 뭐라고 주장했든 데미지 없음

            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null) return; // IDamageable 없는 콜라이더(오탐) 무시

            var ctx = new SkillContext(this.gameObject, new List<IDamageable> { target }, power);
            _skillExecutor.Execute(ctx).Forget(); // 기존 _skillExecutor 재사용 — 이미 SphereCast로 대상이 확정됐으므로 추가 캐스트 타이밍 불필요
        }
    }
}