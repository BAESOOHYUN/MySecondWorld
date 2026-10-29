using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;

namespace MySecondWorld.Customization
{
    public abstract class CharacterCustomizerBase : MonoBehaviour
    {
        #region DI
        [SerializeField] Transform _partRoot; // Modular_Characters
        private AvatarPartKeyTableSO _avatarPartKeyTable;
        private CharacterAppearanceSO _defaultAppearanceSO;
        #endregion

        #region 상태
        protected CharacterAppearanceData _currentAppearance;   // desired — 사용자가 마지막으로 "직접 고른" 값
        private CharacterAppearanceData _lastAppliedVisible;    // visible — 직전에 실제로 렌더링(인스턴스화)한 값
        private readonly struct PartInstance
        {
            public readonly GameObject Instance;
            public readonly AsyncOperationHandle<GameObject> Handle;

            public PartInstance(GameObject instance, AsyncOperationHandle<GameObject> handle)
            {
                Instance = instance;
                Handle = handle;
            }
        }

        private readonly Dictionary<PartType, PartInstance> _activePartInstances = new();
        private readonly Dictionary<string, Transform> _boneMap = new();
        private readonly HashSet<PartType> _loadingParts = new(); //연타방지
        private readonly SemaphoreSlim _appearanceLock = new(1, 1); //동시에 ApplyAppearance/SetPart가 겹쳐 돌지 못하게 세마포어로 직렬화
                                                                    //(처음 허용 수, 최대 허용 수)한 번에 한 작업만 들어가고 나머지는 줄을 섬
        public event System.Action AppearanceApplied;    // 파츠 인스턴스화 완료 시점 알림 — 초상화 캡처처럼 "완성된 모습"이 필요한 쪽에서 구독
        
        // 파츠별 subtype 후보가 여러 개일 때, 사용자가 마지막으로 직접 고른 인덱스를 기억 —
        // FindSubtypeIndex가 무조건 첫 후보를 고르는 대신 "사용자의 최근 선택을 우선"하게 함
        private readonly Dictionary<(PartType part, string subtype), int> _lastSelectedIndexByPartAndSubtype = new();
        #endregion

        #region 초기화
        [Inject]
        public void Construct(
            AvatarPartKeyTableSO avatarPartKeyTable,
            CharacterAppearanceSO defaultAppearanceSO)
        {
            _avatarPartKeyTable = avatarPartKeyTable;
            _defaultAppearanceSO = defaultAppearanceSO;
        }

        protected virtual void Start()
        {
            // ★중요! 이 설정을 안하면 T포즈로 굳는다.
            var anim = GetComponent<Animator>() ?? GetComponentInParent<Animator>();
            if (anim != null)
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // 디폴트 SO로 외형 적용
            ApplyAppearance(_defaultAppearanceSO.DefaultAppearance).Forget();
        }

        protected virtual void OnDestroy()
        {
            _appearanceLock.Dispose();
        }
        #endregion

