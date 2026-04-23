using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using R3;
using ReactiveInputSystem;
using Stateless;
using MessagePipe;
using VContainer;
using MySecondWorld.UI;

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
            Idle,        // 아무것도 안 하는 상태
            InRange,     // 플레이어가 근처에 있는 상태
            Talking,     // 대화창이 열린 상태
            Generating,  // LLM이 응답 생성 중인 상태
        }

        private enum NpcTrigger
        {
            PlayerEntered,    // 플레이어가 Trigger 범위에 들어옴
            PlayerLeft,       // 플레이어가 Trigger 범위를 벗어남
            Interact,         // 상호작용 키를 누름
            Submit,           // 메시지를 전송함
            ResponseReceived, // LLM 응답이 도착함
            Close,            // ESC 또는 닫기 버튼
        }
        #endregion

        #region 전이 그래프
        //    Idle
        //        + PlayerEntered → InRange

        //    InRange
        //        + PlayerLeft    → Idle
        //        + Interact      → Talking

        //    Talking
        //        + PlayerLeft    → Idle(강제 종료)
        //        + Close         → Idle
        //        + Submit        → Generating

        //    Generating
        //        + ResponseReceived → Talking
        //        + PlayerLeft       → Idle(응답 기다리다 강제 종료)
        //        + Close            → Idle(응답 기다리다 취소)


        //            PlayerEntered              Interact
        //     [Idle]───────────────>[InRange]───────────────>[Talking]
        //      ▲                       │                       │  ▲
        //      │       PlayerLeft      │                       │  │
        //      │◄──────────────────────┘                       │  │
        //      │                                               │  │
        //      │       PlayerLeft / Close                      │  │ ResponseReceived
        //      │◄──────────────────────────────────────────────│  │
        //      │                                       Submit  │  │
        //      │       PlayerLeft / Close     ▼────────────────┘  │
        //      │◄─────────────────────────[Generating]────────────┘
        #endregion

        #region 필드
        [Header("References")]
        [SerializeField] private NPCBrain _brain;       // GUID 소스로만 사용
        [SerializeField] private GameObject _highlightUI; // 범위 진입 시 표시할 하이라이트 오브젝트
        [Header("NPC Interaction")]
        [Tooltip("NPC 레이어마스크 — 'NPC' 레이어를 지정하세요")]
        [SerializeField] private LayerMask _npcLayerMask;
        private NPCDialogueUI _dialogueUI;
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
        // 기존에는 SendMessageToNPCAsync에서 await _brain.GenerateResponseAsync(userInput, ct); 하여 
        // Generating → Talking 전이 시점을 알 수 있었다. (응답완료 시점에 SafeFire 해줌)
        // MessagePipe로 바꾸면 응답 완료 시점을 NPCInteractable이 직접 알 수 없다.
        // 해결책: NPCResponseReceivedEvent를 NPCInteractable도 구독한다. 응답이 도착하면 상태 머신을 Talking으로 전이시킨다.
        [Inject]
        private void Construct(
            NPCDialogueUI dialogueUI,
            IPublisher<string, PlayerMessageSentEvent> messagePub,
            ISubscriber<NPCResponseReceivedEvent> responseSub
            )
        {
            _dialogueUI = dialogueUI;
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

        [Header("Input")]
        [Tooltip("New Input System — 상호작용 액션 (InputAction)")]
        public InputActionReference interactAction;
        [Tooltip("New Input System — ESC 키 처리")]
        public InputActionReference cancelAction;

        private StateMachine<NpcState, NpcTrigger> _stateMachine;
        private CancellationTokenSource _cts;
        #endregion

        #region 캐싱
        // Submit 트리거와 실제 텍스트를 동시에 전달할 방법이 없으므로 필드에 임시 보관한다.
        // Stateless의 Fire()는 파라미터를 받는 오버로드가 있지만 OnEntry가 아니라 OnEntryFrom 을 써야 하고, SetTriggerParameters로 미리 등록해야 한다.
        // 나중에 진입 경로가 늘어나면 OnEntryFrom을 여러 개 써야 하고, 각각 다른 파라미터 타입을 처리해야 한다.
        // 복잡해지므로(보일러플레이트 증가) 여기서는 단순하게 필드 방식을 사용한다.
        private string _pendingInput;
        #endregion

        #region 초기화 검증
        private void Start()
        {
            if (_dialogueUI == null)
            {
                Debug.LogError($"[NPCInteractable] {name}: NPCDialogueUI이 연결되지 않았습니다. 컴포넌트를 비활성화합니다.");
                enabled = false;
                return;
            }

            InitStateMachine();

            if (interactAction != null)
            {
                interactAction.action.performed += OnInteractPerformed;
            }
            else
            {
                Debug.LogWarning($"[NPCInteractable] Start: interactAction이 null입니다. Inspector에서 연결 필요.");
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
        #endregion

        #region 입력 등록
        private void OnEnable()
        {
            interactAction?.action.Enable();
            cancelAction?.action.Enable();
        }

        private void OnDisable()
        {
            interactAction?.action.Disable();
            cancelAction?.action.Disable();
        }

        private void OnDestroy()
        {
            if (interactAction != null)
                interactAction.action.performed -= OnInteractPerformed;
            _cts?.Cancel();
            _cts?.Dispose();
        }
        #endregion

        #region Trigger 감지
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

        // Configure(state)                  "이 상태를 어떻게 동작시킬지 설정하는 빌더를 반환한다."
        // Permit(trigger, destinationState) "이 트리거가 발생하면 저 상태로 이동을 허용한다."
        // OnEntry(action)                   "이 상태에 진입하는 순간 딱 한 번 실행된다." transition.Trigger로 어떤 트리거로 진입했는지도 알 수 있다.
        // OnExit(action)                    "이 상태를 빠져나가는 순간 딱 한 번 실행된다."
        // OnTransitioned(action)            "모든 상태 전이가 일어날 때마다 실행되는 전역 훅이다."
        private void InitStateMachine()
        {
            // FiringMode.Queued
            // Fire(Submit) 호출 → 큐에 적재
            //    → OnExit(Talking) 실행
            //    → OnEntry(Generating) 실행
            //    → 실행 완료 후 큐 확인
            //        → 다음 트리거 꺼내서 처리
            // Fire()를 큐에 쌓아두고 순서대로 하나씩 처리한다. OnEntry/OnExit 실행이 끝난 뒤 다음 트리거를 꺼낸다. 중첩 호출이 생겨도 안전하다.
            _stateMachine = new StateMachine<NpcState, NpcTrigger>(NpcState.Idle, FiringMode.Queued);

            // Idle
            _stateMachine.Configure(NpcState.Idle)
                .OnEntry(OnEnterIdle)
                .Permit(NpcTrigger.PlayerEntered, NpcState.InRange);

            // InRange
            _stateMachine.Configure(NpcState.InRange)
                .OnEntry(OnEnterIdle)
                .Permit(NpcTrigger.PlayerLeft, NpcState.Idle)
                .Permit(NpcTrigger.Interact, NpcState.Talking);

            // Talking
            _stateMachine.Configure(NpcState.Talking)
                .OnEntry(OnEnterTalking) //  C#의 메서드 그룹 변환 덕분에 .OnEntry(t => OnEnterTalking(t)) 와 완전 동일
                .Permit(NpcTrigger.PlayerLeft, NpcState.Idle)
                .Permit(NpcTrigger.Close, NpcState.InRange)
                .Permit(NpcTrigger.Submit, NpcState.Generating);

            // Generating
            _stateMachine.Configure(NpcState.Generating)
                .OnEntry(OnEnterGenerating)
                .Permit(NpcTrigger.ResponseReceived, NpcState.Talking)
                .Permit(NpcTrigger.Close, NpcState.InRange)
                .Permit(NpcTrigger.PlayerLeft, NpcState.Idle);

            // 전이 로그 (디버그용)
            _stateMachine.OnTransitioned(t =>
                Debug.Log($"[NPCInteractable] {t.Source} → {t.Destination} ({t.Trigger})"));
        }
        #endregion

        #region 상태 진입/이탈 처리

        // Idle 진입 시 항상 정리
        private void OnEnterIdle()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _dialogueUI.Close();
        }

        // Talking에 진입하는 경로는 두 개다.
        // Interact         → 처음 대화창 열기
        // ResponseReceived → LLM 응답 받고 돌아오기
        private void OnEnterTalking(StateMachine<NpcState, NpcTrigger>.Transition t)
        {
            if (t.Trigger == NpcTrigger.Interact)
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

                cancelAction?.action
                    .PerformedAsObservable()
                    .AsUnitObservable()
                    .Subscribe(_ => SafeFire(NpcTrigger.Close))
                    .AddTo(_cts.Token);

                _dialogueUI.SetInputEnabled(true);
            }
            else if (t.Trigger == NpcTrigger.ResponseReceived)
            {
                // 이제 NPCDialogueUI가 NPCResponseReceivedEvent를 직접 구독하므로
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

            _messagePub.Publish(NpcGuid, new PlayerMessageSentEvent(_pendingInput, _cts.Token));
            _pendingInput = null;
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

        private void OnInteractPerformed(InputAction.CallbackContext ctx)
        {
            if (OnUIChecker.IsInputFieldFocused())
                return;
            SafeFire(NpcTrigger.Interact);
        }

        // 클릭/탭으로 외부에서 직접 호출 (TouchEventListener의 Raycast 결과)
        public void Interact()
        {
            if (OnUIChecker.IsInputFieldFocused())
                return;
            SafeFire(NpcTrigger.Interact);
        }
        #endregion

        #region SafeFire: 불가능한 전이는 무시

        // Stateless는 기본적으로 불가능한 전이 시 예외를 던진다.
        // 예: Idle 상태에서 Close 발생 같은 경쟁 조건 방어.
        // 허용되지 않은 전이 → InvalidOperationException 던짐

        // 게임에서는 플레이어 입력이 동시다발로 들어오는 상황이 생긴다. 이건 버그가 아니라 타이밍 문제.
        // 예외로 처리하면 게임이 터진다. 예외 대신 조용히 무시한다

        // 중요한 건 SafeFire()를 아무데나 남발하면 안 된다는 점이다.
        //   외부 입력 (Trigger, 버튼, ESC) → SafeFire()  ← 타이밍 제어 불가
        //   내부 로직 (LLM 응답 후 등)    → Fire()       ← 항상 올바른 상태여야 함
        // 내부 로직에서 Fire()가 예외를 던졌다면 그건 진짜 버그라는 뜻이다
        private void SafeFire(NpcTrigger trigger)
        {
            if (_stateMachine.CanFire(trigger)) // → 현재 상태에서 이 트리거가 Permit 되어 있으면 true
                _stateMachine.Fire(trigger);
            else
                Debug.Log($"[NPCInteractable] 전이 무시: {_stateMachine.State} + {trigger}");
        }
        #endregion
    }
}
