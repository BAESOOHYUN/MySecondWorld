using UnityEngine;
using Mirror;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 포탈의 한쪽 면. _sourceZone은 "이 트리거가 대표하는 출발 쪽 존".
    /// 두 씬이 동시에 로드된 경계 순간 반대쪽 트리거가 먼저 발동해 월드가 이미 바뀌었다면
    /// 자기 자신은 조용히 무시한다 (currentWorld가 더 이상 _sourceZone과 안 맞으므로).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ZonePortalTrigger : MonoBehaviour
    {
        [SerializeField] private ZonePortalSO _portal;
        [SerializeField] private ZoneDefinitionSO _sourceZone;
        private void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!NetworkServer.active) return;

            var identity = other.GetComponentInParent<NetworkIdentity>();
            if (identity == null || identity.connectionToClient == null) return;

            (NetworkManager.singleton as GameNetworkManager)?.ZoneService
                .OnPortalTriggerEntered(identity.connectionToClient, _portal, _sourceZone);
        }

    }
}
