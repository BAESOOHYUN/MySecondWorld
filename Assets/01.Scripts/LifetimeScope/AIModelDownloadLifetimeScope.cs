using UnityEngine;
using VContainer;
using VContainer.Unity;
using MessagePipe;
using MySecondWorld.AI;
using MySecondWorld.UI;
/// <summary>
/// OpenWorldRobby씬 GameObject에 부착
/// </summary>
public class AIModelDownloadLifetimeScope : LifetimeScope
{
    [SerializeField] private AIModelConfigSO _modelConfig;

    protected override void Configure(IContainerBuilder builder)
    {
#if UNITY_SERVER
    return; // 서버 빌드에서는 LLM/STT 다운로드 자체가 불필요
#endif
        var options = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<AIModelDownloadProgressEvent>(options);
        builder.Register<AIModelDownloader>(Lifetime.Singleton);
        builder.RegisterEntryPoint<AIModelDownloadStarter>();
        var uiManager = Parent.Container.Resolve<UIManager>();
        var uiGO = uiManager.GetOrCreateDynamicCanvas<AIModelDownloadUI>();
        builder.RegisterComponent(uiGO.GetComponent<AIModelDownloadUI>());
        builder.RegisterInstance(_modelConfig);
    }
}

// builder.RegisterMessageBroker<T>(options);        // MessagePipe 이벤트 채널 등록 (Publish/Subscribe 쌍 생성)
// builder.Register<TInterface, TImpl>(Lifetime.X);  // 순수 C# 클래스를 VContainer가 new로 생성해서 등록. T = Type(타입), Impl = Implementation(구현체)
// builder.RegisterEntryPoint<T>();                  // IStartable/ITickable 등 유니티 생명주기 훅 실행용 등록
// Parent.Container.Resolve<T>();                    // Configure() 중 부모 컨테이너에서 이미 등록된 인스턴스 꺼내기
// builder.RegisterComponent(instance);              // 씬에 이미 있는 컴포넌트(MonoBehaviour)를 그대로 등록
// builder.RegisterComponentInHierarchy<T>();        // 씬 Hierarchy에서 T 컴포넌트를 자동 탐색해서 등록
// builder.RegisterInstance(obj);                    // new 없이 외부에서 만들어진 객체(SO 등)를 그대로 등록

// RegisterComponent  → MonoBehaviour (씬 컴포넌트)
// RegisterInstance   → 일반 객체, ScriptableObject (씬 컴포넌트 아님)