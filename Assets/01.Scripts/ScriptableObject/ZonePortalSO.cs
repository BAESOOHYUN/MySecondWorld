using UnityEngine;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 월드 간 전환 지점. 씬 오브젝트가 아니라 데이터 — volume에 들어오면
    /// 현재 World의 반대편(zoneA/zoneB 중 아닌 쪽)으로 전환한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPortal", menuName = "Scene/Zone Portal")]
    public class ZonePortalSO : ScriptableObject
    {
        public Bounds volume;       // 통과 영역 (월드 좌표) — 입구 계단 부근
        public ZoneDefinitionSO zoneA;
        public ZoneDefinitionSO zoneB;
    }
}
