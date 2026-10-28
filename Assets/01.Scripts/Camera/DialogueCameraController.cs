using UnityEngine;
using Unity.Cinemachine;

namespace MySecondWorld.CameraSystem
{
    /// <summary>
    /// NPC 대화 시 NPC 얼굴/상체로 카메라를 클로즈업하고,
    /// 대화 종료 시 플레이어 원래 3인칭 카메라로 복귀시키는 컨트롤러.
    /// Cinemachine 3.x 가상 카메라 블렌딩을 활용.
    /// </summary>
    public class DialogueCameraController : MonoBehaviour, IDialogueCameraService
    {
        private static DialogueCameraController _instance;
        public static DialogueCameraController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindFirstObjectByType<DialogueCameraController>();
                    if (_instance == null)
                    {
                        var go = new GameObject("DialogueCameraController");
                        _instance = go.AddComponent<DialogueCameraController>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Cinemachine Settings")]
        [SerializeField] private CinemachineCamera _dialogueCamera;
        [SerializeField] private int _activePriority = 25;
        [SerializeField] private int _inactivePriority = 0;

        [Header("Framing Offsets")]
        [SerializeField] private float _defaultHeadHeight = 1.2f;
        [SerializeField] private float _cameraDistance = 1.6f;
        [SerializeField] private float _lateralOffset = 0.35f;
        [SerializeField] private float _verticalOffset = 0.05f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            EnsureCameraSetup();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void EnsureCameraSetup()
        {
            if (_dialogueCamera == null)
            {
                _dialogueCamera = GetComponentInChildren<CinemachineCamera>();
            }

            if (_dialogueCamera == null)
            {
                var camGo = new GameObject("DialogueCinemachineCamera");
                camGo.transform.SetParent(transform, false);
                _dialogueCamera = camGo.AddComponent<CinemachineCamera>();
            }

            _dialogueCamera.Priority = _inactivePriority;
            _dialogueCamera.gameObject.SetActive(false);
        }

        public void FocusOnNPC(Transform npcTransform, Transform playerTransform = null)
        {
            if (npcTransform == null) return;
            EnsureCameraSetup();

            // 1. 머리/상체 위치 계산 (발끝 메쉬 오브젝트(로컬 0,0,0) 오검색 방지 및 높이 1.2m 보정)
            Transform headBone = FindHeadBone(npcTransform);
            Vector3 headPos;
            if (headBone != null && headBone.position.y > npcTransform.position.y + 0.8f)
            {
                // Head 본이 정상적으로 높이(> 0.8m)에 위치할 경우 (살짝 상체/얼굴 쪽 1.2~1.4m 조준)
                headPos = new Vector3(headBone.position.x, Mathf.Max(headBone.position.y - 0.15f, npcTransform.position.y + _defaultHeadHeight), headBone.position.z);
            }
            else
            {
                headPos = npcTransform.position + Vector3.up * _defaultHeadHeight;
            }

            // 2. NPC 정면 방향 계산 (플레이어 방향 무시하고 항상 정면)
            Vector3 lookDir = -npcTransform.forward;
            lookDir.y = 0f;
            lookDir.Normalize();

            // 3. 카메라 위치: NPC 머리 정면에서 바라봄 (우측 오프셋 제거)
            Vector3 camPos = headPos - (lookDir * _cameraDistance) + (Vector3.up * _verticalOffset);

            Vector3 lookTarget = headPos - camPos;
            Quaternion camRot = lookTarget.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookTarget) : Quaternion.identity;

            _dialogueCamera.transform.position = camPos;
            _dialogueCamera.transform.rotation = camRot;
            _dialogueCamera.ForceCameraPosition(camPos, camRot);

            // 4. 가상 카메라 활성화 및 우선순위 상향
            _dialogueCamera.Priority = _activePriority;
            _dialogueCamera.gameObject.SetActive(true);

            Debug.Log($"[DialogueCameraController] '{npcTransform.name}' 대화 카메라 포커스 활성화 (Target: {headPos}, Cam: {camPos})");
        }

        private Transform FindHeadBone(Transform npcTransform)
        {
            var animator = npcTransform.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                var hb = animator.GetBoneTransform(HumanBodyBones.Head);
                if (hb != null && hb.position.y > npcTransform.position.y + 0.8f)
                    return hb;
            }

            // Root 계층 내부의 본 검색 (Modular_Characters 메쉬 오브젝트는 제외)
            var rootBone = npcTransform.Find("Root");
            if (rootBone != null)
            {
                var hb = FindBoneRecursive(rootBone, "head");
                if (hb != null && hb.position.y > npcTransform.position.y + 0.8f)
                    return hb;
            }

            return null;
        }

        public void ReleaseFocus()
        {
            if (_dialogueCamera == null) return;

            _dialogueCamera.Priority = _inactivePriority;
            _dialogueCamera.gameObject.SetActive(false);

            Debug.Log("[DialogueCameraController] NPC 대화 카메라 해제 -> 원래 3인칭 뷰로 복귀");
        }

        private Transform FindBoneRecursive(Transform parent, string boneName)
        {
            if (parent.name.ToLower().Contains(boneName))
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindBoneRecursive(parent.GetChild(i), boneName);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
