using UnityEngine;
using MySecondWorld.Utils;

namespace MySecondWorld.Network.OpenWorld
{
    /// <summary>
    /// ZoneHandler의 자식 오브젝트에 붙이는 트리거.
    /// exitAllDungeons = true 전용. 던전 출구에서 Dungeon/ 씬 전체를 언로드한다.
    /// 일반 양방향 전환에는 사용하지 않는다 (ZoneHandler OnTriggerExit가 언로드 담당).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class UnloadTrigger : MonoBehaviour
    {
        private ZoneHandler _zone;

        private void Awake()
        {
            _zone = GetComponentInParent<ZoneHandler>();
        }

        private void OnTriggerEnter(Collider other) => _zone.OnUnloadEnter(other);

        private void OnDrawGizmos()         => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, new Color(1f, 0.4f, 0f), 0.15f);
        private void OnDrawGizmosSelected() => GizmoUtils.DrawCollider(GetComponent<Collider>(), transform, new Color(1f, 0.4f, 0f), 0.35f);
    }
}
