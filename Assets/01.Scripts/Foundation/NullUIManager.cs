using UnityEngine;

namespace MySecondWorld.Network
{
    // 헤드리스 서버엔 UI가 없지만, GameNetworkManager 등의 IUIManager 의존성 해석 실패(DI 체인 전체 중단)를 막기 위한 더미 구현체
    public class NullUIManager : IUIManager
    {
        public GameObject GetOrCreateDynamicCanvas<T>() where T : Component => null;
    }
}