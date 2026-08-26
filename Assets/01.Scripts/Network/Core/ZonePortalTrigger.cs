using System.Collections.Generic;
using UnityEngine;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 포탈의 한쪽 면. _sourceZone은 "이 트리거가 대표하는 출발 쪽 존".
    /// OnEnable 될 때 정적 리스트(_active)에 자신을 등록해서, 서버가 폴링으로 조회할 수 있게 함
    /// 
    /// 물리 OnTriggerEnter 대신 ServerZoneService.CheckPortals()가 매 틱 bounds로 직접 검사한다 —
    /// 원격 클라이언트는 서버 사본의 CharacterController가 항상 꺼져있어
    /// 물리 트리거 자체가 발동할 수 없기 때문(2026-08-26 발견).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ZonePortalTrigger : MonoBehaviour
    {

        [SerializeField] private ZonePortalSO _portal;
        [SerializeField] private ZoneDefinitionSO _sourceZone;
        private BoxCollider _collider;

        public ZonePortalSO Portal => _portal;
        public ZoneDefinitionSO SourceZone => _sourceZone;
        public Bounds Bounds => _collider.bounds;

        private static readonly List<ZonePortalTrigger> _active = new();
        public static IReadOnlyList<ZonePortalTrigger> Active => _active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetActiveList()
        {
            _active.Clear(); // Domain Reload OFF 환경에서도 매번 초기화
        }

        private void Awake()
        {
            _collider = GetComponent<BoxCollider>();
            _collider.isTrigger = true; // 시각적으로만 존재, 물리 충돌은 여전히 안 함(시야차단 오브젝트 겸용 대비)
        }

        private void OnEnable() // 던전 씬이 로드되면서 그 안의 PortalTrigger가 활성화됨 → OnEnable → 리스트에 자동 추가
        {
            if (_portal == null || _sourceZone == null)
            {
                Debug.LogError($"[ZonePortalTrigger] {gameObject.name} — _portal/_sourceZone 인스펙터 연결 누락", this);
                return; // _active에 등록 안 함
            }
            _active.Add(this);
        }

        private void OnDisable() => _active.Remove(this); // 던전 씬이 언로드되면 → OnDisable → 리스트에서 자동 제거

    }
}
