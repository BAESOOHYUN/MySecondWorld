using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    [CreateAssetMenu(fileName = "MonsterStatsSO", menuName = "Combat/Monster Stats")]
    public class MonsterStatsSO : ScriptableObject
    {
        // TODO : 7/28 기준 몬스터 1종만 테스트. 차후 몬스터 종류나 패턴이 늘어남에 따라 추가될 수 있음.
        [SerializeField] private float _maxHP = 100f;
        [SerializeField] private float _attack = 10f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _attackCooldown = 1.5f;
        [SerializeField] private float _detectRange = 8f;
        [SerializeField] private float _moveSpeed = 3.5f;

        public float MaxHP => _maxHP;
        public float Attack => _attack;
        public float AttackRange => _attackRange;
        public float AttackCooldown => _attackCooldown;
        public float DetectRange => _detectRange;
        public float MoveSpeed => _moveSpeed;
    }
}