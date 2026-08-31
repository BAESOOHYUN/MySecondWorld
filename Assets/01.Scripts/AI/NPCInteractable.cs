using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using R3;        // (구 UniRX)
using Stateless; // .NET용 상태머신 라이브러리
using MessagePipe;
using VContainer;
using System.Collections.Generic;
using MySecondWorld.Inventory.Items;
using MySecondWorld.Interaction;

namespace MySecondWorld.AI
{
    /// <summary>
    /// 플레이어 근접 감지 → 대화 시작/종료 흐름 제어
    /// Stateless 상태 머신으로 4개 상태를 명시적으로 관리한다.
    /// </summary>
    public class NPCInteractable : MonoBehaviour, ITapTargetable
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

        #region 필드 — Inspector 참조
        [Header("References")]
        [SerializeField] private NPCBrain _brain;       // GUID 소스로만 사용
        [SerializeField] private GameObject _highlightUI; // 범위 진입 시 표시할 하이라이트 오브젝트

        [Header("상점")]
        [Tooltip("이 NPC가 판매하는 아이템 목록 — 비워두면 상점 선택 시 빈 카탈로그가 열림")]
        [SerializeField] private List<EquipmentItem> _shopCatalog;
        #endregion

        #region 필드 — 주입 의존성

        // 아래 것들은 Construct(초기화 region)가 채운다.
        private INPCMenuUI _menuUI;
        private INPCDialogueUI _dialogueUI;
        private IShopUI _shopUI;
        private IPublisher<string, PlayerMessageSentEvent> _messagePub;
        #endregion

        #region 필드 — 상태
        // 전역 공유 — 지금 공유 UI(Menu/Dialogue/Shop)를 차지하고 있는 NPC.
        // 인스턴스 상태가 아니라서 맨 위에 따로 둔다.
        private static NPCInteractable _activeInteraction;

        // Domain Reload가 꺼져 있어 static이 플레이 세션을 넘어 살아남는다.
        // 파괴된 객체는 Unity의 fake-null 덕에 자연 복구되지만, 그건 우연이지 보장이 아니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _activeInteraction = null;

        private StateMachine<NpcState, NpcTrigger> _stateMachine;
        private CancellationTokenSource _cts;
        private string _pendingInput;      // Submit 시 입력 텍스트를 임시 저장(Generating 진입 시 꺼내 씀)
        private bool _playerInProximity;   // 플레이어가 지금 물리적으로 범위 안에 있는지(ForceReset 후 즉시 복구용)
        #endregion

        #region 식별자
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
        #endregion

