using UnityEngine;

namespace MySecondWorld.Combat.Companion
{
    [CreateAssetMenu(fileName = "CompanionStatsSO", menuName = "Combat/Companion Stats")]
    public class CompanionStatsSO : CombatantStatsSO
    {
        [SerializeField] private float _followDistanceThreshold = 3f; // owner와 이 거리 이상 벌어져야 다시 쫓아감
        [SerializeField] private float _maxChaseDistanceFromOwner = 12f; // owner 기준 이 거리 이상 몬스터를 쫓지 않음
        public float FollowDistanceThreshold => _followDistanceThreshold;
        public float MaxChaseDistanceFromOwner => _maxChaseDistanceFromOwner;
    }
}