        #region 공개 API
        // 전체 외형 "통째로 다시" 교체 (로그인 시 / 랜덤 / 불러오기)
        public async UniTask ApplyAppearance(CharacterAppearanceData data)
        {
            Debug.Log($"[CustomizerBase] ApplyAppearance 시작! Gender={data.Gender}, Head={data.Head}, Torso={data.Torso}, Hair={data.Hair}, Table={(_avatarPartKeyTable != null ? "OK" : "NULL")}, PartRoot={(_partRoot != null ? _partRoot.name : "NULL")}");

            await _appearanceLock.WaitAsync(); //레이스컨디션을 막음
            try
            {
                _currentAppearance = data;                  // desired 통째로 교체
                _lastSelectedIndexByPartAndSubtype.Clear(); // 세션 캐시 리셋 (기존과 동일)

                if (_avatarPartKeyTable == null)
                {
                    Debug.LogError($"[{name}] AvatarPartKeyTableSO가 null이라 외형을 로드할 수 없습니다!");
                    return;
                }

                // ApplyDirectSelectionRules(Stage1/Type-2)는 호출하지 않는다 — 2026-07-18 확정.
                // 로드된 데이터가 설령 앞뒤가 안 맞아도(예: 예전에 억제된 상태로 저장됐어도)
                // ComputeVisibleAppearance(Stage2)가 매번 desired만 보고 일관된 visible을 계산한다.
                var visible = MutualExclusionResolver.ComputeVisibleAppearance(
                    _currentAppearance, data.Gender, _avatarPartKeyTable, _lastSelectedIndexByPartAndSubtype);


                Debug.Log($"[CustomizerBase] visible 계산 완료: Head={visible.Head}, Torso={visible.Torso}, Hair={visible.Hair}");


                // 기존 인스턴스 전부 해제
                foreach (var key in new List<PartType>(_activePartInstances.Keys))
                    ReleasePartInstance(key);

                // 본 맵을 병렬(25개 파츠를 동시에) 로드 전에 1회 구성 (race condition 방지)
                // _boneMap은 이미 완성된 상태라 읽기만 하면 됨.
                EnsureBoneMap();

                // WhenAll로 동시 로드 — 전부 desired가 아니라 visible.X를 사용한다
                await UniTask.WhenAll(
                    InstantiatePart(PartType.Hair, visible.Hair, data.Gender),
                    InstantiatePart(PartType.Head, visible.Head, data.Gender),
                    InstantiatePart(PartType.Eyebrows, visible.Eyebrows, data.Gender),
                    InstantiatePart(PartType.FacialHair, visible.FacialHair, data.Gender),
                    InstantiatePart(PartType.Torso, visible.Torso, data.Gender),
                    InstantiatePart(PartType.ArmUpperRight, visible.ArmUpperRight, data.Gender),
                    InstantiatePart(PartType.ArmUpperLeft, visible.ArmUpperLeft, data.Gender),
                    InstantiatePart(PartType.ArmLowerRight, visible.ArmLowerRight, data.Gender),
                    InstantiatePart(PartType.ArmLowerLeft, visible.ArmLowerLeft, data.Gender),
                    InstantiatePart(PartType.HandRight, visible.HandRight, data.Gender),
                    InstantiatePart(PartType.HandLeft, visible.HandLeft, data.Gender),
                    InstantiatePart(PartType.Hips, visible.Hips, data.Gender),
                    InstantiatePart(PartType.LegRight, visible.LegRight, data.Gender),
                    InstantiatePart(PartType.LegLeft, visible.LegLeft, data.Gender),
                    InstantiatePart(PartType.HeadCoverings, visible.HeadCoverings, data.Gender),
                    InstantiatePart(PartType.Ear, visible.Ear, data.Gender),
                    InstantiatePart(PartType.HelmetAttachment, visible.HelmetAttachment, data.Gender),
                    InstantiatePart(PartType.BackAttachment, visible.BackAttachment, data.Gender),
                    InstantiatePart(PartType.ShoulderAttachRight, visible.ShoulderAttachRight, data.Gender),
                    InstantiatePart(PartType.ShoulderAttachLeft, visible.ShoulderAttachLeft, data.Gender),
                    InstantiatePart(PartType.ElbowAttachRight, visible.ElbowAttachRight, data.Gender),
                    InstantiatePart(PartType.ElbowAttachLeft, visible.ElbowAttachLeft, data.Gender),
                    InstantiatePart(PartType.KneeAttachRight, visible.KneeAttachRight, data.Gender),
                    InstantiatePart(PartType.KneeAttachLeft, visible.KneeAttachLeft, data.Gender),
                    InstantiatePart(PartType.HipsAttachment, visible.HipsAttachment, data.Gender)
                );

                _lastAppliedVisible = visible;
                ReapplyColors();
                OnAppearanceChanged();
                AppearanceApplied?.Invoke();
                Debug.Log($"[CustomizerBase] ✅ ApplyAppearance 완료! 인스턴스화된 파츠 수={_activePartInstances.Count}");
            }
            finally
            {
                _appearanceLock.Release();
            }
        }

