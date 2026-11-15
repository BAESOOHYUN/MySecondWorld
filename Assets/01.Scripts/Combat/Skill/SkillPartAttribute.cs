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

        public SkillPartAttribute(string description) => Description = description;
    }
}
