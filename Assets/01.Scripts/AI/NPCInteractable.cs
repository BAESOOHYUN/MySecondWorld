using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MySecondWorld.AI
{
    // Trigger 감지, New Input System, 흐름 제어

    /// <summary>
    /// 플레이어 근접 감지 → 대화 시작/종료 흐름 제어
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NPCInteractable : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NPCBrain _brain;
        [SerializeField] private NPCDialogueUI _dialogueUI;

        [Header("Input")]
        [Tooltip("New Input System — 상호작용 액션 (예: 'E' 키 또는 터치 탭)")]
        public InputActionReference interactAction;

        private bool _playerInRange;
        private bool _isDialogueOpen;
        private CancellationTokenSource _cts;

        // ── 초기화 검증 ──────────────────────────────────────────────────────────
        private void Start()
        {
            if (_brain == null)
            {
                Debug.LogError($"[NPCInteractable] {name}: NPCBrain이 연결되지 않았습니다. 컴포넌트를 비활성화합니다.");
                enabled = false;
                return;
            }

            if (interactAction != null)
            {
                interactAction.action.performed += OnInteractPerformed;
                Debug.Log($"[NPCInteractable] Start: interactAction 등록됨 — {interactAction.action.name}, enabled: {interactAction.action.enabled}");
            }
            else
            {
                Debug.LogWarning($"[NPCInteractable] Start: interactAction이 null입니다. Inspector에서 연결 필요.");
            }
        }

        // ── 입력 등록 ────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            interactAction?.action.Enable();
            Debug.Log($"[NPCInteractable] OnEnable: action enabled");
        }
        private void OnDisable()
        {
            interactAction?.action.Disable();
            Debug.Log($"[NPCInteractable] OnDisable: action disabled");
        }

        private void OnDestroy()
        {
            if (interactAction != null)
                interactAction.action.performed -= OnInteractPerformed;
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // ── Trigger 감지 ─────────────────────────────────────────────────────────
        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[NPCInteractable] TriggerEnter: {other.name}, tag: {other.tag}");
            if (other.CompareTag("Player"))
                _playerInRange = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInRange = false;
            CloseDialogue();
        }

        // ── 상호작용 입력 ────────────────────────────────────────────────────────
        private void OnInteractPerformed(InputAction.CallbackContext ctx)
        {
            Debug.Log($"[NPCInteractable] OnInteractPerformed, playerInRange: {_playerInRange}");
            if (!_playerInRange) return;
            if (OnUIChecker.IsInputFieldFocused()) return;

            if (!_isDialogueOpen) OpenDialogue();
            else CloseDialogue();
        }

        // ── 대화 열기/닫기 ───────────────────────────────────────────────────────
        private void OpenDialogue()
        {
            Debug.Log($"[NPCInteractable] OpenDialogue 호출, _dialogueUI={_dialogueUI}");
            _isDialogueOpen = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _dialogueUI?.Open(OnPlayerSubmit, OnCloseRequest);
        }

        public void CloseDialogue()
        {
            if (!_isDialogueOpen) return;
            _isDialogueOpen = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _dialogueUI?.Close();
        }

        // ── 플레이어 메시지 전송 콜백 ────────────────────────────────────────────
        private void OnPlayerSubmit(string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput)) return;
            if (_cts == null) return;
            SendMessageToNPCAsync(userInput, _cts.Token).Forget();
        }

        private async UniTaskVoid SendMessageToNPCAsync(string userInput, CancellationToken ct)
        {
            Debug.Log($"[NPCInteractable] SendMessageToNPCAsync 시작: {userInput}");
            if (_brain == null)
            {
                Debug.LogError($"[NPCInteractable] {name}: NPCBrain이 null입니다.");
                return;
            }

            _dialogueUI?.SetInputEnabled(false);
            _dialogueUI?.ShowThinking();

            string response = await _brain.GenerateResponseAsync(userInput, ct);

            if (this == null) return;
            if (ct.IsCancellationRequested) return;

            _dialogueUI?.ShowResponse(_brain.SystemPrompt?.NpcName ?? "NPC", response);
            _dialogueUI?.SetInputEnabled(true);
        }

        private void OnCloseRequest()
        {
            Debug.Log($"[NPCInteractable] OnCloseRequest 호출");
            CloseDialogue();
        }
    }
}