        // 단일 파츠 변경 (UI 버튼에서 호출). 연타 방지 필요
        public async UniTask SetPart(PartType part, int index)
        {
            if (_loadingParts.Contains(part)) return;  // 파츠 하나 단위 가드 — 2026-07-18 확정, 확장 안 함
            
            _loadingParts.Add(part);
            await _appearanceLock.WaitAsync();
            try
            {
                var previousVisible = _lastAppliedVisible; // 직전에 실제로 렌더링했던 상태
                var gender = _currentAppearance.Gender;
                _currentAppearance[part] = index;           // desired 갱신
                RecordLastSelected(part, index, gender);     // 사용자 취향 캐시 갱신 (직접 선택만 기록)

                MutualExclusionResolver.ApplyDirectSelectionRules(
                    ref _currentAppearance, part,index, gender, _avatarPartKeyTable, _lastSelectedIndexByPartAndSubtype);

                var newVisible = MutualExclusionResolver.ComputeVisibleAppearance(
                    _currentAppearance, gender, _avatarPartKeyTable, _lastSelectedIndexByPartAndSubtype);

                // 25개 파츠 전부 순회하지만 실제로 값이 다른 건 최대 6개(상호배제 대상)뿐.
                // newVisible은 이 루프가 시작되기 전에 이미 완전히 확정된 값이라,
                // 어떤 순서로 적용하든(서로를 참조하지 않으므로) 최종 결과는 항상 같다.
                foreach (PartType p in Enum.GetValues(typeof(PartType)))
                {
                    if (newVisible[p] != previousVisible[p])
                        await ReplacePartInstance(p, newVisible[p], gender);
                }
                _lastAppliedVisible = newVisible;
                ReapplyColors();
                OnAppearanceChanged();
            }
            finally
            {
                _appearanceLock.Release();
                _loadingParts.Remove(part);
            }
        }

        // 미러 쌍 전용 — 락 1번 + visible 계산 1번으로 두 파츠를 함께 반영 (CharacterCustomizeUI 전용, 결정 4)
        // SetPart와 로직은 동일하되, 로드 단계만 순차가 아니라 WhenAll로 병렬 처리해 체감 지연을 줄인다
        public async UniTask SetPartPair(PartType partA, PartType partB, int index)
        {
            if (_loadingParts.Contains(partA) || _loadingParts.Contains(partB)) return;

            _loadingParts.Add(partA);
            _loadingParts.Add(partB);
            await _appearanceLock.WaitAsync();
            try
            {
                var previousVisible = _lastAppliedVisible;
                var gender = _currentAppearance.Gender;
                _currentAppearance[partA] = index;
                _currentAppearance[partB] = index;
                RecordLastSelected(partA, index, gender);
                RecordLastSelected(partB, index, gender);

                MutualExclusionResolver.ApplyDirectSelectionRules(
                    ref _currentAppearance, partA, index, gender, _avatarPartKeyTable, _lastSelectedIndexByPartAndSubtype);
                MutualExclusionResolver.ApplyDirectSelectionRules(
                    ref _currentAppearance, partB, index, gender, _avatarPartKeyTable, _lastSelectedIndexByPartAndSubtype);

                var newVisible = MutualExclusionResolver.ComputeVisibleAppearance(
                        _currentAppearance, gender, _avatarPartKeyTable, _lastSelectedIndexByPartAndSubtype);

                // loadTasks에 집어넣고 동시에 돌림
                var loadTasks = new List<UniTask>();
                foreach (PartType p in Enum.GetValues(typeof(PartType)))
                {
                    if (newVisible[p] != previousVisible[p])
                        loadTasks.Add(ReplacePartInstance(p, newVisible[p], gender));
                }
                await UniTask.WhenAll(loadTasks);
                _lastAppliedVisible = newVisible;
                ReapplyColors();
                OnAppearanceChanged();
            }
            finally
            {
                _appearanceLock.Release();
                _loadingParts.Remove(partA);
                _loadingParts.Remove(partB);
            }
            
        }

