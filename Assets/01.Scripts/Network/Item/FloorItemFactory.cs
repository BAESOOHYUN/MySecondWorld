using UnityEngine;

namespace MySecondWorld.Network
{
    // NetworkFloorItem/NetworkMatch는 NetworkBehaviour라 Instantiate 이후 런타임에 못 붙인다
    // (NetworkIdentity.Awake()가 그 시점의 NetworkBehaviour만 한 번 스캔하기 때문 — NetworkFloorItem.cs 주석 참고).
    // 그래서 프리팹에 이미 붙어있는 걸 가져다 쓰고, SphereCollider의 isTrigger 기본값만 팩토리가 강제한다.
    public static class FloorItemFactory
    {
        public static NetworkFloorItem Create(GameObject purePrefab, Vector3 position, Quaternion rotation)
        {
            var go = UnityEngine.Object.Instantiate(purePrefab, position, rotation);
            var floorItem = go.GetComponent<NetworkFloorItem>();

            // RequireComponent 덕에 SphereCollider는 프리팹에 항상 있지만, isTrigger 기본값(false)만은
            // 항상 명시적으로 강제 — 프리팹 쪽에서 깜빡 꺼놔도 픽업 판정(OnTriggerEnter)이 안전하게 동작하도록.
            go.GetComponent<SphereCollider>().isTrigger = true;

            return floorItem;
        }
    }
}
