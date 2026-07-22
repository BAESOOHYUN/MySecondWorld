using UnityEngine;

public class TestManager : MonoBehaviour
{

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            Debug.Log("Alpha0 번 키 눌림");
            TestInventoryItem();
        }
    }
    private void TestInventoryItem()
    {
        var invs = Object.FindObjectsByType<Cleverous.VaultInventory.Inventory>(FindObjectsSortMode.None);
        if (invs.Length == 0)
        {
            Debug.Log("인벤토리 없음 - 플레이어 스폰 확인 필요");
            return;
        }
        var inv = invs[0];
        var potion = UnityEditor.AssetDatabase.LoadAssetAtPath<Cleverous.VaultInventory.RootItem>("Assets/06.ScriptableObject/Inventory/Consumables/DungeonItems_SM_Item_Potion_02.asset");
        inv.DoAdd(new Cleverous.VaultInventory.RootItemStack(potion, 1));
        Debug.Log($"granted 1 more potion to {inv.name}");
    }
}
