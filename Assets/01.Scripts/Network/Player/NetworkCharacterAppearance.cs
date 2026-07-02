using Cysharp.Threading.Tasks;
using UnityEngine;
using Mirror;
using VContainer;
using MessagePipe;
using MySecondWorld.Customization;

namespace MySecondWorld.Network
{
    public class NetworkCharacterAppearance : NetworkBehaviour
    {
        private const string PrefKeyCustomized = "CharacterCustomized";

        [SyncVar(hook = nameof(OnAppearanceChanged))]
        private CharacterAppearanceData _appearance;

        private ObservableCharacterCustomizer _customizer;
        private AvatarPartKeyTableSO _avatarPartKeyTable;
        private IPublisher<RequestOpenCustomizeUIEvent> _openUIPublisher;

        [Inject]
        public void Construct(
            ObservableCharacterCustomizer customizer,
            AvatarPartKeyTableSO avatarPartKeyTable,
            IObjectResolver resolver)
        {
            _customizer = customizer;
            _avatarPartKeyTable = avatarPartKeyTable;

            // PlayerScope도 RegisterMessagePipe()를 호출하기 때문에(자기 전용 이벤트용),
            // MessagePipe의 open-generic MessageBrokerCore<> 폴백(VContainer 1.14+에서
            // versionDefines로 자동 활성화)이 PlayerScope 로컬 레지스트리에도 생겨서
            // closed 타입 resolve 요청을 가로채 버린다. 그래서 평범한 [Inject]로
            // ISubscriber<CharacterAppearanceSyncRequestedEvent>를 받으면 PlayerScope가
            // 즉석에서 만든, 아무도 발행하지 않는 로컬 인스턴스를 구독하게 된다.
            // ProjectLifetimeScope가 소유한 진짜 공유 브로커를 받으려면 Root에서 직접 resolve해야 한다.
            var saveRequestSub = resolver is IScopedObjectResolver scoped
                ? scoped.Root.Resolve<ISubscriber<CharacterAppearanceSyncRequestedEvent>>()
                : resolver.Resolve<ISubscriber<CharacterAppearanceSyncRequestedEvent>>();

            var openUISub = resolver is IScopedObjectResolver scoped2
                ? scoped2.Root.Resolve<IPublisher<RequestOpenCustomizeUIEvent>>()
                : resolver.Resolve<IPublisher<RequestOpenCustomizeUIEvent>>();
                
            _openUIPublisher = openUISub;

            // isLocalPlayer로 필터링하는 이유:
            // 이 broker는 ProjectLifetimeScope에 등록되어 화면에 보이는
            // 모든 NetworkCharacterAppearance 인스턴스(다른 플레이어 캐릭터 포함)가 함께 구독한다.
            // Mirror가 권한 없는 CmdSetAppearance 호출을 내부적으로 막아주므로(SendCommandInternal)
            // isLocalPlayer 필터링 없이도 잘못된 동기화는 안 생기지만,
            // 보이는 플레이어 수만큼 "without authority" 경고 로그 + 헛호출이 매번 발생한다.
            // 그걸 막기 위해 로컬 플레이어 본인 객체일 때만 호출한다.
            saveRequestSub.Subscribe(evt =>
            {
                if (!isLocalPlayer) return;
                CmdSetAppearance(evt.Data);
            }).AddTo(destroyCancellationToken);
        }

        [Command]
        public void CmdSetAppearance(CharacterAppearanceData data)
        {
            var t = _avatarPartKeyTable;

            // IsDefined 이 값이 enum에 실제로 정의된 값인지 검증 (해킹 등으로 이상한 값 들어올 가능성 방어)
            if (!System.Enum.IsDefined(typeof(Gender), data.Gender))
                data.Gender = _appearance.Gender;

            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                int count = t.GetCount(part, data.Gender);
                data[part] = Clamp(data[part], count);
            }

            _appearance = data; // 여기서 대입하며 [SyncVar] 훅 발동 → OnAppearanceChanged 실행
        }

        private void OnAppearanceChanged(CharacterAppearanceData old, CharacterAppearanceData next)
        {
            _customizer.ApplyAppearance(next).Forget();
        }

        private static int Clamp(int value, int count)
            => value >= 0 && value < count ? value : (count > 0 ? 0 : -1);

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            if (!PlayerPrefs.HasKey(PrefKeyCustomized))
                _openUIPublisher.Publish(new RequestOpenCustomizeUIEvent());
        }
    }
}

//[Client] CharacterCustomizeUI
//   │  CmdSetAppearance(data)
//   ▼  [Command] RPC
//[Server] NetworkCharacterAppearance.CmdSetAppearance(data)
//   │  Gender 검증 +25개 필드 클램프
//   │  _appearance = data   (SyncVar 대입)
//   ▼
//[Mirror] SyncVar 변경 감지 → 모든 클라이언트에 자동 브로드캐스트
//   ▼
//[All Clients] NetworkCharacterAppearance.OnAppearanceChanged(old, next)
//   │  (SyncVar hook)
//   ▼
//   _customizer.ApplyAppearance(next)  → 시각 반영

