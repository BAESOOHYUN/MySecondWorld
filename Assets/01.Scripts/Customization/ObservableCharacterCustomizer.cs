using UnityEngine;
using VContainer;

namespace MySecondWorld.Customization
{
    // 서버 외형(SyncVar)이 바뀌면 부모가 파츠를 적용한다. 이 클래스는 DI로 필요한 테이블만 받는다.
    public class ObservableCharacterCustomizer : CharacterCustomizerBase
    {
        [Inject]
        public void Construct(
            AvatarPartKeyTableSO avatarPartKeyTable,
            CharacterAppearanceSO defaultAppearanceSO
            )
        {

            Debug.Log($"[ObservableCustomizer] Construct 호출! Table={(avatarPartKeyTable != null ? "OK" : "NULL")}, " +
                      $"DefaultSO={(defaultAppearanceSO != null ? "OK" : "NULL")}");
            // 부모의 AvatarPartKeyTableSO 및 DefaultAppearanceSO 주입 보장!
            base.Construct(avatarPartKeyTable, defaultAppearanceSO);
        }

        protected override void Start() { }  // SyncVar 훅이 담당, 기본 호출 막음
    }
}