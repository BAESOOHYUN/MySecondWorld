using System;

namespace MySecondWorld.Combat
{
    // 스킬 제작 툴의 부품 보관함에 표시할 "이 부품이 하는 일" 설명.
    // 축1~3 SO 클래스에 붙인다. 에셋마다 적을 필요 없이 클래스당 한 번만 적으면 된다.

    // 클래스 이름을 XxxAttribute로 만들어 두면 [Xxx]로 짧게 쓸 수 있다. SkillPartAttribute가 [SkillPart]로 보이는 이유.
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SkillPartAttribute : Attribute
    {
        public string Description { get; }

        // 같은 그룹 이름을 가진 부품은 한 슬롯에 둘 이상 넣으면 효과가 겹친다(예: 데미지 + 스플래시 데미지).
        // 툴이 이런 조합을 경고한다. 겹쳐도 되는 부품은 비워 둔다.
        public string ExclusiveGroup { get; set; }

        public SkillPartAttribute(string description) => Description = description;
    }
}
