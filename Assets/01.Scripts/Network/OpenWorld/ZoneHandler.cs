using UnityEngine;
using Mirror;
using MySecondWorld.Utils;

namespace MySecondWorld.Network.OpenWorld
{
    /// <summary>
    /// 씬 경계에 배치하는 양방향 Zone 컴포넌트.
    ///
    /// 방향 판정: ZoneHandler의 local Z축 기준.
    ///   +Z (파란 화살표) 방향 → sceneA 영역
    ///   -Z (반대)         방향 → sceneB 영역
    ///
    /// 동작:
    ///   진입/체류 → 반대편 씬 미리 로드 (중복 요청 자동 무시)
    ///   퇴장      → 방금 떠나온 씬 언로드
    ///
    /// 배치 권고: onlineScene(OpenWorldRobby)에 배치.
    ///   서브씬이 언로드돼도 ZoneHandler가 사라지지 않음.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ZoneHandler : MonoBehaviour
    {
        [Scene]
        [Tooltip("+Z (파란 화살표) 방향의 씬")]
        public string sceneA;

        [Scene]
        [Tooltip("-Z (화살표 반대) 방향의 씬")]
        public string sceneB;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        // ── 트리거 ────────────────────────────────────────────────────

        [ServerCallback]
        private void OnTriggerEnter(Collider other) => HandleZoneEnter(other);

        /// <summary>
        /// OnTriggerStay: 플레이어가 Zone 안에 스폰됐을 때도 씬 로드를 보장.
        /// SendSceneLoad 내부에 중복 체크가 있으므로 매 프레임 호출해도 중복 전송 없음.
        /// </summary>
        [ServerCallback]
        private void OnTriggerStay(Collider other) => HandleZoneEnter(other);

        [ServerCallback]
        private void OnTriggerExit(Collider other)
        {
            if (!TryGetPlayerConn(other, out var conn, out var mgr)) return;

            float localZ = transform.InverseTransformPoint(other.transform.position).z;
            // +Z(sceneA) 방향으로 나감 → sceneB 언로드 (방금 있던 곳)
            // -Z(sceneB) 방향으로 나감 → sceneA 언로드 (방금 있던 곳)
            string sceneToUnload = localZ >= 0f ? sceneB : sceneA;
            if (string.IsNullOrEmpty(sceneToUnload)) return;

            mgr.SendSceneUnload(conn, sceneToUnload);
            Debug.Log($"[Zone] Unload: {sceneToUnload} (localZ={localZ:F1})");
        }

        // ── Gizmo ─────────────────────────────────────────────────────

        private void OnDrawGizmos()         => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, new Color(0f, 0.8f, 1f), 0.15f);
        private void OnDrawGizmosSelected() => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, new Color(0f, 0.8f, 1f), 0.35f);

        // ── 내부 헬퍼 ─────────────────────────────────────────────────

        private void HandleZoneEnter(Collider other)
        {
            if (!TryGetPlayerConn(other, out var conn, out var mgr)) return;

            float localZ = transform.InverseTransformPoint(other.transform.position).z;
            // +Z(sceneA) 쪽에서 진입 → sceneB 로드 (앞으로 이동할 씬)
            // -Z(sceneB) 쪽에서 진입 → sceneA 로드
            string sceneToLoad = localZ > 0f ? sceneB : sceneA;
            if (string.IsNullOrEmpty(sceneToLoad)) return;

            mgr.SendSceneLoad(conn, sceneToLoad);
        }

        private bool TryGetPlayerConn(Collider other,
            out NetworkConnectionToClient conn,
            out OpenWorldNetworkManager mgr)
        {
            conn = null;
            mgr = null;

            if (!other.CompareTag("Player")) return false;

            if (!other.TryGetComponent<NetworkIdentity>(out var identity))
            {
                Debug.LogWarning("[Zone] NetworkIdentity 못 찾음!");
                return false;
            }

            mgr = NetworkManager.singleton as OpenWorldNetworkManager;
            if (mgr == null) return false;

            conn = identity.connectionToClient;
            return true;
        }
    }
}
