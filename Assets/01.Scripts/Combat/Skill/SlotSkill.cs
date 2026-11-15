using UnityEngine;

namespace MySecondWorld.Combat
{
    // 슬롯 하나가 들고 있는 진행 상태. 클라와 서버가 각자 따로 갖는다(Host에서 섞이면 서로의 쿨다운을 거부한다).
    public struct SlotCursor
    {
        public int Step;        // 콤보에서 다음에 나갈 단계 (단일 스킬은 항상 0)
        public float ReadyAt;   // 이 시각 전에는 입력을 받지 않는다
        public float ExpireAt;  // 이 시각을 넘기면 콤보가 1단계로 돌아간다
    }

    // 슬롯에 넣을 수 있는 것의 공통 부모: 단일 스킬(SkillDefinition) 또는 콤보(ComboDefinition).
    // 슬롯 UI가 필요로 하는 이름·아이콘·쿨다운은 여기에 둔다.
    public abstract class SlotSkill : ScriptableObject
    {
        [Header("슬롯 표시·제한")]
        [SerializeField] private string _displayName;   // 슬롯 UI에 보일 이름. 비우면 에셋 이름을 쓴다
        [SerializeField] private Sprite _icon;          // 슬롯 UI 아이콘
        [Min(0f)] [SerializeField] private float _cooldown = 1f; // 재사용 대기(초). 콤보는 마지막 단계 뒤에 적용된다

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public Sprite Icon => _icon;
        public float Cooldown => _cooldown;

        // 슬롯 입력 1회를 처리한다. 지금 실행할 스킬을 돌려주고 cursor를 갱신한다. 아직 입력을 받을 수 없으면 null.
        // cooldownScale: 서버는 네트워크 지연을 감안해 1보다 작은 값(느슨하게)을 넘긴다.
        public abstract SkillDefinition TryAdvance(ref SlotCursor cursor, float now, float cooldownScale);
    }
}
