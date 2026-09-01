using UnityEngine;
using UnityEngine.Events;

namespace MySecondWorld.Utils
{
    // 자식 오브젝트의 콜라이더를 부모와 다른 Layer/Tag로 분리해야 할 때 쓴다
    // (Unity의 Layer/Tag는 GameObject 단위라, 한 오브젝트에 콜라이더가 여러 개면
    // 전부 같은 값을 가질 수밖에 없어서 분리하려면 자식 오브젝트가 필요함).
    // 트리거 이벤트를 UnityEvent로 중계 — 인스펙터에서 원하는 대상/메서드에 직접 연결한다.
    public class TriggerRelay : MonoBehaviour
    {
        public UnityEvent<Collider> OnTriggerEntered;
        public UnityEvent<Collider> OnTriggerExited;
        private void OnTriggerEnter(Collider other) => OnTriggerEntered?.Invoke(other);
        private void OnTriggerExit(Collider other) => OnTriggerExited?.Invoke(other);
    }
}