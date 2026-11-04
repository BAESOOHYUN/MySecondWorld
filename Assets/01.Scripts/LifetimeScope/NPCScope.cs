using VContainer;
using VContainer.Unity;
using MySecondWorld.AI.Voice;
using MySecondWorld.Customization;
using UnityEngine;

namespace MySecondWorld.AI
{
    /// <summary>
    /// SubScenes에 배치된 NPC에게 직접 부착
    /// </summary>
    public class NPCScope : LifetimeScope
    {
        [SerializeField] private CharacterAppearanceSO _appearanceSO; // NPC별로 다른 SO를 인스펙터에서 지정

        protected override LifetimeScope FindParent()
        {
            var parent = Find<NPCLifetimeScope>();
            Debug.Log($"[NPCScope] {name}: FindParent -> {(parent != null ? parent.name : "NULL")}");
            return parent;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            Debug.Log($"[NPCScope] {name}: Configure 시작");
            var brain = GetComponent<NPCBrain>();
            var interactable = GetComponent<NPCInteractable>();
            var voicePlayer = GetComponent<NPCVoicePlayer>();
            var lipSync = GetComponent<NPCLipSyncController>();
            var customizer = GetComponent<NPCCharacterCustomizer>();
            builder.RegisterComponent(brain);
            builder.RegisterComponent(interactable);
            builder.RegisterComponent(voicePlayer);
            builder.RegisterComponent(lipSync);
            builder.RegisterComponent(customizer);

            // NPCSystemPromptSO를 컨테이너에 별도 타입으로 추가 등록 → NPCVoicePlayer [Inject]에 자동 주입
            if (brain != null && brain.SystemPrompt != null)
                builder.RegisterInstance(brain.SystemPrompt);
            else
                Debug.LogWarning($"[NPCScope] {name}: NPCBrain 또는 SystemPrompt가 null입니다.");

            // 외형 커스터마이저 + NPC별 SO
            if (_appearanceSO != null)
                builder.RegisterInstance(_appearanceSO);

            // ServerMode 판별 및 LLM 등록
            IDeviceCapability deviceCapability = null;
            if (Parent != null && Parent.Container != null)
            {
                try
                {
                    deviceCapability = Parent.Container.Resolve<IDeviceCapability>();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[NPCScope] {name}: IDeviceCapability Resolve 예외 (기본 ServerMode 적용): {ex.Message}");
                }
            }

            bool useServerMode = deviceCapability?.UseServerMode ?? true;
            Debug.Log($"[NPCScope] {name}: LLM 등록 -> UseServerMode={useServerMode}");
            if (useServerMode)
                builder.Register<GroqLLMService>(Lifetime.Scoped).As<ILLMService>();
            else
            {
                var localLLM = gameObject.AddComponent<LocalLLMService>();
                builder.RegisterInstance(localLLM).As<ILLMService>();
            }

            // TTS: Typecast. NPC별 보이스 ID는 NPCSystemPromptSO에서 받는다
            var tts = gameObject.AddComponent<TypecastTTSService>();
            tts.Init(brain != null && brain.SystemPrompt != null ? brain.SystemPrompt.VoiceId : null);
            builder.RegisterInstance(tts).As<ITTSService>();
        }
    }
}
