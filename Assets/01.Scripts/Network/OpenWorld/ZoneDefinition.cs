using UnityEngine;
using Mirror;

namespace MySecondWorld.Network
{
    /// <summary>
    /// "이 좌표 범위가 이 씬이다"라는 지도 데이터.
    /// </summary>
    [CreateAssetMenu(fileName = "NewZone", menuName = "Network/Zone Definition")]
    public class ZoneDefinition : ScriptableObject
    {
        [Scene] public string sceneName;    // 이 존에 대응하는 씬 경로
        public Bounds bounds;               // 씬의 정확한 월드 영역 (플레이어 소속 판정용)
        public float preloadMargin = 20f;   // bounds 바깥으로 이만큼 확장한 영역 = 미리 로드
    }
}