        // 색 변경
        public void SetColor(ColorTarget target, Color color)
        {
            // SetColor는 한 번 호출에 하나의 ColorTarget만 처리
            switch (target)
            {
                case ColorTarget.Primary: _currentAppearance.PrimaryColor = color; break;
                case ColorTarget.Secondary: _currentAppearance.SecondaryColor = color; break;
                case ColorTarget.LeatherPrimary: _currentAppearance.LeatherPrimaryColor = color; break;
                case ColorTarget.LeatherSecondary: _currentAppearance.LeatherSecondaryColor = color; break;
                case ColorTarget.MetalPrimary: _currentAppearance.MetalPrimaryColor = color; break;
                case ColorTarget.MetalSecondary: _currentAppearance.MetalSecondaryColor = color; break;
                case ColorTarget.MetalDark: _currentAppearance.MetalDarkColor = color; break;
                case ColorTarget.Hair: _currentAppearance.HairColor = color; break;
                case ColorTarget.Skin: _currentAppearance.SkinColor = color; break;
                case ColorTarget.Stubble: _currentAppearance.StubbleColor = color; break;
                case ColorTarget.Scar: _currentAppearance.ScarColor = color; break;
                case ColorTarget.Eyes: _currentAppearance.EyesColor = color; break;
                case ColorTarget.BodyArt: _currentAppearance.BodyArtColor = color; break;
            }

            // prop 셰이더 코드 안의 프로퍼티 이름
            var prop = target switch
            {
                ColorTarget.Primary => "_Color_Primary",
                ColorTarget.Secondary => "_Color_Secondary",
                ColorTarget.LeatherPrimary => "_Color_Leather_Primary",
                ColorTarget.LeatherSecondary => "_Color_Leather_Secondary",
                ColorTarget.MetalPrimary => "_Color_Metal_Primary",
                ColorTarget.MetalSecondary => "_Color_Metal_Secondary",
                ColorTarget.MetalDark => "_Color_Metal_Dark",
                ColorTarget.Hair => "_Color_Hair",
                ColorTarget.Skin => "_Color_Skin",
                ColorTarget.Stubble => "_Color_Stubble",
                ColorTarget.Scar => "_Color_Scar",
                ColorTarget.Eyes => "_Color_Eyes",
                ColorTarget.BodyArt => "_Color_BodyArt",
                _ => null
            };

            if (prop == null) return;

            foreach (var instance in _activePartInstances.Values)
            {
                if (instance.Instance == null) continue;
                var smr = instance.Instance.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                    smr.material.SetColor(prop, color);
            }
        }

        private void ReapplyColors()
        {
            SetColor(ColorTarget.Primary, _currentAppearance.PrimaryColor);
            SetColor(ColorTarget.Secondary, _currentAppearance.SecondaryColor);
            SetColor(ColorTarget.LeatherPrimary, _currentAppearance.LeatherPrimaryColor);
            SetColor(ColorTarget.LeatherSecondary, _currentAppearance.LeatherSecondaryColor);
            SetColor(ColorTarget.MetalPrimary, _currentAppearance.MetalPrimaryColor);
            SetColor(ColorTarget.MetalSecondary, _currentAppearance.MetalSecondaryColor);
            SetColor(ColorTarget.MetalDark, _currentAppearance.MetalDarkColor);
            SetColor(ColorTarget.Hair, _currentAppearance.HairColor);
            SetColor(ColorTarget.Skin, _currentAppearance.SkinColor);
            SetColor(ColorTarget.Stubble, _currentAppearance.StubbleColor);
            SetColor(ColorTarget.Scar, _currentAppearance.ScarColor);
            SetColor(ColorTarget.Eyes, _currentAppearance.EyesColor);
            SetColor(ColorTarget.BodyArt, _currentAppearance.BodyArtColor);
        }

        public CharacterAppearanceData GetCurrentAppearance() => _currentAppearance;
        #endregion

        #region 오버라이드 포인트
        protected virtual void OnAppearanceChanged() { }
        protected virtual void OnPartInstantiated(GameObject instance) { }
        #endregion

