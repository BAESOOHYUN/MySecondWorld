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

        // MonsterFactory가 Hitbox의 attackPoint를 찾을 때 쓰는 자식 소켓 이름.
        // 리그마다 본 이름이 달라서(예: 방패를 든 몹은 "ShieldBoneR") 몬스터별로 다르게 지정한다.
        [SerializeField] private string _attackPointSocketName = "AttackPoint";

        // 순수 프리팹엔 Collider/Animator가 없어서(전부 팩토리가 동적 부착) 몸통 크기·리그 참조를 데이터로 들고 있는다.
        [Header("몸통 콜라이더 (CapsuleCollider)")]
        [SerializeField] private Vector3 _bodyColliderCenter = new Vector3(0f, 0.95f, 0f);
        [SerializeField] private float _bodyColliderRadius = 0.2f;
        [SerializeField] private float _bodyColliderHeight = 1.9f;

        [Header("애니메이터 (리그 전용 — 몬스터마다 다름)")]
        [SerializeField] private Avatar _avatar;
        [SerializeField] private RuntimeAnimatorController _animatorController;

        #region Property
        public float MaxHP => _maxHP;
        public float Attack => _attack;
        public float AttackRange => _attackRange;
        public float AttackCooldown => _attackCooldown;
        public float DetectRange => _detectRange;
        public float MoveSpeed => _moveSpeed;
        public float DeathDuration => _deathDuration;
        public string AttackPointSocketName => _attackPointSocketName;
        public Vector3 BodyColliderCenter => _bodyColliderCenter;
        public float BodyColliderRadius => _bodyColliderRadius;
        public float BodyColliderHeight => _bodyColliderHeight;
        public Avatar Avatar => _avatar;
        public RuntimeAnimatorController AnimatorController => _animatorController;
        #endregion
    }
}