using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;
using Random = UnityEngine.Random;
using VContainer;
using MessagePipe;
using MySecondWorld.Customization;
using MySecondWorld.Save;

namespace MySecondWorld.UI
{
    [System.Serializable]
    public struct PartTabEntry
    {
        public PartType Part;
        public Toggle Toggle;
    }

    public class CharacterCustomizeUI : MonoBehaviour
    {
        [Header("최상위 탭")]
        [SerializeField] private Toggle _partsTopTab;
        [SerializeField] private Toggle _accessoryTopTab;
        [SerializeField] private Toggle _colorTopTab;
        [SerializeField] private GameObject _partsTabGroup;     // 파츠 탭 버튼 묶음 부모
        [SerializeField] private GameObject _accessoryTabGroup; // 액세서리 탭 버튼 묶음 부모
        [SerializeField] private GameObject _colorPanel;        // 색상 패널

        [Header("파츠 탭 (Hair~LegRight 15개)")]
        [SerializeField] private PartTabEntry[] _partsTabEntries;

        [Header("액세서리 탭 (HeadCoverings~HipsAttachment 10개)")]
        [SerializeField] private PartTabEntry[] _accessoryTabEntries;

        [Header("썸네일")]
        [SerializeField] private ScrollRect _thumbnailScrollRect;
        [SerializeField] private Transform _thumbnailContent;  // GridLayoutGroup 부착
        [SerializeField] private PartThumbnailButton _thumbnailButtonPrefab;

        [Header("색상")]
        [SerializeField] private Transform _colorRowParent;        // Content (VerticalLayoutGroup)
        [SerializeField] private ColorChannelRow _colorRowPrefab;  // 행 프리팹
        [SerializeField] private FlexibleColorPicker _colorPicker; // 공유 팝업 피커
        [SerializeField] private ColorTarget _activeColorTarget = ColorTarget.Primary;

        [Header("버튼")]
        [SerializeField] private Button _maleButton;
        [SerializeField] private Button _femaleButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _randomButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _exitButton;

        [Header("프리뷰 캐릭터")]
        [SerializeField] private LocalCharacterCustomizer _previewCustomizer;

        // ── 상수 ──
        private const string PrefKeyCustomized = "CharacterCustomized";

        // ── DI ──
        private AvatarPartKeyTableSO _keyTable;
        private CharacterAppearanceSO _defaultAppearanceSO;
        private CharacterSaveService _saveService;
        private LocalPlayerInventoryProvider _inventoryProvider;
        private IPublisher<CharacterAppearanceSyncRequestedEvent> _saveRequestPub;

        // ── 아틀라스 ──
        private SpriteAtlas _thumbnailAtlas;
        private AsyncOperationHandle<SpriteAtlas> _atlasHandle;

        // ── 탭 상태 ──
        private bool _isSwitchingTab;
        private Gender _currentGender = Gender.Male;
        private PartType _currentTab = PartType.Hair;
        private PartType _lastPartsTab;
        private PartType _lastAccessoryTab;

        // ── 파츠 인덱스 ──
        private readonly Dictionary<PartType, int> _currentIndices = new();
        private static readonly HashSet<PartType> _optionalParts = new() // 필수가 아닌 파츠. -1로 세팅하여 빈칸 썸네일 생성
        {
            PartType.Hair, PartType.FacialHair, PartType.Eyebrows, PartType.Ear
        };

        // ── UI 요소 ──
        private readonly List<PartThumbnailButton> _spawnedButtons = new();
        private readonly Dictionary<ColorTarget, ColorChannelRow> _colorRows = new();
        private readonly HashSet<PartType> _pendingParts = new();
        private bool _isOpeningPicker;
        private bool _openedDuringStartup;

