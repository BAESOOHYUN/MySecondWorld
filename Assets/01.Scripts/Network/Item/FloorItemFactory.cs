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

            // 프리팹에서 NetworkFloorItem을 다시 뺐다 붙이는 실수가 실제로 반복됐던 지점이라 방어적으로 확인
            if (!go.TryGetComponent(out NetworkFloorItem floorItem))
            {
                Debug.LogError($"[FloorItemFactory] '{purePrefab.name}' 프리팹에 NetworkFloorItem이 없습니다.", purePrefab);
                UnityEngine.Object.Destroy(go);
                return null;
            }

            // isTrigger는 반드시 프리팹에 저장돼 있어야 한다.
            // 여기서 런타임에 대입하면 서버 인스턴스에만 적용되고, 클라는 각자 원본 프리팹에서
            // Instantiate하므로 프리팹 값(false)을 그대로 쓴다 → 원격 클라에서 통행 방해 + 픽업 불가.
            // 그래서 고쳐주지 않고 잘못된 설정을 드러내기만 한다.
            var sphere = go.GetComponent<SphereCollider>();
            if (!sphere.isTrigger)
                Debug.LogError(
                    $"[FloorItemFactory] '{purePrefab.name}' 프리팹의 SphereCollider가 Trigger가 아닙니다 — " +
                    $"원격 클라에서 픽업 불가 + 통행 방해. 프리팹에서 직접 체크하세요.", purePrefab);

            return floorItem;
        }
    }
}
