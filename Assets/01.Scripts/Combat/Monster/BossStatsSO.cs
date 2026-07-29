using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    [CreateAssetMenu(fileName = "BossStatsSO", menuName = "Combat/Boss Stats")]
    public class BossStatsSO : MonsterStatsSO
    {
        // 오름차순 배열. HP비율이 이 값 이하로 내려갈 때마다 다음 페이즈로 전환.
        // 예: [0.5f] = 1회 전환(페이즈1→2), 50% 미만이면 페이즈2.
        [SerializeField] private float[] _phaseHpThresholds = { 0.5f };
        [SerializeField] private float _aoeRadius = 3f;
        [SerializeField] private float _aoeCooldown = 4f;
        [SerializeField] private float _phaseTransitionDuration = 1.2f;
        public IReadOnlyList<float> PhaseHpThresholds => _phaseHpThresholds;
        public float AoERadius => _aoeRadius;
        public float AoECooldown => _aoeCooldown;
        public float PhaseTransitionDuration => _phaseTransitionDuration;
    }
}