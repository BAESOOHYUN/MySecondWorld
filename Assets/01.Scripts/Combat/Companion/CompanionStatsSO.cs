using UnityEngine;

namespace MySecondWorld.Combat.Companion
{
    [CreateAssetMenu(fileName = "CompanionStatsSO", menuName = "Combat/Companion Stats")]
    public class CompanionStatsSO : ScriptableObject
    {
        [SerializeField] private float _maxHP = 100f;
        [SerializeField] private float _attack = 10f;
        [SerializeField] private float _moveSpeed = 3.5f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _attackCooldown = 1.5f;
        [SerializeField] private float _detectRange = 8f;
        [SerializeField] private float _followDistanceThreshold = 3f; // owner와 이 거리 이상 벌어져야 다시 쫓아감
        [SerializeField] private float _deathDuration = 1.5f; // Death 애니메이션 재생 후 비활성화까지 대기시간

        #region Property
        public float MaxHP => _maxHP;
        public float Attack => _attack;
        public float MoveSpeed => _moveSpeed;
        public float AttackRange => _attackRange;
        public float AttackCooldown => _attackCooldown;
        public float DetectRange => _detectRange;
        public float FollowDistanceThreshold => _followDistanceThreshold;
        public float DeathDuration => _deathDuration;
        #endregion
    }
}