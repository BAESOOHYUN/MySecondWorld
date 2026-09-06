using System;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // HP 관리 + 데미지 처리 + 사망 이벤트 발행을 담당하는 컴포넌트 (IDamageable의 첫 구현체)
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float _maxHP = 100f;
        [SerializeField] private CombatFaction _faction = CombatFaction.Player;
        public float MaxHP => _maxHP; 
        public float CurrentHP { get; private set; }
        public bool IsDead => CurrentHP <= 0f;
        public CombatFaction Faction => _faction;
        public event Action OnDied;
        public event Action<float> OnDamaged; // 데미지로 CurrentHP가 바뀔 때마다 새 CurrentHP 값을 실어 발행
        public event Action<float, GameObject> OnDamagedWithAttacker; // 공격자 정보가 포함된 피격 이벤트

        // 죽지 않았어도 "지금은 공격 대상이 될 수 없음" 상태(소환해제 등)를 알리는 이벤트.
        // MonsterAI가 OnDied와 동일하게 처리해 표적에서 즉시 뺀다.
        public event Action OnBecameUntargetable;
        private bool _isTargetable = true;
        public bool IsTargetable => _isTargetable;


        private void OnEnable()
        {
            CurrentHP = _maxHP;
        }

        public void Initialize(float maxHP)
        {
            _maxHP = maxHP;
            CurrentHP = maxHP;
        }

        // Monster/Companion Awake가 진영까지 함께 확정하는 경로.
        // 기존 Initialize(float)는 Player처럼 Inspector authoring에 맡기는 호출부를 위해 남겨둔다.
        public void Initialize(float maxHP, CombatFaction faction)
        {
            _maxHP = maxHP;
            CurrentHP = maxHP;
            _faction = faction;
        }

        public void SetTargetable(bool targetable)
        {
            if (_isTargetable == targetable) return;
            _isTargetable = targetable;
            if (!targetable)
                OnBecameUntargetable?.Invoke();
        }

        public void TakeDamage(float damage) => TakeDamage(damage, null);

        public void TakeDamage(float damage, GameObject attacker)
        {
            // 죽었거나, 소환 해제(IsTargetable == false) 상태면 데미지 완벽 무시
            if (IsDead || !_isTargetable || damage <= 0f) return;

            // 다른 방(match)에서 온 공격은 무시한다.
            // 인스턴싱된 던전은 방마다 몬스터 세트가 같은 좌표에 겹쳐 있는데,
            // 물리 쿼리(SphereCast/OverlapSphere)는 matchId를 모르기 때문에
            // 여기서 한 번 걸러 주지 않으면 남의 방 몬스터를 때리게 된다.
            // 투사체·AoE·근접이 전부 이 메서드를 거치므로 차단은 여기 한 곳이면 충분하다.
            if (attacker != null && !MySecondWorld.Network.MatchScope.Same(gameObject, attacker)) return;
            CurrentHP = Mathf.Max(0f, CurrentHP - damage);

            Debug.Log($"<color=orange>[DamageTrack]</color> <b>{gameObject.name}</b> 피격! -{damage:F1} (남은 HP: {CurrentHP:F1}) | 공격자: {(attacker != null ? attacker.name : "None")}");

            OnDamaged?.Invoke(CurrentHP);
            OnDamagedWithAttacker?.Invoke(CurrentHP, attacker);
            if (CurrentHP <= 0)
                OnDied?.Invoke();
        }

        public void ReviveFull()
        {
            CurrentHP = _maxHP;
        }
    }
}