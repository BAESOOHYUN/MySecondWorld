using System;

namespace MySecondWorld.Inventory
{
    // 아이템 하나가 여러 카테고리를 동시에 가질 수 있도록 [Flags]로 설계.
    // 각 값은 정수 하나에서 서로 다른 비트 한 칸씩을 차지해야 하므로 1<<n(2의 n제곱) 형태로 정의:
    //   Equipment = 1  (0000 0001)
    //   Weapon    = 2  (0000 0010)
    //   Consumable= 4  (0000 0100)
    //   Material  = 8  (0000 1000)
    //   Quest     = 16 (0001 0000)
    //   Currency  = 32 (0010 0000)
    // 순차값(1,2,3,4..)을 쓰면 1|2=3처럼 조합 결과가 다른 값과 겹쳐 구분이 안 되기 때문.

    // 사용 예 (사과 = Material + Consumable):
    //   apple.Tags = ItemCategory.Material | ItemCategory.Consumable;
    //   // |(OR) = 태그 추가/합치기 → 0000 1000 | 0000 0100 = 0000 1100(두 비트 동시 켜짐)
    //
    //   bool isConsumable = (apple.Tags & ItemCategory.Consumable) != 0;
    //   // &(AND) = 특정 태그가 켜져 있는지 검사 → 0000 1100 & 0000 0100 = 0000 0100(0이 아니므로 true)
    //   // 대상 비트만 걸러내고, 그 결과가 0이 아니면(즉 해당 비트가 원래 켜져 있었으면) 포함된 것
    //
    //   bool isConsumable2 = apple.Tags.HasFlag(ItemCategory.Consumable);
    //   // 위 & 검사를 감싼 편의 메서드. 약간 느리지만 가독성 좋음 (지금 규모에선 문제 없음)
    //
    // [Flags] 속성 덕분에 인스펙터에서도 라디오 버튼이 아닌 다중 선택 체크박스로 표시됨.

    [Flags]
    public enum ItemCategory
    {
        None = 0,
        Equipment = 1 << 0,
        Weapon = 1 << 1,
        Consumable = 1 << 2,
        Material = 1 << 3,
        Quest = 1 << 4,
        Currency = 1 << 5,
    }
}