        [Inject]
        public void Construct(AvatarPartKeyTableSO keyTable,
                            CharacterSaveService saveService,
                            CharacterAppearanceSO defaultAppearanceSO,
                            LocalPlayerInventoryProvider inventoryProvider,
                            IPublisher<CharacterAppearanceSyncRequestedEvent> saveRequestPub,
                            ISubscriber<RequestOpenCustomizeUIEvent> openUISub)
        {
            _keyTable = keyTable;
            _saveService = saveService;
            _defaultAppearanceSO = defaultAppearanceSO;
            _inventoryProvider = inventoryProvider;
            _saveRequestPub = saveRequestPub;
            openUISub.Subscribe(_ => Open()).AddTo(destroyCancellationToken);

            // 프리뷰 커스터마이저에 수동으로 의존성 전달
            _previewCustomizer.Construct(keyTable, defaultAppearanceSO);
        }

        private void Start()
        {
            _lastPartsTab = _partsTabEntries.Length > 0 ? _partsTabEntries[0].Part : default;
            _lastAccessoryTab = _accessoryTabEntries.Length > 0 ? _accessoryTabEntries[0].Part : default;
            RegisterButtons();
            InitColorRows();
            _colorPicker.gameObject.SetActive(false);
            StartupAsync().Forget();
        }

        private void OnDestroy()
        {
            if (_atlasHandle.IsValid())
                Addressables.Release(_atlasHandle);
        }

        private async UniTaskVoid StartupAsync()
        {
            _atlasHandle = Addressables.LoadAssetAsync<SpriteAtlas>(_keyTable.ThumbnailAtlas);
            _thumbnailAtlas = await _atlasHandle.ToUniTask();
            await ReloadFromSave();
            if (!_openedDuringStartup)
                gameObject.SetActive(false);
        }

        public void OpenForFirstLaunch() => gameObject.SetActive(true);

        // 패널을 열 때마다 호출 — 저장된 외형으로 다시 초기화한다.
        // (저장 안 하고 닫았다가 다시 열면 편집 중이던 미저장 상태가 그대로 남아있던 버그 수정)
        public void Open()
        {
            _openedDuringStartup = true;
            gameObject.SetActive(true);
            ReloadFromSave().Forget();
        }


        // 인덱스 초기화
        private void InitIndices(CharacterAppearanceData data)
        {
            // 저장된 외형을 로드해서 인덱스/ 프리뷰에 반영
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
                _currentIndices[part] = data[part];
        }

        // 버튼 등록
        private void RegisterButtons()
        {
            _maleButton.onClick.AddListener(() => SetGender(Gender.Male));
            _femaleButton.onClick.AddListener(() => SetGender(Gender.Female));

            _partsTopTab.onValueChanged.AddListener(isOn => { if (isOn) ShowTopTab(0); });
            _accessoryTopTab.onValueChanged.AddListener(isOn => { if (isOn) ShowTopTab(1); });
            _colorTopTab.onValueChanged.AddListener(isOn => { if (isOn) ShowTopTab(2); });

            foreach (var entry in _partsTabEntries)
            {
                var part = entry.Part;
                entry.Toggle?.onValueChanged.AddListener(isOn =>
                {
                    if (isOn) 
                    { 
                        _lastPartsTab = part; 
                        SwitchTab(part); 
                    }
                });
            }
            foreach (var entry in _accessoryTabEntries)
            {
                var part = entry.Part;
                entry.Toggle?.onValueChanged.AddListener(isOn =>
                {
                    if (isOn) 
                    { 
                        _lastAccessoryTab = part;
                        SwitchTab(part); 
                    }
                });
            }

            _colorPicker.onColorChange.AddListener(color =>
            {
                if (_isOpeningPicker) return;
                _previewCustomizer.SetColor(_activeColorTarget, color);
                if (_colorRows.TryGetValue(_activeColorTarget, out var row))
                    row.UpdatePreview(color);
            });

            _saveButton.onClick.AddListener(OnSaveClicked);
            _randomButton.onClick.AddListener(() => ApplyRandom().Forget());
            _resetButton.onClick.AddListener(() => ResetToDefault().Forget());
            _exitButton.onClick.AddListener(Close);
        }

        // 탭 전환

