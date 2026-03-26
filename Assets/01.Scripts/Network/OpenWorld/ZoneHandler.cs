using UnityEngine;
using Mirror;
using MySecondWorld.Utils;

namespace MySecondWorld.Network.OpenWorld
{
    /// <summary>
    /// 씬 경계 위에 배치하는 양방향 Zone 컴포넌트.
    /// 이 GameObject에 Collider를 직접 붙인다.
    ///
    /// 동작 원리:
    ///   진입 → 반대편 씬 미리 로드 (양쪽 씬 동시 유지)
    ///   퇴장 → 방금 떠나온 씬 언로드
    ///   → 와리가리해도 Zone 안에 있는 동안 양쪽 씬 모두 유지
    ///
    /// 방향 판정: ZoneHandler의 local Z축 기준.
    ///   local z &lt; 0 → sceneA 영역 (뒤쪽)
    ///   local z &gt; 0 → sceneB 영역 (앞쪽)
    ///
    /// 사용 패턴:
    ///   일반 양방향 전환 : 이 컴포넌트만, sceneA/sceneB 설정
    ///   던전 출구        : 이 컴포넌트 + 자식에 UnloadTrigger, exitAllDungeons = true
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ZoneHandler : MonoBehaviour
    {
        [Scene]
        [Tooltip("local z < 0 쪽 씬 (ZoneHandler 뒤쪽)")]
        public string sceneA;

        [Scene]
        [Tooltip("local z > 0 쪽 씬 (ZoneHandler 앞쪽), Z축 파란 화살표 방향")]
        public string sceneB;

        [Tooltip("true면 UnloadTrigger 진입 시 Dungeon/ 씬 전체 언로드. 던전 출구 전용.")]
        public bool exitAllDungeons = false;

        [Tooltip("던전 출구 전용. 던전이 localZ < 0(sceneA 방향)이면 true, localZ > 0(sceneB 방향)이면 false.\n" +
                 "언로드 트리거를 던전 안쪽에서 밟을 때만 언로드. 바깥(필드)에서 닿으면 무시.")]
        public bool dungeonIsNegativeZ = true;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;

            // 던전 출구용 UnloadTrigger 자식도 isTrigger 보장
            foreach (var col in GetComponentsInChildren<Collider>())
                col.isTrigger = true;
        }

        // ── 트리거 ────────────────────────────────────────────────────

        [ServerCallback]
        public void OnTriggerEnter(Collider other)
        {
            if (!TryGetPlayerConn(other, out var conn, out var mgr)) return;
            /*
                어떤 객체 A가 World 좌표계에서 위치 (10, 10, 10)에 있고,
                객체 B는 A의 자식(child) 객체로서 A의 Local 좌표계에서 위치 (1, 1, 1)에 있다고 가정해보자.
                이 경우 객체 B의 월드 좌표는 (11, 11, 11)이 될 것이다.
                객체 A의 `InverseTransformPoint` 메소드를 사용하여 객체 B의 월드 좌표 (11, 11, 11)을 입력하면,
                결과로 (1, 1, 1)이라는 로컬 좌표가 반환된다.
                만약 부모 객체가 회전하거나 이동하면 그 아래의 자식 객체들의 월드 좌표도 변경된다.
                로컬 좌표계를 사용한다면 부모 객체 내에서의 상대적인 위치나 방향을 일관성 있게 알 수 있다.
            */
            float localZ = transform.InverseTransformPoint(other.transform.position).z;
            // [  →진입     sceneA      콜라이더 박스(0)z→      sceneB (로드)       ]
            // [     (로드) sceneA      콜라이더 박스(0)z→      sceneB       ←진입  ]
            string sceneToLoad = localZ < 0f ? sceneB : sceneA;
            if (string.IsNullOrEmpty(sceneToLoad)) return;

            mgr.SendSceneLoad(conn, sceneToLoad);
            Debug.Log($"[Zone] PreLoad: {sceneToLoad} (localZ={localZ:F1})");
        }

        [ServerCallback]
        public void OnTriggerExit(Collider other)
        {
            if (!TryGetPlayerConn(other, out var conn, out var mgr)) return;

            float localZ = transform.InverseTransformPoint(other.transform.position).z;

            // [   (언로드) sceneA      콜라이더 박스(0)z→      sceneB       진출→  ]
            // [  ←진출     sceneA      콜라이더 박스(0)z→      sceneB (언로드)     ]
            string sceneToUnload = localZ >= 0f ? sceneA : sceneB;
            if (string.IsNullOrEmpty(sceneToUnload)) return;

            mgr.SendSceneUnload(conn, sceneToUnload);
            Debug.Log($"[Zone] Unload(Exit): {sceneToUnload} (localZ={localZ:F1})");
        }

        // UnloadTrigger 자식에서 호출 (던전 출구 전용)
        [ServerCallback]
        public void OnUnloadEnter(Collider other)
        {
            if (!exitAllDungeons) return;
            if (!TryGetPlayerConn(other, out var conn, out var mgr)) return;

            // 방향 체크: 던전 안쪽에서 진입할 때만 언로드
            // 필드 방향에서 실수로 닿아도 던전이 사라지지 않도록
            // ZoneHandler의 파란 화살표(+Z) 기준으로:
            // 던전이 뒤쪽(sceneA, localZ < 0)에 있으면 → true  ✓
            // 던전이 앞쪽(sceneB, localZ > 0)에 있으면 → false
            // 던전이 sceneA면 true, sceneB면 false
            float localZ = transform.InverseTransformPoint(other.transform.position).z;
            bool fromDungeon = dungeonIsNegativeZ ? (localZ < 0f) : (localZ > 0f);
            if (!fromDungeon) return;

            mgr.UnloadAllDungeonsFor(conn);
        }

        // ── Gizmo (에디터 모드 전용, 하늘색) ─────────────────────────

        private void OnDrawGizmos() => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, new Color(0f, 0.8f, 1f), 0.15f);
        private void OnDrawGizmosSelected() => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, new Color(0f, 0.8f, 1f), 0.35f);

        // ── 내부 헬퍼 ─────────────────────────────────────────────────

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
