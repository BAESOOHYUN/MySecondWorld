using UnityEngine;
using UnityEngine.AddressableAssets;

public static class AddressablesBootstrap 
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        // 캐시 삭제 요청이 있으면 앱 시작 시 처리
        if (PlayerPrefs.GetInt("ClearCacheOnStart", 0) == 1)
        {
            Caching.ClearCache();
            PlayerPrefs.DeleteKey("ClearCacheOnStart");
            Debug.Log("[Addressables] 캐시 삭제 완료 (앱 시작 시 처리)");
        }

        // 목적 : Addressables 정적 상태 초기화 트리거
        Addressables.InitializeAsync();
    }
}