        // 최상위 탭 전환 — 0=파츠, 1=액세서리, 2=색상
        public void ShowTopTab(int index)
        {
            _partsTabGroup.SetActive(index == 0);
            _accessoryTabGroup.SetActive(index == 1);
            _colorPanel.SetActive(index == 2);

            if (index == 0)
                SwitchTab(_lastPartsTab);
            else if (index == 1)
                SwitchTab(_lastAccessoryTab);
            
            if (index != 2)
                _colorPicker.gameObject.SetActive(false);  // 색상 탭 벗어나면 피커 닫기
        }

        // 하위 탭
        private void SwitchTab(PartType part)
        {
            if (_isSwitchingTab) return;
            _isSwitchingTab = true;
            _currentTab = part;
            PopulateThumbnails(part);
            _isSwitchingTab = false;
        }

        // Female 시 FacialHair 탭 버튼 숨김 처리
        private void UpdateFacialHairTabVisibility()
        {
            var entry = System.Array.Find(_partsTabEntries, e => e.Part == PartType.FacialHair);
            if (entry.Toggle == null)
                return;
           
            bool hide =
                _currentGender == Gender.Female &&   //여성인가?
                _keyTable.GetCount(PartType.FacialHair, Gender.Female) == 0; //여성용 수염 파츠가 0개인가?
            
            entry.Toggle.gameObject.SetActive(!hide); // 둘 다 맞으면 탭 버튼을 숨겨라
        }

        // 썸네일 스크롤뷰 채우기
        private void PopulateThumbnails(PartType part)
        {
            ClearThumbnailContent();
            var names = _keyTable.GetThumbnailNames(part, _currentGender);
            int selectedIndex = _currentIndices[part];

            // 선택적 파츠: 맨 앞에 "없음(-1)" 슬롯 삽입
            if (_optionalParts.Contains(part))
            {
                var noneBtn = Instantiate(_thumbnailButtonPrefab, _thumbnailContent);
                noneBtn.Setup(part, -1, null, OnThumbnailClicked);  // sprite null
                noneBtn.SetSelected(selectedIndex == -1);
                noneBtn.SetLocked(false); // "착용 안 함"은 항상 무료
                _spawnedButtons.Add(noneBtn);
            }

            for (int i = 0; i < names.Count; i++)
            {
                Sprite sprite = 
                    !string.IsNullOrEmpty(names[i]) ?
                    _thumbnailAtlas?.GetSprite(names[i]) : null;
               
                var btn = Instantiate(_thumbnailButtonPrefab, _thumbnailContent);
                int capturedIndex = i;
                btn.Setup(part, capturedIndex, sprite, OnThumbnailClicked);
                btn.SetSelected(capturedIndex == selectedIndex);
                // 잠금 체크
                btn.SetLocked(!IsPartOwned(part, _currentGender, capturedIndex));
                _spawnedButtons.Add(btn);
            }
        }

        // 썸네일 클릭시 해당 파트, 해당 인덱스로 변경
        private void OnThumbnailClicked(PartType part, int index)
        {
            // 같은 파츠 처리중이면 무시
            if (_pendingParts.Contains(part)) return; 
            
            _currentIndices[part] = index;
            UpdateThumbnailSelection();
            ApplyPartChange(part, index).Forget();
        }

        private void UpdateThumbnailSelection()
        {
            // 예: 지금 Hair 탭이고, Hair 인덱스가 3이면 selected = 3
            int selected = _currentIndices[_currentTab];

            // _spawnedButtons는 현재 탭의 썸네일 버튼들만 들어있다.
            // Hair 탭을 클릭한 상황이라면:
            // _spawnedButtons = [Hair썸네일0, Hair썸네일1, Hair썸네일2, Hair썸네일3, Hair썸네일4...]
            // 인덱스가 3인 'Hair썸네일3'만 하이라이트 ON, 나머지는 OFF
            foreach (var btn in _spawnedButtons)
                btn.SetSelected(btn.Index == selected);
        }

