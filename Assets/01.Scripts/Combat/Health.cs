using System;
using UnityEngine;

namespace MySecondWorld.Combat
{
    // HP 관리 + 데미지 처리 + 사망 이벤트 발행을 담당하는 컴포넌트 (IDamageable의 첫 구현체)
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float _maxHP = 100f;
        public float MaxHP => _maxHP; 
        public float CurrentHP { get; private set; }
        public bool IsDead => CurrentHP <= 0f;
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