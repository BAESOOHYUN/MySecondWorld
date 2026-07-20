using System.Collections.Generic;
using Cleverous.VaultInventory;

// 장비 아이템
namespace MySecondWorld.Inventory.Items
{
    public class EquipmentItem : RootItem
    {
        public List<StatModifier> Stats = new List<StatModifier>(); // (장착 중 스탯 영구 적용)
        public WeaponCategory Category; // 무기 카테고리 (애니메이션 그룹 매핑용, weapon-draft.csv Category 컬럼)
    }
}