        // SetPart의 상호배제 연쇄가 끝난 뒤 UI 캐시와 하이라이트를 다시 맞춘다
        private async UniTaskVoid ApplyPartChange(PartType part, int index)
        {
            _pendingParts.Add(part);
            try
            {
                // 미러 쌍이면 반대쪽도 같은 index로 순차 반영 (Right 탭은 씬에서 숨김 처리됨)
                if (MirroredPartPairs.TryGetMirror(part, out var mirrorPart))
                    await _previewCustomizer.SetPartPair(part, mirrorPart, index);
                else // 미러 쌍 아니면 기존 SetPart 그대로
                    await _previewCustomizer.SetPart(part, index);
            }
            finally
            {
                _pendingParts.Remove(part);
            }

            SyncIndicesFromCustomizer();
            UpdateThumbnailSelection();
        }


        private void ClearThumbnailContent()
        {
            foreach (var btn in _spawnedButtons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }
            _spawnedButtons.Clear();

            // 탭 전환 시 스크롤 맨 위로
            if (_thumbnailScrollRect != null)
                _thumbnailScrollRect.normalizedPosition = Vector2.up;
        }

        // Gender
        private void SetGender(Gender gender)
        {
            if (gender == _currentGender) return; // 같은 성별 재클릭 무시

            _currentGender = gender;

            // 마지막 편집 상태를 그대로 새 성별 인덱스 공간으로 이행
            // 범위 안이면 유지, 범위 밖이면: 후보가 있으면 0번, 후보가 0개면 -1(없음)
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                var count = _keyTable.GetCount(part, gender);
                var current = _currentIndices[part];
                _currentIndices[part] = current < count ? current : (count > 0 ? 0 : -1);
            }

            UpdateFacialHairTabVisibility();
            // FacialHair 탭이 숨겨지면 Hair로 이동
            if (_currentGender == Gender.Female &&
                _currentTab == PartType.FacialHair &&
                _keyTable.GetCount(PartType.FacialHair, Gender.Female) == 0)
                _currentTab = PartType.Hair;

            ApplyCurrentAppearance().Forget();
            SwitchTab(_currentTab);
        }

        // 색상
        private void InitColorRows()
        {
            foreach (ColorTarget target in System.Enum.GetValues(typeof(ColorTarget)))
            {
                var row = Instantiate(_colorRowPrefab, _colorRowParent);
                row.Setup(target, GetColorFromAppearance(target), OnColorChannelSelected);
                _colorRows[target] = row;
            }
        }

        private void OnColorChannelSelected(ColorTarget target)
        {
            _activeColorTarget = target;
            _isOpeningPicker = true;
            _colorPicker.gameObject.SetActive(true);
            var color = GetColorFromAppearance(target);
            _colorPicker.color = color;
            if (_colorRows.TryGetValue(target, out var row))
                row.UpdatePreview(color);
            _isOpeningPicker = false;
        }

        private Color GetColorFromAppearance(ColorTarget target)
        {
            var a = _previewCustomizer.GetCurrentAppearance();

            // 미로드 상태(모든 프리뷰 색상버튼 default(Color) = (0, 0, 0, 0) = 완전 투명) 방어
            // → SO 기본값으로 대체
            if (a.PrimaryColor == default && a.SkinColor == default)
                a = _defaultAppearanceSO.DefaultAppearance;

            return target switch
            {
                ColorTarget.Primary => a.PrimaryColor,
                ColorTarget.Secondary => a.SecondaryColor,
                ColorTarget.LeatherPrimary => a.LeatherPrimaryColor,
                ColorTarget.LeatherSecondary => a.LeatherSecondaryColor,
                ColorTarget.MetalPrimary => a.MetalPrimaryColor,
                ColorTarget.MetalSecondary => a.MetalSecondaryColor,
                ColorTarget.MetalDark => a.MetalDarkColor,
                ColorTarget.Hair => a.HairColor,
                ColorTarget.Skin => a.SkinColor,
                ColorTarget.Stubble => a.StubbleColor,
                ColorTarget.Scar => a.ScarColor,
                ColorTarget.Eyes => a.EyesColor,
                ColorTarget.BodyArt => a.BodyArtColor,
                _ => Color.white
            };
        }

        private void RefreshColorRows()
        {
            foreach (var kvp in _colorRows)
                kvp.Value.UpdatePreview(GetColorFromAppearance(kvp.Key));

            if (_colorPicker.gameObject.activeSelf)
                _colorPicker.color = GetColorFromAppearance(_activeColorTarget); // 색상탭 보고 있는 중이면 피커도 갱신
        }