        #region 초기화
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
                    if (_stateMachine.CanFire(NpcTrigger.ResponseReceived))
                        _stateMachine.Fire(NpcTrigger.ResponseReceived);
                })
                .AddTo(destroyCancellationToken);
        }
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

            if (_dialogueUI == null)
            {
                Debug.LogError($"[NPCInteractable] {name}: NPCDialogueUI이 연결되지 않았습니다. 컴포넌트를 비활성화합니다.");
                enabled = false;
                return;
            }

            // 판정 캡슐은 이미 있는 몸통 콜라이더를 그대로 베껴 만든다.
            BuildTapCollider();
        }

        private void OnDisable()
        {
            ForceReset();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        #endregion

        #region 근접 트리거 감지
        // Trigger 감지 (TriggerRelay가 자식 오브젝트에서 대신 받아서 호출)
        // 코드 참조가 아니라 각 NPC/Companion 프리팹의 TriggerRelay.OnTriggerEntered/OnTriggerExited(UnityEvent)
        // 슬롯에 인스펙터로 직접 연결돼있음 — Find References로는 호출부가 안 잡힘, 프리팹에서 확인해야 함.

        // 플레이어 몸에 콜라이더가 여러 개면(CharacterController + Collider 등) Enter/Exit가 각각 들어온다.
        // 개수를 세지 않으면 하나만 빠져나가도 "범위 이탈"로 처리돼, 반경 안에 서 있는데도 Idle로 떨어진다.
        private readonly HashSet<Collider> _playerColliders = new();

        public void HandlePlayerTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            // 첫 콜라이더가 들어올 때만 상태를 바꾼다.
            if (!_playerColliders.Add(other) || _playerColliders.Count > 1) return;

            _playerInProximity = true;
            _highlightUI?.SetActive(true);
            SafeFire(NpcTrigger.PlayerEntered);
        }

        public void HandlePlayerTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            // 마지막 하나가 나갈 때만 이탈로 친다.
            if (!_playerColliders.Remove(other) || _playerColliders.Count > 0) return;

            _playerInProximity = false;
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
                Debug.Log($"[NPCInteractable] {name}: {t.Source} → {t.Destination} ({t.Trigger})", this));
        }
        #endregion

        #region 상태 진입/이탈 처리

        // Idle 진입 시 항상 정리
        private void OnEnterIdle()
        {
            if (_activeInteraction == this)
                _activeInteraction = null;

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
            // 새 NPC가 Menu에 진입하는 순간, 이전에 활성화돼있던 다른 NPC를 강제로 정리
            if (_activeInteraction != null && _activeInteraction != this)
                _activeInteraction.ForceReset();
            _activeInteraction = this;

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

        // 다른 NPC가 공유 UI를 새로 차지할 때(_activeInteraction 경유), 또는 자기 자신이 비활성화될 때(OnDisable) 호출되는 강제 정리
        public void ForceReset()
        {
            if (_activeInteraction == this)
                _activeInteraction = null;

            _playerColliders.Clear();

            _brain?.OnConversationEnded();
            _dialogueUI?.Close();
            _shopUI?.Close();
            _menuUI?.Close();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            InitStateMachine(); // Idle부터 깨끗하게 재시작

            if (_playerInProximity)
                SafeFire(NpcTrigger.PlayerEntered); // 플레이어가 계속 범위 안에 있었으면 즉시 InRange로 복귀
        }

        #endregion

        #region ITapTargetable
        public Transform Anchor => transform;

        // NPC는 몬스터와 달리 사망 상태가 없다. 켜져 있으면 항상 고를 수 있다.
        public bool IsSelectable => isActiveAndEnabled;

        // 판정 규칙만 리졸버로 통일하고, 탭 이후 동작은 기존 그대로(즉시 Menu)를 유지한다.
        public void OnTapped() => Interact();

        // NPC/컴패니언은 몸통 콜라이더가 이미 있으므로 그 치수를 그대로 쓴다.
        // 손으로 재면 프리팹마다 어긋나고, 렌더러 바운드로 재면 상자가 몸보다 커진다
        // (컴패니언이 옆 NPC의 클릭을 가로채던 원인).
        private void BuildTapCollider()
        {
            int layer = LayerMask.NameToLayer("TapTarget");
            if (layer < 0)
            {
                Debug.LogError($"[NPCInteractable] 'TapTarget' 레이어가 없습니다 — {name}", this);
                return;
            }

            var go = new GameObject("TapCollider");
            go.transform.SetParent(transform, false);
            go.layer = layer;

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;

            if (TryGetComponent(out CharacterController cc))
            {
                capsule.center = cc.center;
                capsule.radius = cc.radius;
                capsule.height = cc.height;
            }
            else if (TryGetComponent(out CapsuleCollider body))
            {
                capsule.center = body.center;
                capsule.radius = body.radius;
                capsule.height = body.height;
            }
            else
            {
                Debug.LogWarning($"[NPCInteractable] {name}: 몸통 콜라이더를 못 찾아 기본 크기 사용", this);
                capsule.center = new Vector3(0f, 0.9f, 0f);
                capsule.radius = 0.3f;
                capsule.height = 1.8f;
            }
        }
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

        // 탭 판정의 승자로 뽑혔을 때 OnTapped가 부른다. UI 버튼 등 외부에서 직접 불러도 된다.
        public virtual void Interact()
        {
            //Debug.Log($"[NPCInteractable] {name}: Interact 진입 — 상태={_stateMachine.State}, " +
            //          $"입력필드포커스={OnUIChecker.IsInputFieldFocused()}", this);
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
                Debug.Log($"[NPCInteractable] {name}: 전이 무시: {_stateMachine.State} + {trigger}", this);
        }

        #endregion
    }
}
