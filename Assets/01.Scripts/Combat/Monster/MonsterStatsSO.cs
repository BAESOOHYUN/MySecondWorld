using UnityEngine;

namespace MySecondWorld.Combat.Monster
{
    [CreateAssetMenu(fileName = "MonsterStatsSO", menuName = "Combat/Monster Stats")]
    public class MonsterStatsSO : CombatantStatsSO
    {
        // MonsterFactory가 Hitbox의 attackPoint를 찾을 때 쓰는 자식 소켓 이름.
        // 리그마다 본 이름이 달라서(예: 방패를 든 몹은 "ShieldBoneR") 몬스터별로 다르게 지정한다.
        [SerializeField] private string _attackPointSocketName = "AttackPoint";

        // 순수 프리팹엔 Collider/Animator가 없어서(전부 팩토리가 동적 부착) 몸통 크기·리그 참조를 데이터로 들고 있는다.
        [Header("몸통 콜라이더 (CapsuleCollider)")]
        [SerializeField] private Vector3 _bodyColliderCenter = new Vector3(0f, 0.95f, 0f);
        [SerializeField] private float _bodyColliderRadius = 0.2f;
        [SerializeField] private float _bodyColliderHeight = 1.9f;

        #region Property
       
        public string AttackPointSocketName => _attackPointSocketName;
        public Vector3 BodyColliderCenter => _bodyColliderCenter;
        public float BodyColliderRadius => _bodyColliderRadius;
        public float BodyColliderHeight => _bodyColliderHeight;

        #endregion
    }
}