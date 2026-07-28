using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    // 원거리 몹의 발사 애니메이션 이벤트 훅. PlayerAttackController와 대칭 구조.
    public class RangedMonsterHitbox : MonoBehaviour
    {
        [SerializeField] private MonsterAI _monsterAI;
        [SerializeField] private RangedMonsterStatsSO _stats;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private EnemyProjectile _projectilePrefab;
        [SerializeField] private float _aimHeightOffset = 1.4f; // 플레이어 피벗이 발밑이라 가슴 높이 정도로 보정
        private SkillExecutor _skillExecutor;

        private void Awake()
        {
            if (_monsterAI == null || _stats == null || _spawnPoint == null || _projectilePrefab == null)
            {
                Debug.LogError($"RangedMonsterHitbox 참조 누락 on {this.gameObject.name}", this);
                enabled = false;
                return;
            }

            _skillExecutor = new SkillExecutor(
                new SingleTargetSelector(),
                new ProjectileCast(_projectilePrefab, _spawnPoint, _stats.ProjectileSpeed),
                new List<ISkillEffect> { new DamageEffect() });
        }

        // Shoot 애니메이션 클립의 발사(화살이 손을 떠나는) 프레임에 Animation Event로 연결
        public void OnShootRelease()
        {
            var targetPosition = _monsterAI.CurrentTargetPosition;
            if (targetPosition == null) return; // 타겟을 잃은 상태(사거리 이탈 등)면 발사 취소
            var aimPoint = targetPosition.Value + Vector3.up * _aimHeightOffset;
            var ctx = new SkillContext(gameObject, Array.Empty<IDamageable>(), _stats.Attack, aimPoint);
            _skillExecutor.Execute(ctx).Forget();
        }
    }
}