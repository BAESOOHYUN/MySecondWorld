using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using R3;        // (구 UniRX)
using Stateless; // .NET용 상태머신 라이브러리
using MessagePipe;
using VContainer;
using System.Collections.Generic;
using MySecondWorld.Inventory.Items;

namespace MySecondWorld.AI
{
    /// <summary>
    /// 플레이어 근접 감지 → 대화 시작/종료 흐름 제어
    /// Stateless 상태 머신으로 4개 상태를 명시적으로 관리한다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NPCInteractable : MonoBehaviour
    {
        #region 상태 머신 정의
        private enum NpcState
        {
            Idle,        // 기본 상태
            InRange,     // 플레이어 근접
            Menu,        // 대화/상점 선택 메뉴
            Talking,     // 대화창 열림
            Generating,  // LLM 응답 생성 중
            Shopping,    // 상점창 열림
        }

        private enum NpcTrigger
        {
            PlayerEntered,    // 플레이어 범위 진입
            PlayerLeft,       // 범위 이탈
            Interact,         // 상호작용 키
            SelectTalk,       // 메뉴에서 대화 선택
            SelectShop,       // 메뉴에서 상점 선택
            Submit,           // 메시지 전송
            ResponseReceived, // LLM 응답 도착
            Close,            // ESC/닫기
        }
        #endregion

        #region 전이 그래프
        // Idle
        //   + PlayerEntered → InRange
        //
        // InRange
        //   + PlayerLeft    → Idle
        //   + Interact      → Menu
        //
        // Menu
        //   + PlayerLeft    → Idle
        //   + SelectTalk    → Talking
        //   + SelectShop    → Shopping
        //
        // Talking (기존과 동일, Menu를 거쳐 들어옴)
        //   + PlayerLeft    → Idle (강제 종료)
        //   + Close         → Idle
        //   + Submit        → Generating
        //
        // Generating (기존과 동일)
        //   + ResponseReceived → Talking
        //   + PlayerLeft       → Idle (응답 기다리다 강제 종료)
        //   + Close            → Idle (응답 기다리다 취소)
        //
        // Shopping
        //   + Close         → InRange
        //   + PlayerLeft    → InRange

        //   [Idle]
        //      | PlayerEntered
        //      v
        //   [InRange] <-------------------+
        //      | PlayerLeft               |
        //      |                          |
        //      +--> Interact              |
        //             v                   |
        //          [Menu]                 |
        //          | PlayerLeft           |
        //          |                      |
        //          +--> SelectTalk ------> [Talking]
        //          |                         | PlayerLeft / Close → Idle
        //          |                         | Submit → Generating -----------------→
        //          |                         v
        //          +--> SelectShop ------> [Shopping]
        //                                    | Close / PlayerLeft → InRange
        //
        //--→[Generating]
        //      | ResponseReceived → Talking
        //      | PlayerLeft / Close → Idle

        #endregion

        #region 필드
        [Header("References")]
        [SerializeField] private NPCBrain _brain;       // GUID 소스로만 사용
        [SerializeField] private GameObject _highlightUI; // 범위 진입 시 표시할 하이라이트 오브젝트
        private INPCMenuUI _menuUI;

        [Header("상점")]
        [Tooltip("이 NPC가 판매하는 아이템 목록 — 비워두면 상점 선택 시 빈 카탈로그가 열림")]
        [SerializeField] private List<EquipmentItem> _shopCatalog;

        [Header("NPC Interaction")]
        [Tooltip("NPC 레이어마스크 — 'NPC' 레이어를 지정하세요")]
        [SerializeField] private LayerMask _npcLayerMask;
        private INPCDialogueUI _dialogueUI;
        private IShopUI _shopUI;

        [Inject] private TouchEventListener _touchEventListener;

        // GUID는 NPCBrain → SystemPromptSO에서 읽는다
        private string NpcGuid
        {
            get
            {
                if (_brain == null)
                {
                    Debug.LogError($"[NPCInteractable] _brain이 null입니다. Inspector에서 NPCBrain을 연결하세요.", this);
                    return string.Empty;
                }
                return _brain.SystemPrompt?.NpcGuid ?? _brain.name;
            }
        }

        private IPublisher<string, PlayerMessageSentEvent> _messagePub;
        // MessagePipe 방식에선 응답 완료 시점을 NPCInteractable이 직접 알 수 없다.
        // 해결책: NPCResponseReceivedEvent를 NPCInteractable도 구독한다.
        // 응답이 도착하면 상태 머신을 ResponseReceived-> Talking으로 전이시킨다.
        [Inject]
        private void Construct(
            INPCDialogueUI dialogueUI,
            IShopUI shopUI,
            INPCMenuUI menuUI,
            IPublisher<string, PlayerMessageSentEvent> messagePub,
            ISubscriber<NPCResponseReceivedEvent> responseSub
            )
        {
            _dialogueUI = dialogueUI;
            _shopUI = shopUI;
            _menuUI = menuUI;
            _messagePub = messagePub;
            responseSub
                .Subscribe(evt =>
                {
                    if (evt.NpcGuid != NpcGuid)
                        return;
                    // 내부 로직이므로 Fire() (SafeFire가 아님)
                    if (_stateMachine.CanFire(NpcTrigger.ResponseReceived)) // 지금 상태에서 그 트리거를 처리할 수 있는 전이가 정의되어 있는지 확인
                        _stateMachine.Fire(NpcTrigger.ResponseReceived); // 실제로 트리거를 발동시켜 상태 전환을 수행
                })
                .AddTo(destroyCancellationToken);
        }

        private StateMachine<NpcState, NpcTrigger> _stateMachine;
        private CancellationTokenSource _cts;
        #endregion

        #region 캐싱
        // Submit 시 입력 텍스트를 상태머신에 직접 전달하기 복잡하므로,
        // 임시 필드(_pendingInput)에 저장해두고 Generating 진입 시 꺼내 사용한다.
        private string _pendingInput;
        #endregion

        #region 유니티 생명주기
        private void Awake()
        {
            _highlightUI?.SetActive(false);
            InitStateMachine();
        }

        private void Start()
        {
            if (_dialogueUI == null)
            {
                Debug.LogError($"[NPCInteractable] {name}: NPCDialogueUI이 연결되지 않았습니다. 컴포넌트를 비활성화합니다.");
                enabled = false;
                return;
            }

            _touchEventListener.OnTap.Subscribe(screenPos =>
            {
                if (Camera.main == null)
                    return;

                Ray ray = Camera.main.ScreenPointToRay(screenPos);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _npcLayerMask, QueryTriggerInteraction.Collide))
                {
                    var npc = hit.collider.GetComponentInParent<NPCInteractable>();
                    npc?.Interact();
                }
            })
            .AddTo(destroyCancellationToken);
        }
        private void OnDisable()
        {
            _brain?.OnConversationEnded();
            _dialogueUI?.Close();
            _shopUI?.Close();
            _menuUI?.Close();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            InitStateMachine(); // 나중에 다시 활성화(부활 등)될 때 Idle부터 깨끗하게 시작
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;
            _highlightUI?.SetActive(true);
            SafeFire(NpcTrigger.PlayerEntered);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;
            _highlightUI?.SetActive(false);
            SafeFire(NpcTrigger.PlayerLeft);
        }
        #endregion

        #region 상태 머신 설정
        // Configure(state)   : 상태별 동작 정의
        // Permit(trigger, s) : 트리거 → 상태 전이 허용
        // OnEntry(action)    : 상태 진입 시 실행
        // OnExit(action)     : 상태 이탈 시 실행
        // OnTransitioned     : 모든 전이 발생 시 실행
        private void InitStateMachine()
        {
            // FiringMode.Queued
            // Fire()를 큐에 쌓아두고 순서대로 하나씩 처리한다. OnEntry/OnExit 실행이 끝난 뒤 다음 트리거를 꺼낸다. 중첩 호출이 생겨도 안전하다.
            _stateMachine = new StateMachine<NpcState, NpcTrigger>(NpcState.Idle, FiringMode.Queued);

            // Idle
            _stateMachine.Configure(NpcState.Idle)
                .OnEntry(OnEnterIdle) //  C#의 메서드 그룹 변환 덕분에 .OnEntry(t => OnEnterIdle(t)) 와 완전 동일
                .Permit(NpcTrigger.PlayerEntered, NpcState.InRange);

            // InRange
            _stateMachine.Configure(NpcState.InRange)
                .OnEntry(OnEnterIdle)
                .Permit(NpcTrigger.PlayerLeft, NpcState.Idle)
                .Permit(NpcTrigger.Interact, NpcState.Menu);

            // Menu
            _stateMachine.Configure(NpcState.Menu)
                .OnEntry(OnEnterMenu)
                .OnExit(OnExitMenu)
                .Permit(NpcTrigger.PlayerLeft, NpcState.Idle)
                .Permit(NpcTrigger.Close, NpcState.InRange)
                .Permit(NpcTrigger.SelectTalk, NpcState.Talking)
                .Permit(NpcTrigger.SelectShop, NpcState.Shopping);

            // Talking
            _stateMachine.Configure(NpcState.Talking)
                .OnEntry(OnEnterTalking)
                .Permit(NpcTrigger.PlayerLeft, NpcState.Idle)
                .Permit(NpcTrigger.Close, NpcState.InRange)
                .Permit(NpcTrigger.Submit, NpcState.Generating);

            // Generating
            _stateMachine.Configure(NpcState.Generating)
                .OnEntry(OnEnterGenerating)
                .Permit(NpcTrigger.ResponseReceived, NpcState.Talking)
                .Permit(NpcTrigger.Close, NpcState.InRange)
                .Permit(NpcTrigger.PlayerLeft, NpcState.Idle);

            // Shopping
            _stateMachine.Configure(NpcState.Shopping)
                .OnEntry(OnEnterShopping)
                .Permit(NpcTrigger.Close, NpcState.InRange)
                .Permit(NpcTrigger.PlayerLeft, NpcState.InRange);

            // 전이 로그 (디버그용)
            _stateMachine.OnTransitioned(t =>
                Debug.Log($"[NPCInteractable] {t.Source} → {t.Destination} ({t.Trigger})"));
        }
        #endregion

        #region 상태 진입/이탈 처리

        // Idle 진입 시 항상 정리
        private void OnEnterIdle()
        {
            _brain?.OnConversationEnded();  // _cts.Cancel() 전 — destroyCancellationToken 살아있어야 함
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _dialogueUI?.Close();
            _shopUI?.Close();

            OnConversationStateChanged(false);
        }

        private void OnEnterMenu()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            _menuUI.Open();
            _menuUI.OnSelectTalk.Subscribe(_ => SafeFire(NpcTrigger.SelectTalk)).AddTo(_cts.Token);
            _menuUI.OnSelectShop.Subscribe(_ => SafeFire(NpcTrigger.SelectShop)).AddTo(_cts.Token);
            _menuUI.OnClose.Subscribe(_ => SafeFire(NpcTrigger.Close)).AddTo(_cts.Token);

            OnConversationStateChanged(true);
        }

        private void OnExitMenu() => _menuUI.Close();

        // Talking에 진입하는 경로는 두 개다.
        // Interact         → 처음 대화창 열기
        // ResponseReceived → LLM 응답 받고 돌아오기
        private void OnEnterTalking(StateMachine<NpcState, NpcTrigger>.Transition t)
        {
            if (t.Trigger == NpcTrigger.SelectTalk)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                _dialogueUI.Open(NpcGuid);

                // R3 구독 — _cts 해제 시 자동으로 함께 해제된다
                _dialogueUI.OnSubmit
                    .Subscribe(OnPlayerSubmit)
                    .AddTo(_cts.Token);

                _dialogueUI.OnClose
                    .Subscribe(_ => SafeFire(NpcTrigger.Close))
                    .AddTo(_cts.Token);

                _dialogueUI.SetInputEnabled(true);
            }
            else if (t.Trigger == NpcTrigger.ResponseReceived)
            {
                // Generating에서 돌아옴 — 입력만 다시 활성화
                _dialogueUI.SetInputEnabled(true);
            }
        }

        // Generating은 진입 경로가 Submit 하나다. 진입 경로가 하나뿐이므로 파라미터 없는 버전을 쓴다.
        // 나가는 경로는 세 개다. 그러나 특별한 정리작업이 없으므로 OnExit은 따로 작성하지 않는다.
        private void OnEnterGenerating()
        {
            // _pendingInput Submit 시 저장해둔 값을 사용한다. (캐싱)
            if (_pendingInput == null)
                return;

            _dialogueUI.SetInputEnabled(false);
            _dialogueUI.ShowThinking();

            _messagePub.Publish(NpcGuid, new PlayerMessageSentEvent(_pendingInput, MySecondWorld.Network.PlayerSession.LocalPlayerName, _cts.Token));
            _pendingInput = null;
        }


        // Shopping은 진입 경로가 SelectShop 하나뿐이라 항상 cts를 새로 만든다.
        // 나가면(Close/PlayerLeft → InRange) OnEnterIdle이 InRange의 OnEntry로도 걸려있어 cts 정리 + _shopUI.Close()까지 같이 처리된다.
        private void OnEnterShopping()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            _shopUI.Open(_shopCatalog);
            _shopUI.OnClose
                .Subscribe(_ => SafeFire(NpcTrigger.Close))
                .AddTo(_cts.Token);
        }

        // companion이 대화 중에 딴 데로 걸어가버리는 걸 막기 위한 훅
        // "대화창이 열려있냐/닫혀있냐" 라는 UI 레벨 상태로, 이걸 CompanionAI한테 알려줘서 이동을 잠깐 멈추게 하는 용도
        protected virtual void OnConversationStateChanged(bool paused) { }
        
        #endregion

        #region 입력 처리
        private void OnPlayerSubmit(string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput))
                return;
            _pendingInput = userInput;
            _dialogueUI.ClearInput();
            SafeFire(NpcTrigger.Submit);
        }

        // 클릭/탭으로 외부에서 직접 호출 (TouchEventListener의 Raycast 결과)
        public virtual void Interact()
        {
            if (OnUIChecker.IsInputFieldFocused())
                return;
            SafeFire(NpcTrigger.Interact);
        }
        #endregion

        #region SafeFire: 불가능한 전이는 무시
        // Stateless 기본 동작: 허용되지 않은 트리거를 Fire() 하면 예외 발생.
        // 게임에서는 입력이 동시에 들어올 수 있어 이런 예외가 정상적인 상황에서도 발생할 수 있다.
        // → 예외로 게임이 터지는 걸 막기 위해 SafeFire()를 사용.
        //
        // SafeFire(trigger):
        //   - 현재 상태에서 전이가 가능하면 Fire(trigger) 실행
        //   - 불가능하면 예외 대신 로그만 찍고 무시
        //
        // 사용 원칙:
        //   - 외부 입력(플레이어 조작, 버튼, ESC 등) → SafeFire()
        //   - 내부 로직(LLM 응답 등) → Fire()
        //     → 내부 로직에서 Fire가 실패하면 진짜 버그로 간주한다.
        private void SafeFire(NpcTrigger trigger)
        {
            if (_stateMachine.CanFire(trigger))
                _stateMachine.Fire(trigger);
            else
                Debug.Log($"[NPCInteractable] 전이 무시: {_stateMachine.State} + {trigger}");
        }
        #endregion
    }
}
