using UnityEngine;
using UnityEngine.AddressableAssets;

public static class AddressablesBootstrap 
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        // 목적 : Addressables 정적 상태 초기화 트리거
        Addressables.InitializeAsync();
    }
}