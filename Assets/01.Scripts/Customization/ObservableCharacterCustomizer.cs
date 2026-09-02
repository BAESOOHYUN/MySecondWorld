using MessagePipe;
using UnityEngine;
using VContainer;

namespace MySecondWorld.Customization
{
    // 외형 변경 시 이벤트 발행 → 다른 시스템이 구독(observe) 가능
    public class ObservableCharacterCustomizer : CharacterCustomizerBase
    {
        private IPublisher<CharacterAppearanceChangedEvent> _publisher;
        
        [Inject]
        public void Construct(
            AvatarPartKeyTableSO avatarPartKeyTable,
            CharacterAppearanceSO defaultAppearanceSO,
            IPublisher<CharacterAppearanceChangedEvent> publisher
            )
        {

            Debug.Log($"[ObservableCustomizer] Construct 호출! Table={(avatarPartKeyTable != null ? "OK" : "NULL")}, DefaultSO={(defaultAppearanceSO != null ? "OK" : "NULL")}");
            // 부모의 AvatarPartKeyTableSO 및 DefaultAppearanceSO 주입 보장!
            base.Construct(avatarPartKeyTable, defaultAppearanceSO);
            _publisher = publisher;
        }

        protected override void Start() { }  // SyncVar 훅이 담당, 기본 호출 막음

        protected override void OnAppearanceChanged()
            => _publisher.Publish(new CharacterAppearanceChangedEvent(_currentAppearance));
    }
}