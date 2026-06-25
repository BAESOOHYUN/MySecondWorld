using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;

namespace MySecondWorld.Customization
{
    public abstract class CharacterCustomizerBase : MonoBehaviour
    {
        // ── DI ──
        [SerializeField] Transform _partRoot; // Modular_Characters
        private AvatarPartKeyTableSO _avatarPartKeyTable;
        private CharacterAppearanceSO _defaultAppearanceSO;

        // ── 상태 ──
        protected CharacterAppearanceData _currentAppearance;
        private readonly Dictionary<PartType, GameObject> _activePartInstances = new();
        private readonly Dictionary<string, Transform> _boneMap = new();
        private readonly HashSet<PartType> _loadingParts = new(); //연타방지

        // ── 초기화 ──
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
            var anim = GetComponent<Animator>() ?? GetComponentInParent<Animator>();
            if (anim != null)
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            ApplyAppearance(_defaultAppearanceSO.DefaultAppearance).Forget();
        }

        // ── 공개 API ──
        // 전체 외형 교체 (로그인 시 / 랜덤 / 불러오기)
        public async UniTask ApplyAppearance(CharacterAppearanceData data)
        {
            _currentAppearance = data;

            // 기존 인스턴스 전부 해제
            foreach (var key in new List<PartType>(_activePartInstances.Keys))
                ReleasePartInstance(key);

            // 본 맵을 병렬(25개 파츠를 동시에) 로드 전에 1회 구성 (race condition 방지)
            // _boneMap은 이미 완성된 상태라 읽기만 하면 됨.
            EnsureBoneMap();

            // WhenAll로 동시 로드
            await UniTask.WhenAll(
                InstantiatePart(PartType.Hair, data.Hair, data.Gender),
                InstantiatePart(PartType.Head, data.Head, data.Gender),
                InstantiatePart(PartType.Eyebrows, data.Eyebrows, data.Gender),
                InstantiatePart(PartType.FacialHair, data.FacialHair, data.Gender),
                InstantiatePart(PartType.Torso, data.Torso, data.Gender),
                InstantiatePart(PartType.ArmUpperRight, data.ArmUpperRight, data.Gender),
                InstantiatePart(PartType.ArmUpperLeft, data.ArmUpperLeft, data.Gender),
                InstantiatePart(PartType.ArmLowerRight, data.ArmLowerRight, data.Gender),
                InstantiatePart(PartType.ArmLowerLeft, data.ArmLowerLeft, data.Gender),
                InstantiatePart(PartType.HandRight, data.HandRight, data.Gender),
                InstantiatePart(PartType.HandLeft, data.HandLeft, data.Gender),
                InstantiatePart(PartType.Hips, data.Hips, data.Gender),
                InstantiatePart(PartType.LegRight, data.LegRight, data.Gender),
                InstantiatePart(PartType.LegLeft, data.LegLeft, data.Gender),
                InstantiatePart(PartType.HeadCoverings, data.HeadCoverings, data.Gender),
                InstantiatePart(PartType.Ear, data.Ear, data.Gender),
                InstantiatePart(PartType.HelmetAttachment, data.HelmetAttachment, data.Gender),
                InstantiatePart(PartType.BackAttachment, data.BackAttachment, data.Gender),
                InstantiatePart(PartType.ShoulderAttachRight, data.ShoulderAttachRight, data.Gender),
                InstantiatePart(PartType.ShoulderAttachLeft, data.ShoulderAttachLeft, data.Gender),
                InstantiatePart(PartType.ElbowAttachRight, data.ElbowAttachRight, data.Gender),
                InstantiatePart(PartType.ElbowAttachLeft, data.ElbowAttachLeft, data.Gender),
                InstantiatePart(PartType.KneeAttachRight, data.KneeAttachRight, data.Gender),
                InstantiatePart(PartType.KneeAttachLeft, data.KneeAttachLeft, data.Gender),
                InstantiatePart(PartType.HipsAttachment, data.HipsAttachment, data.Gender)
            );

            SetColor(ColorTarget.Primary, data.PrimaryColor);
            SetColor(ColorTarget.Secondary, data.SecondaryColor);
            SetColor(ColorTarget.LeatherPrimary, data.LeatherPrimaryColor);
            SetColor(ColorTarget.LeatherSecondary, data.LeatherSecondaryColor);
            SetColor(ColorTarget.MetalPrimary, data.MetalPrimaryColor);
            SetColor(ColorTarget.MetalSecondary, data.MetalSecondaryColor);
            SetColor(ColorTarget.MetalDark, data.MetalDarkColor);
            SetColor(ColorTarget.Hair, data.HairColor);
            SetColor(ColorTarget.Skin, data.SkinColor);
            SetColor(ColorTarget.Stubble, data.StubbleColor);
            SetColor(ColorTarget.Scar, data.ScarColor);
            SetColor(ColorTarget.Eyes, data.EyesColor);
            SetColor(ColorTarget.BodyArt, data.BodyArtColor);

            // ApplyAppearance는 SetPart처럼 끝에서 ApplyMutualExclusion을 호출하지 않으므로 들어온 데이터를 검증 없이 그대로 로드한다.
            // 성별전환/랜덤/세이브로드는 파츠를 하나씩 따로 정해서 조합하기 때문에,
            // Head뿐 아니라 HeadCoverings/Hair/Eyebrows/FacialHair/HelmetAttachment 각자의 상호배제 규칙과도 충돌할 수 있어
            // 트리거가 되는 6개 파츠를 전부 다시 검증한다. 매번 _currentAppearance를 새로 읽어 앞선 검증의 연쇄 결과를 반영한다.
            await ApplyMutualExclusion(PartType.Head, _currentAppearance.Head, data.Gender);
            await ApplyMutualExclusion(PartType.HeadCoverings, _currentAppearance.HeadCoverings, data.Gender);
            await ApplyMutualExclusion(PartType.Hair, _currentAppearance.Hair, data.Gender);
            await ApplyMutualExclusion(PartType.Eyebrows, _currentAppearance.Eyebrows, data.Gender);
            await ApplyMutualExclusion(PartType.FacialHair, _currentAppearance.FacialHair, data.Gender);
            await ApplyMutualExclusion(PartType.HelmetAttachment, _currentAppearance.HelmetAttachment, data.Gender);

            OnAppearanceChanged();
        }