        // 외형 적용
        private async UniTask ApplyCurrentAppearance()
        {
            await _previewCustomizer.ApplyAppearance(BuildData());

            SyncIndicesFromCustomizer();
            UpdateThumbnailSelection();
        }

        // 랜덤
        private async UniTaskVoid ApplyRandom()
        {
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                var count = _keyTable.GetCount(part, _currentGender);
                _currentIndices[part] = count > 0 ? Random.Range(0, count) : -1;
            }
            var data = BuildData(); 
            data.PrimaryColor = Random.ColorHSV(0f, 1f, 0f, 1f, 0f, 1f);
            data.SecondaryColor = Random.ColorHSV(0f, 1f, 0f, 1f, 0f, 1f);
            data.LeatherPrimaryColor = Random.ColorHSV(0f, 1f, 0f, 1f, 0f, 1f);
            data.LeatherSecondaryColor = Random.ColorHSV(0f, 1f, 0f, 1f, 0f, 1f);
            data.MetalPrimaryColor = Random.ColorHSV(0f, 1f, 0f, 1f, 0f, 1f);
            data.MetalSecondaryColor = Random.ColorHSV(0f, 1f, 0f, 1f, 0f, 1f);
            data.MetalDarkColor = Random.ColorHSV(0f, 1f, 0f, 1f, 0f, 1f);
            data.HairColor = Random.ColorHSV(0f, 1f, 0f, 1f, 0f, 1f);
            data.SkinColor = Random.ColorHSV(0.03f, 0.10f, 0.2f, 0.5f, 0.5f, 1.0f);
            data.StubbleColor = Random.ColorHSV(0.03f, 0.10f, 0.2f, 0.5f, 0.5f, 1.0f);
            data.ScarColor = Random.ColorHSV(0.03f, 0.10f, 0.2f, 0.5f, 0.5f, 1.0f);
            data.EyesColor = Random.ColorHSV(0f, 1f, 0.3f, 0.8f, 0.7f, 1.0f);
            data.BodyArtColor = Random.ColorHSV(0f, 1f, 0f, 1f, 0f, 1f);
            await _previewCustomizer.ApplyAppearance(data);
            // 동기화
            SyncIndicesFromCustomizer();
            // 현재 탭 썸네일 하이라이트 갱신
            UpdateThumbnailSelection();
            // 랜덤 색상도 스와치/피커에 반영
            RefreshColorRows();
        }


        // 리셋
        private async UniTaskVoid ResetToDefault()
        {
            var d = _defaultAppearanceSO.DefaultAppearance;
            d.Gender = _currentGender; // SO의 Gender(항상 Male)로 덮어쓰지 않고, 지금 커스터마이징 중인 성별 유지
            InitIndices(d);
            await _previewCustomizer.ApplyAppearance(d); // 인덱스+색상 전부 기본값으로
            SyncIndicesFromCustomizer(); // 상호배제 캐스케이드 결과로 _currentIndices 재동기화

            UpdateFacialHairTabVisibility(); // 성별이 바뀌었을 수 있으니 탭 가시성도 갱신
            UpdateThumbnailSelection(); // 새 성별 기준으로 현재 탭 썸네일 목록 재생성 + 선택 하이라이트
            RefreshColorRows(); // 색상 탭 스와치/피커도 갱신
        }

        // 저장

        // 미소유 파츠를 fallback 값으로 되돌린 사본을 반환.
        // fallback 지정 시: 그 값(보통 "직전 저장값")으로 되돌림 — 서버 CmdSetAppearance와 동일하게
        //   "거부하고 이전 상태 유지" 의미. fallback의 그 값마저 미소유면(예: 그사이 아이템 상실) 맨몸(0)까지 떨어뜨림.
        // fallback 생략 시: 곧바로 맨몸(0)으로 되돌림 — 되돌아갈 "직전"이 없는 자기 자신 검증(로드 시 자가 정화)용.
        private CharacterAppearanceData ValidateOwnership(CharacterAppearanceData data, CharacterAppearanceData? fallback = null)
        {
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                if (data[part] <= 0) continue; // 맨몸(0)/없음(-1)은 항상 무료라 검증 불필요
                if (IsPartOwned(part, data.Gender, data[part])) continue; // 이미 정당하게 소유한 아이템이면 검증 불필요.

                int fallbackIndex = fallback.HasValue ? fallback.Value[part] : 0; // fallback 값이 있으면 fallback으로 되돌림. 없으면 0
                if (fallbackIndex > 0 && !IsPartOwned(part, fallback.Value.Gender, fallbackIndex)) //fallback 값이 있는데 허용된 아이템이 아닌 경우. ex) lock된 아이템
                    fallbackIndex = 0; // fallback 값조차 지금은 미소유면 더 물러날 곳이 없어 맨몸으로 되돌림

                data[part] = fallbackIndex;
            }

            return data;
        }

        private bool IsPartOwned(PartType part, Gender gender, int index)
        {
            if (IsPartOwnedSelf(part, gender, index))
                return true;
            // 미러 쌍은 OR — 반대쪽만 소유해도 해금
            return MirroredPartPairs.TryGetMirror(part, out var mirror)
                && IsPartOwnedSelf(mirror, gender, index);
        }

        private bool IsPartOwnedSelf(PartType part, Gender gender, int index)
        {
            var requiredItem = _keyTable.GetRequiredItem(part, gender, index);
            return requiredItem == null || _inventoryProvider.IsOwned(requiredItem);
        }

        private void OnSaveClicked()
        {
            var previouslySaved = _saveService.Load(); // 검증 실패 파츠는 이 값(직전 저장 상태)으로 되돌림
            var data = ValidateOwnership(BuildData(), previouslySaved);

            _saveService.Save(data);
            _saveRequestPub.Publish(new CharacterAppearanceSyncRequestedEvent(data)); // 네트워크 동기화 요청
            PlayerPrefs.SetInt(PrefKeyCustomized, 1);
            PlayerPrefs.Save();

            // 검증 후 되돌려진 인덱스가 있으면 UI/미리보기도 즉시 그 모습으로 맞춘다
            InitIndices(data);
            _previewCustomizer.ApplyAppearance(data).Forget();
            UpdateThumbnailSelection();
        }

        private async UniTask ReloadFromSave()
        {
            var saved = ValidateOwnership(_saveService.Load()); // fallback 없음(이미 저장된 값 자체가 오염된 경우) → 자기 자신 정화, 실패 시 맨몸(0)
            _currentGender = saved.Gender;
            InitIndices(saved); // 디폴트 SO 대신 저장된 외형으로 인덱스 캐시 채움

            UpdateFacialHairTabVisibility();
            ShowTopTab(0); // 파츠 탭(0) 먼저

            await _previewCustomizer.ApplyAppearance(saved); // 프리뷰 캐릭터에 저장된 외형 반영
            SyncIndicesFromCustomizer(); // 상호배제 캐스케이드 결과로 _currentIndices 재동기화
            UpdateThumbnailSelection();
            RefreshColorRows(); // 색상 스와치/피커도 저장된 색으로 갱신
        }

        // 유틸
        private CharacterAppearanceData BuildData()
        {
            var data = _previewCustomizer.GetCurrentAppearance(); // 현재 색상 유지
            data.Gender = _currentGender;
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
                data[part] = _currentIndices[part]; //인덱서로 직접 접근
            return data;
        }

        // BuildData()의 반대 방향(customizer→UI) 동기화 함수
        // customizer 내부 상호배제 연쇄로 바뀐 인덱스를 UI 캐시에 반영
        private void SyncIndicesFromCustomizer()
        {
            var current = _previewCustomizer.GetCurrentAppearance();
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
                _currentIndices[part] = current[part];
        }

        private void Close()
        {
            _colorPicker.gameObject.SetActive(false); // 닫을 때도 피커를 비활성화해 다음 열릴 때 OnEnable 재오염(빨간색 초기화) 방지
            gameObject.SetActive(false);
        }
    }
}