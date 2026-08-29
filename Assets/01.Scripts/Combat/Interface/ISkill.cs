using Cysharp.Threading.Tasks;

namespace MySecondWorld.Combat
{
    // 축4 — "스킬 하나를 실행한다"는 계약.
    // 단일 스킬(SkillDefinition)과 콤보(ComboSkill)를 같은 타입으로 다루기 위해 존재한다.
    // Composite 패턴의 성립 조건: Leaf와 Composite가 같은 Execute를 갖는다.
    public interface ISkill
    {
        UniTask Execute(SkillContext ctx);
    }
}