        // 단일 파츠 변경 (UI 버튼에서 호출). 연타 방지 필요
        public async UniTask SetPart(PartType part, int index)
        {
            if (_loadingParts.Contains(part)) return;
            _loadingParts.Add(part);
            ReleasePartInstance(part);  // 자기 자신의 이전 인스턴스 해제
            SetCurrentIndex(part, index);
            await InstantiatePart(part, index, _currentAppearance.Gender);
            OnAppearanceChanged();
            _loadingParts.Remove(part);
            await ApplyMutualExclusion(part, index, _currentAppearance.Gender);
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
                if (instance == null) continue;
                var smr = instance.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                    smr.material.SetColor(prop, color);
            }
        }

        public CharacterAppearanceData GetCurrentAppearance() => _currentAppearance;

        // ── 오버라이드 포인트 ──
        protected virtual void OnAppearanceChanged() { }
        protected virtual void OnPartInstantiated(GameObject instance) { }

        // ── 내부 구현 ──
        private async UniTask InstantiatePart(PartType part, int index, Gender gender)
        {
            var candidates = _avatarPartKeyTable.GetCandidates(part, gender);
            if (index < 0 || index >= candidates.Count) return;

            var instance = await Addressables.InstantiateAsync(candidates[index], _partRoot).ToUniTask();
            _activePartInstances[part] = instance;

            OnPartInstantiated(instance);

            var smr = instance.GetComponent<SkinnedMeshRenderer>();
            var partBoneNames = instance.GetComponent<PartBoneNames>();

            if (smr != null && partBoneNames != null)
                RemapBones(smr, partBoneNames);
        }

        private void ReleasePartInstance(PartType part)
        {
            if (_activePartInstances.TryGetValue(part, out var old) && old != null)
            {
                Addressables.ReleaseInstance(old);
                _activePartInstances.Remove(part);
            }
        }

        private void SetCurrentIndex(PartType part, int index)
        {
            switch (part)
            {
                case PartType.Hair: _currentAppearance.Hair = index; break;
                case PartType.Head: _currentAppearance.Head = index; break;
                case PartType.Eyebrows: _currentAppearance.Eyebrows = index; break;
                case PartType.FacialHair: _currentAppearance.FacialHair = index; break;
                case PartType.Torso: _currentAppearance.Torso = index; break;
                case PartType.ArmUpperRight: _currentAppearance.ArmUpperRight = index; break;
                case PartType.ArmUpperLeft: _currentAppearance.ArmUpperLeft = index; break;
                case PartType.ArmLowerRight: _currentAppearance.ArmLowerRight = index; break;
                case PartType.ArmLowerLeft: _currentAppearance.ArmLowerLeft = index; break;
                case PartType.HandRight: _currentAppearance.HandRight = index; break;
                case PartType.HandLeft: _currentAppearance.HandLeft = index; break;
                case PartType.Hips: _currentAppearance.Hips = index; break;
                case PartType.LegRight: _currentAppearance.LegRight = index; break;
                case PartType.LegLeft: _currentAppearance.LegLeft = index; break;
                case PartType.HeadCoverings: _currentAppearance.HeadCoverings = index; break;
                case PartType.Ear: _currentAppearance.Ear = index; break;
                case PartType.HelmetAttachment: _currentAppearance.HelmetAttachment = index; break;
                case PartType.BackAttachment: _currentAppearance.BackAttachment = index; break;
                case PartType.ShoulderAttachRight: _currentAppearance.ShoulderAttachRight = index; break;
                case PartType.ShoulderAttachLeft: _currentAppearance.ShoulderAttachLeft = index; break;
                case PartType.ElbowAttachRight: _currentAppearance.ElbowAttachRight = index; break;
                case PartType.ElbowAttachLeft: _currentAppearance.ElbowAttachLeft = index; break;
                case PartType.KneeAttachRight: _currentAppearance.KneeAttachRight = index; break;
                case PartType.KneeAttachLeft: _currentAppearance.KneeAttachLeft = index; break;
                case PartType.HipsAttachment: _currentAppearance.HipsAttachment = index; break;
            }
        }

        // 파츠 선택 시 Synty 상호배제 규칙 적용
        // ── 상호배제 매트릭스 (Synty ModularCharacters 규칙) ──────────────────────────────
        // 선택 파츠                  │ 조건                      │ 결과
        // ───────────────────────────┼───────────────────────────┼─────────────────────────
        // HeadCoverings  (index ≥ 0) │ subtype = No_Hair          │ Hair              → -1
        // HeadCoverings  (index ≥ 0) │ subtype = No_FacialHair    │ FacialHair        → -1
        // HeadCoverings  (index ≥ 0) │ 현재 Head = No_Elements    │ Head → 첫 All_Elements
        // Hair           (index ≥ 0) │ 현재 HC   = No_Hair        │ HeadCoverings     → -1
        // Hair           (index ≥ 0) │ 현재 Head = No_Elements    │ Head → 첫 All_Elements
        // Eyebrows       (index ≥ 0) │ 현재 Head = No_Elements    │ Head → 첫 All_Elements
        // FacialHair     (index ≥ 0) │ 현재 HC   = No_FacialHair  │ HeadCoverings     → -1
        // FacialHair     (index ≥ 0) │ 현재 Head = No_Elements    │ Head → 첫 All_Elements
        // Head           (index ≥ 0) │ subtype   = All_Elements   │ HelmetAttachment  → -1
        // Head           (index ≥ 0) │ subtype   = No_Elements    │ Hair, Eyebrows, FacialHair, HeadCoverings → -1
        // HelmetAttachment(index ≥ 0)│ 현재 Head = All_Elements   │ Head → 첫 No_Elements
        // ───────────────────────────┼───────────────────────────┼─────────────────────────
        // ※ index < 0 (없음 슬롯) 는 추가 배제 없음
        private async UniTask ApplyMutualExclusion(PartType changedPart, int newIndex, Gender gender)
        {
            var subtype = _avatarPartKeyTable.GetSubType(changedPart, gender, newIndex);

            switch (changedPart)
            {
                case PartType.HeadCoverings when newIndex >= 0:
                    {
                        // 헤드커버 선택 → No_Hair면 머리카락, No_FacialHair면 수염 해제
                        if (subtype == "No_Hair")
                            await SetPart(PartType.Hair, -1);
                        else if (subtype == "No_FacialHair")
                            await SetPart(PartType.FacialHair, -1);

                        // 어떤 헤드커버든 No_Elements 헤드와 충돌 → All_Elements로 교체
                        if (_avatarPartKeyTable.GetSubType(PartType.Head, gender, _currentAppearance.Head) == "No_Elements")
                            await SwitchHeadTo("All_Elements", gender);
                        break;
                    }

                case PartType.Hair when newIndex >= 0:
                    {
                        // 머리카락 선택 → No_Hair 헤드커버와 충돌 → 헤드커버 해제
                        if (_avatarPartKeyTable.GetSubType(PartType.HeadCoverings, gender, _currentAppearance.HeadCoverings) == "No_Hair")
                            await SetPart(PartType.HeadCoverings, -1);

                        // No_Elements 헤드와도 충돌 → All_Elements로 교체
                        if (_avatarPartKeyTable.GetSubType(PartType.Head, gender, _currentAppearance.Head) == "No_Elements")
                            await SwitchHeadTo("All_Elements", gender);
                        break;
                    }

                case PartType.Eyebrows when newIndex >= 0:
                    {
                        // 눈썹 선택 → No_Elements 헤드와 충돌 (눈썹 메시가 빈 얼굴판에 클리핑) → All_Elements로 교체
                        if (_avatarPartKeyTable.GetSubType(PartType.Head, gender, _currentAppearance.Head) == "No_Elements")
                            await SwitchHeadTo("All_Elements", gender);
                        break;
                    }

                case PartType.FacialHair when newIndex >= 0:
                    {
                        // 수염 선택 → No_FacialHair 헤드커버와 충돌 → 헤드커버 해제
                        if (_avatarPartKeyTable.GetSubType(PartType.HeadCoverings, gender, _currentAppearance.HeadCoverings) == "No_FacialHair")
                            await SetPart(PartType.HeadCoverings, -1);

                        // No_Elements 헤드와도 충돌 → All_Elements로 교체
                        if (_avatarPartKeyTable.GetSubType(PartType.Head, gender, _currentAppearance.Head) == "No_Elements")
                            await SwitchHeadTo("All_Elements", gender);
                        break;
                    }

                case PartType.Head when newIndex >= 0:
                    {
                        // 헤드 선택
                        // All_Elements(얼굴판 있음) → 헬멧과 충돌 → 헬멧 해제
                        // No_Elements(빈 얼굴판) → 머리카락·눈썹·수염·헤드커버 전부 충돌 → 일괄 해제
                        if (subtype == "All_Elements")
                            await SetPart(PartType.HelmetAttachment, -1);
                        else if (subtype == "No_Elements")
                        {
                            await SetPart(PartType.Hair, -1);
                            await SetPart(PartType.Eyebrows, -1);
                            await SetPart(PartType.FacialHair, -1);
                            await SetPart(PartType.HeadCoverings, -1);
                        }
                        break;
                    }

                case PartType.HelmetAttachment when newIndex >= 0:
                    {
                        // 헬멧 선택 → All_Elements 헤드와 충돌 (헬멧이 얼굴판을 뚫고 나옴)
                        // → No_Elements 헤드 중 첫 번째로 자동 교체
                        if (_avatarPartKeyTable.GetSubType(PartType.Head, gender, _currentAppearance.Head) == "All_Elements")
                            await SwitchHeadTo("No_Elements", gender);
                        break;
                    }
            }
        }

        private async UniTask SwitchHeadTo(string targetSubtype, Gender gender)
        {
            var count = _avatarPartKeyTable.GetCount(PartType.Head, gender);
            for (int i = 0; i < count; i++)
            {
                if (_avatarPartKeyTable.GetSubType(PartType.Head, gender, i) == targetSubtype)
                {
                    await SetPart(PartType.Head, i);
                    return;
                }
            }
        }

        // ── 본 관련 ──
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
    }
}