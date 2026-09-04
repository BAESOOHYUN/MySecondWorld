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

        public void TakeDamage(float damage)
        {
            // 죽었거나, 소환 해제(IsTargetable == false) 상태면 데미지 완벽 무시
            if (IsDead || !_isTargetable || damage <= 0f) return;
            CurrentHP = Mathf.Max(0f, CurrentHP - damage);
            OnDamaged?.Invoke(CurrentHP);
            if (CurrentHP <= 0)
                OnDied?.Invoke();
        }

        public void ReviveFull()
        {
            CurrentHP = _maxHP;
        }
    }
}