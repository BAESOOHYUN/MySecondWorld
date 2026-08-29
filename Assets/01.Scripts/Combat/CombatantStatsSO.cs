using UnityEngine;

namespace MySecondWorld.Combat
{
    // 몬스터/컴패니언 등 "전투에 참여하는 개체"가 공통으로 갖는 기초 스탯.
    // MonsterStatsSO(동적 팩토리 부착 전용 필드 보유)와 CompanionStatsSO(완성 프리팹 인스턴스화)는
    // 스폰 방식이 달라 서로 다른 필드도 있지만, 이 7개는 완전히 동일해서 여기로 추출.
    public abstract class CombatantStatsSO : ScriptableObject
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