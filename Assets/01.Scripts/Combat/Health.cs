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

        public void TakeDamage(float damage)
        {
            if (IsDead || damage <= 0f) return;
            CurrentHP = Mathf.Max(0f, CurrentHP - damage);
            OnDamaged?.Invoke(CurrentHP);

            if (CurrentHP <= 0)
                OnDied?.Invoke();
        }
    }
}