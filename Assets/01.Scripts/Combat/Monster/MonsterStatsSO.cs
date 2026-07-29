using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    [CreateAssetMenu(fileName = "MonsterStatsSO", menuName = "Combat/Monster Stats")]
    public class MonsterStatsSO : ScriptableObject
    {
        [SerializeField] private float _maxHP = 100f;
        [SerializeField] private float _attack = 10f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _attackCooldown = 1.5f;
        [SerializeField] private float _detectRange = 8f;
        [SerializeField] private float _moveSpeed = 3.5f;
        [SerializeField] private float _deathDuration = 1.5f; // Death 애니메이션 재생 후 비활성화까지 대기시간

        public float MaxHP => _maxHP;
        public float Attack => _attack;
        public float AttackRange => _attackRange;
        public float AttackCooldown => _attackCooldown;
        public float DetectRange => _detectRange;
        public float MoveSpeed => _moveSpeed;
        public float DeathDuration => _deathDuration;
    }
}