        #region 내부 구현
        private async UniTask InstantiatePart(PartType part, int index, Gender gender)
        {
            var candidates = _avatarPartKeyTable.GetCandidates(part, gender);
            if (index < 0 || index >= candidates.Count) // "인덱스가 리스트 안에 있냐"만 봄. 리스트 안의 항목이 빈 항목인지는 구분 못함
            {
                // -1(None)이 아닌데 인덱스 범위 초과면 경고
                if (index != -1) Debug.LogWarning($"[CustomizerBase] {part} 인덱스 범위 벗어남 (index={index}, count={candidates.Count})");
                return;
            }

            if (!candidates[index].RuntimeKeyIsValid()) // "인덱스는 유효하지만 내용물이 빈 참조"인 경우를 걸러냄
            {
                Debug.LogWarning($"[CustomizerBase] {part} candidates[{index}] RuntimeKey 유효하지 않음!");
                return;
            }

            try
            {
                var handle = Addressables.InstantiateAsync(candidates[index], _partRoot);
                var instance = await handle.ToUniTask();
                _activePartInstances[part] = new PartInstance(instance, handle);

                OnPartInstantiated(instance);

                var smr = instance.GetComponent<SkinnedMeshRenderer>();
                var partBoneNames = instance.GetComponent<PartBoneNames>();

                if (smr != null && partBoneNames != null)
                    RemapBones(smr, partBoneNames);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CustomizerBase] {part} InstantiateAsync 실패: {ex.Message}");
            }
        }

        private void ReleasePartInstance(PartType part)
        {
            if (_activePartInstances.TryGetValue(part, out var old))
            {
                if (old.Handle.IsValid())
                    Addressables.ReleaseInstance(old.Handle); // 저장해 둔 핸들로 해제 — 오브젝트 파괴도 Addressables가 함
                else
                {
                    Debug.LogWarning($"[ReleasePartInstance] 핸들 무효 → Destroy: {part} {old.Instance.name}");
                    Destroy(old.Instance);                      // 핸들이 이미 없을 때만 안전망
                }

                _activePartInstances.Remove(part); // 착용 파츠를 착용목록 장부에서 삭제
            }
        }

        // "이 파츠는 사용자가 방금 이 인덱스로 직접 골랐다"는 사실을 기록해두는 함수.
        // 나중에 같은 subtype 안에서 하나를 자동으로 골라야 할 때(예: 헬멧 벗고 얼굴 되돌리기),
        // 아무거나 첫 번째를 고르지 않고 이 선택 기록을 먼저 확인해서 쓴다.
        // 이 기록은 오직 여기서만 바뀐다 — 계산만 하는 순수 함수들(ApplyDirectSelectionRules,
        // ComputeVisibleAppearance)은 이 기록을 읽기만 하고 절대 고치지 않는다.
        private void RecordLastSelected(PartType part, int index, Gender gender)
        {
            var subtype = _avatarPartKeyTable.GetSubType(part, gender, index);
            _lastSelectedIndexByPartAndSubtype[(part, subtype)] = index;
        }

        // 딱 하나의 파츠만 지우고 다시 만드는 함수. 다른 파츠는 절대 안 건드린다.
        // 그래서 이 함수를 여러 번 연달아 불러도 서로 꼬이거나 겹칠 일이 없다.
        private async UniTask ReplacePartInstance(PartType part, int index, Gender gender)
        {
            ReleasePartInstance(part);
            await InstantiatePart(part, index, gender);
        }
        #endregion

        #region 본 관련
        private void EnsureBoneMap()
        {
            if (_boneMap.Count > 0) return;
            // 이 오브젝트 + 모든 자손에서 Transform 컴포넌트를 전부 긁어온다.
            // true = 비활성(SetActive false) 오브젝트도 포함
            // false = 활성된 것만 (기본값)
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                _boneMap[t.name] = t;
        }

        private void RemapBones(SkinnedMeshRenderer smr, PartBoneNames partBoneNames)
        {
            var boneNames = partBoneNames.BoneNames;
            var bones = new Transform[boneNames.Length];
            for (int i = 0; i < boneNames.Length; i++)
                _boneMap.TryGetValue(boneNames[i], out bones[i]);
            smr.bones = bones;

            smr.updateWhenOffscreen = true; // 초기 바운즈 0으로 인한 SMR 컬링 방지

            string rootName = !string.IsNullOrEmpty(partBoneNames.RootBoneName)
                ? partBoneNames.RootBoneName
                : "Hips";

            if (_boneMap.TryGetValue(rootName, out var root))
                smr.rootBone = root;
        }
        #endregion
    }
}