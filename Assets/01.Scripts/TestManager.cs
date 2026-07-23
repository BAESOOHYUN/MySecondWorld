using MySecondWorld.Network;
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
        else if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("Alpha1 번 키 눌림");
            TestPurchase();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("Alpha2 번 키 눌림");
            TestReward();
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

    private void TestPurchase()
    {
        var inv = Object.FindObjectsByType<Cleverous.VaultInventory.Inventory>(FindObjectsSortMode.None)[0]; 
        var coin = UnityEditor.AssetDatabase.LoadAssetAtPath<Cleverous.VaultInventory.RootItem>("Assets/06.ScriptableObject/Inventory/Generic/DungeonItems_SM_Item_Coins_01.asset");
        inv.DoAdd(new Cleverous.VaultInventory.RootItemStack(coin, 10));
    }

    private void TestReward()
    {
        var rewards = Object.FindObjectsByType<NetworkQuestRewardStub>(FindObjectsSortMode.None);
        if (rewards.Length == 0)
        {
            Debug.Log("NetworkQuestRewardStub 없음 - 플레이어 스폰 확인 필요");
            return;
        }
        rewards[0].ClaimReward();
    }
}
