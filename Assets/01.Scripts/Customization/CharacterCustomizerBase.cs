using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;

namespace MySecondWorld.Customization
{
    public abstract class CharacterCustomizerBase : MonoBehaviour
    {
        [SerializeField] Transform _partRoot; // Modular_Characters

        // Hair/Head/FacialHair/HeadCoverings — 동시에 하나만 활성화
        private static readonly HashSet<PartType> _headGroup = new()
        {
            PartType.Hair,
            PartType.Head,
            PartType.FacialHair,
            PartType.HeadCoverings,
        };

        private AvatarPartKeyTableSO _avatarPartKeyTable;
        private CharacterAppearanceSO _defaultAppearanceSO;

        protected CharacterAppearanceData _currentAppearance;

        private readonly Dictionary<PartType, GameObject> _activePartInstances = new();
        private readonly Dictionary<string, Transform> _boneMap = new();
        private readonly HashSet<PartType> _loadingParts = new(); //연타방지

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

            OnAppearanceChanged();
        }

        // 단일 파츠 변경 (UI 버튼에서 호출). 연타 방지 필요
        public async UniTask SetPart(PartType part, int index)
        {
            if (_loadingParts.Contains(part)) return;

            _loadingParts.Add(part);

            // 머리 상호배제: 같은 그룹의 다른 파츠 해제
            if (_headGroup.Contains(part)) // 바꾸려는 게 머리 그룹 중 하나면
            {
                // 그룹 내 나머지 전부 해제
                // ex) Hair는 headGroup 소속
                //    → Head, FacialHair, HeadCoverings 인스턴스 해제. Hair 새로 로드
                foreach (var groupPart in _headGroup)
                {
                    if (groupPart != part)
                        ReleasePartInstance(groupPart);
                }
            }
            else                           // 머리 그룹이 아닌 파츠면 (Torso, Arm 등)
            {
                // 그냥 자기 자신의 이전 인스턴스만 해제
                // → Torso는 headGroup 비소속 → else
                //   → 이전 Torso 인스턴스만 해제. Torso 새로 로드
                ReleasePartInstance(part);
            }

            SetCurrentIndex(part, index);
            await InstantiatePart(part, index, _currentAppearance.Gender);
            OnAppearanceChanged();
            _loadingParts.Remove(part);
        }

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

        protected virtual void OnAppearanceChanged() { }

        private async UniTask InstantiatePart(PartType part, int index, Gender gender)
        {
            var candidates = _avatarPartKeyTable.GetCandidates(part, gender);
            if (index < 0 || index >= candidates.Count) return;

            var instance = await Addressables.InstantiateAsync(candidates[index], _partRoot).ToUniTask();
            _activePartInstances[part] = instance;

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
    }
}