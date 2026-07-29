using UnityEngine;

namespace MySecondWorld.Network
{
    // 순수 프리팹(NetworkFloorItem 스크립트 없음, NetworkIdentity+MeshCollider만)에
    // 스폰 시점마다 NetworkFloorItem을 붙이는 팩토리.
    // 서버 스폰 경로(NetworkInventoryActions)와 Mirror 클라이언트 커스텀 스폰 핸들러(GameNetworkManager)
    // 양쪽에서 재사용해서 서버/클라 컴포넌트 구성이 항상 동일하게 맞춰지도록 한다.
    public static class FloorItemFactory
    {
        public static NetworkFloorItem Create(GameObject purePrefab, Vector3 position, Quaternion rotation)
        {
            var go = UnityEngine.Object.Instantiate(purePrefab, position, rotation);
            var floorItem = go.AddComponent<NetworkFloorItem>(); // RequireComponent(SphereCollider)가 이 안에서 자동으로 SphereCollider까지 붙여줌(없었다면)

            // 프리팹에 원래 있던 SphereCollider든 방금 자동 부착된 것이든 구분하지 않고 항상 재설정.
            // 자동 부착된 SphereCollider는 isTrigger 기본값이 false라, 강제로 안 맞추면 픽업 판정(OnTriggerEnter)이 조용히 안 됨.
            go.GetComponent<SphereCollider>().isTrigger = true;

            return floorItem;
        }
    }
}
