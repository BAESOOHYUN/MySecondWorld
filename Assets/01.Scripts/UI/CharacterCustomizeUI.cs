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
using TMPro;

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

        #region 필드
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

        [Header("도감(Codex) UI")]
        [SerializeField] private TextMeshProUGUI _collectionRateText;
        [SerializeField] private Slider _collectionProgressBar;
        [SerializeField] private GameObject _detailCardRoot;
        [SerializeField] private TextMeshProUGUI _detailCardName;
        [SerializeField] private TextMeshProUGUI _detailCardRarity;
        [SerializeField] private TextMeshProUGUI _detailCardStatus;
        [SerializeField] private TextMeshProUGUI _detailCardDesc;

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
        [SerializeField] private Toggle _showAllGendersToggle;   // "전체 보기" 토글 (씬에서 직접 만들어 연결)

        [Header("프리뷰 캐릭터")]
        [SerializeField] private LocalCharacterCustomizer _previewCustomizer;

        // ── DI ──
        private AvatarPartKeyTableSO _keyTable;
        private CharacterAppearanceSO _defaultAppearanceSO;
        private CharacterAppearanceService _appearanceService;
        private LocalAppearanceCache _appearanceCache;
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

        private static readonly HashSet<PartType> OptionalAccessoryParts = new HashSet<PartType>
        {
            PartType.HeadCoverings, PartType.BackAttachment, PartType.ShoulderAttachLeft,
            PartType.ElbowAttachLeft, PartType.KneeAttachLeft, PartType.HelmetAttachment, PartType.HipsAttachment
        };

        // ── UI 요소 ──
        private readonly List<PartThumbnailButton> _spawnedButtons = new();
        private readonly Dictionary<ColorTarget, ColorChannelRow> _colorRows = new();
        private readonly HashSet<PartType> _pendingParts = new();
        // ComputeVisibleAppearance가 요구하는 "마지막 선택 기록". 이 판정에서는 쓰이지 않으니 빈 것을 넘긴다.
        private static readonly Dictionary<(PartType part, string subtype), int> NoLastSelected = new();
        private bool _openCalled;
        private bool _isOpeningPicker;
        private bool _isInitialSetup; // 첫 설정 중(외형 미저장 계정) 닫을 수 없다
        private bool _isInitialized;
        private bool _showAllGenders;
        // 반대 성별 칸은 따로 관리
        // 이유 : UpdateThumbnailSelection이 _spawnedButtons를 돌며 번호가 같은 칸을 선택 표시하는데,
        // 반대 성별 칸이 섞이면 번호가 같은 칸이 잘못 켜진다.그래서 목록을 분리한다.
        private readonly List<PartThumbnailButton> _otherGenderButtons = new();

        #endregion

        #region 생명주기·초기화
        [Inject]
        public void Construct(AvatarPartKeyTableSO keyTable,
                            CharacterAppearanceSO defaultAppearanceSO,
                            CharacterAppearanceService appearanceService,
                            LocalAppearanceCache appearanceCache,
                            LocalPlayerInventoryProvider inventoryProvider,
                            IPublisher<CharacterAppearanceSyncRequestedEvent> saveRequestPub,
                            ISubscriber<RequestInitialCustomizeEvent> initialCustomizeSub)
        {
            _keyTable = keyTable;
            _defaultAppearanceSO = defaultAppearanceSO;
            _appearanceService = appearanceService;
            _appearanceCache = appearanceCache;
            _inventoryProvider = inventoryProvider;
            _saveRequestPub = saveRequestPub;
            initialCustomizeSub.Subscribe(_ => Open(isInitialSetup: true)).AddTo(destroyCancellationToken);

            _previewCustomizer ??= GetComponentInChildren<LocalCharacterCustomizer>(true) ?? FindAnyObjectByType<LocalCharacterCustomizer>();
            _previewCustomizer?.Construct(keyTable, defaultAppearanceSO);
        }

        private void Awake()
        {
#if !UNITY_SERVER
            enabled = true;
            EnsureInitialized();
#endif
        }

        // 창은 켜진 채로 만들어지므로 Start에서 한 번 숨긴다.
        // 단, Start보다 Open()이 먼저 불릴 수 있다. (예: 접속 직후 첫 설정 이벤트가 UI 생성 직후 도착)
        // 이때 그대로 숨기면 방금 연 창이 바로 닫히므로, Open()이 _openCalled = true로 표시해 두고
        // Start는 _openCalled가 false일 때(= 아직 아무도 안 열었을 때)만 숨긴다.
        private void Start()
        {
#if UNITY_SERVER
            gameObject.SetActive(false);
            enabled = false;
            return;
#else
            if (!_openCalled)             // Open이 아직 안 불렸으면 숨겨 둔다
                gameObject.SetActive(false);
#endif
        }

        private void Update()
        {
            if (gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_colorPicker != null && _colorPicker.gameObject.activeSelf)
                    _colorPicker.gameObject.SetActive(false);
                else
                    Close();
            }
        }

        private void OnDestroy()
        {
            if (_atlasHandle.IsValid())
                Addressables.Release(_atlasHandle);
        }

        private void EnsureInitialized()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            // 세이브 로드(SetIndicesFromData)가 끝나기 전에 Toggle 콜백이 먼저 SwitchTab을 부를 수 있어서
            // _currentIndices[part] 직접 인덱서 접근이 KeyNotFoundException을 던지지 않도록 기본값(0)으로 선채움
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
                _currentIndices[part] = 0;

            _lastPartsTab = _partsTabEntries.Length > 0 ? _partsTabEntries[0].Part : default;
            _lastAccessoryTab = _accessoryTabEntries.Length > 0 ? _accessoryTabEntries[0].Part : default;
            RegisterButtons();
            InitColorRows();
            if (_colorPicker != null)
                _colorPicker.gameObject.SetActive(false);
        }

        private void RegisterButtons()
        {
            // ♂/♀ 버튼은 "내 캐릭터의 성별을 바꾸는" 기능
            if (_maleButton != null)
                _maleButton.onClick.AddListener(() => SetGender(Gender.Male));
            if (_femaleButton != null)
                _femaleButton.onClick.AddListener(() => SetGender(Gender.Female));
            //  "전체 보기" 토글은 "목록에 반대 성별 파츠도 보여 줄지"를 정하는 기능
            if (_showAllGendersToggle != null)
                _showAllGendersToggle.onValueChanged.AddListener(on =>
                {
                    _showAllGenders = on;       // 켜졌는지 저장해 두고
                    SwitchTab(_currentTab);     // 지금 탭의 썸네일을 다시 그린다
                });

            if (_partsTopTab != null)
                _partsTopTab.onValueChanged.AddListener(isOn => { if (isOn) ShowTopTab(0); });
            if (_accessoryTopTab != null)
                _accessoryTopTab.onValueChanged.AddListener(isOn => { if (isOn) ShowTopTab(1); });
            if (_colorTopTab != null)
                _colorTopTab.onValueChanged.AddListener(isOn => { if (isOn) ShowTopTab(2); });

            if (_partsTabEntries != null)
            {
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
            }

            if (_accessoryTabEntries != null)
            {
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
            }

            if (_colorPicker != null)
            {
                _colorPicker.onColorChange.AddListener(color =>
                {
                    if (_isOpeningPicker) return;
                    _previewCustomizer?.SetColor(_activeColorTarget, color);
                    if (_colorRows.TryGetValue(_activeColorTarget, out var row))
                        row.UpdatePreview(color);
                });
            }

            if (_saveButton != null) _saveButton.onClick.AddListener(OnSaveClicked);
            if (_randomButton != null) _randomButton.onClick.AddListener(() => ApplyRandom().Forget());
            if (_resetButton != null) _resetButton.onClick.AddListener(() => ResetToDefault().Forget());
            if (_exitButton != null) _exitButton.onClick.AddListener(Close);
        }

        private void InitColorRows()
        {
            foreach (ColorTarget target in System.Enum.GetValues(typeof(ColorTarget)))
            {
                var row = Instantiate(_colorRowPrefab, _colorRowParent);
                row.Setup(target, GetColorFromAppearance(target), OnColorChannelSelected);
                _colorRows[target] = row;
            }
        }
        #endregion

        #region 열기·닫기
        // 패널을 열 때마다 호출 — 저장된 외형으로 다시 초기화한다.
        // (저장 안 하고 닫았다가 다시 열면 편집 중이던 미저장 상태가 그대로 남아있던 버그 수정)
        // isInitialSetup: 외형 미저장 계정의 첫 설정이면 true. 저장하기 전까지 닫을 수 없다.
        //                 메뉴 등 일반 진입은 기본값(false)이라 자유롭게 닫을 수 있다.
        public void Open(bool isInitialSetup = false)
        {
            if (_inventoryProvider?.Inventory == null) return;  // 캐릭터 도착 전에는 열지 않는다

            _isInitialSetup = isInitialSetup;                   // 첫 설정 여부로 잠금 상태를 새로 정한다
            if (_exitButton != null)
                _exitButton.interactable = !isInitialSetup;     // 첫 설정 중에는 나가기 버튼을 누를 수 없게 한다

            _openCalled = true;             // Start()가 방금 연 창을 닫지 않게 표시
            gameObject.SetActive(true);
            ReloadFromSave().Forget();      // 저장된 외형으로 다시 채운다
        }

        private void Close()
        {
            if (_isInitialSetup) return;              // 첫 설정 중에는 닫을 수 없다 (저장하기 전까지)
            _colorPicker.gameObject.SetActive(false); // 피커가 켜질 때(FlexibleColorPicker.OnEnable) 시작색(빨강)으로 알림이 나가 색이 덮이는 걸 막으려, 닫을 때 피커를 꺼 둔다
            gameObject.SetActive(false);
        }
        #endregion

        #region 탭
        // 최상위 탭 전환 — 0=파츠, 1=액세서리, 2=색상
        public void ShowTopTab(int index)
        {
            _partsTabGroup.SetActive(index == 0);
            _accessoryTabGroup.SetActive(index == 1);
            _colorPanel.SetActive(index == 2);

            if (_thumbnailScrollRect != null)
                _thumbnailScrollRect.gameObject.SetActive(index != 2);
            if (_detailCardRoot != null)
                _detailCardRoot.SetActive(index != 2);

            if (index == 0)
                SwitchTab(_lastPartsTab);
            else if (index == 1)
                SwitchTab(_lastAccessoryTab);

            if (index != 2)
                _colorPicker.gameObject.SetActive(false);  // 색상 탭 벗어나면 피커 닫기
        }

        // 하위 탭 (헤어, 헤드.... / 머리장식, 등부착물)
        private void SwitchTab(PartType part)
        {
            if (_isSwitchingTab) return;
            _isSwitchingTab = true;
            try
            {
                _currentTab = part;
                PopulateThumbnails(part);
            }
            finally
            {
                _isSwitchingTab = false;
            }
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
        #endregion

        #region 썸네일
        private async UniTask<SpriteAtlas> EnsureAtlasLoadedAsync()
        {
            if (_thumbnailAtlas != null) return _thumbnailAtlas;
            try
            {
                if (!_atlasHandle.IsValid())
                    _atlasHandle = Addressables.LoadAssetAsync<SpriteAtlas>(_keyTable.ThumbnailAtlas);
                _thumbnailAtlas = await _atlasHandle.ToUniTask();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CustomizeUI] SpriteAtlas 로드 실패: {ex.Message}");
            }
            return _thumbnailAtlas;
        }

        // (탭을 열 때) 썸네일 스크롤뷰 채우기
        private void PopulateThumbnails(PartType part)
        {
            ClearThumbnailContent();
            var names = _keyTable.GetThumbnailNames(part, _currentGender);
            int selectedIndex = _currentIndices[part];
            // BuildData 때문에 버튼마다 부르지 않고 for 밖에서 한 번만 계산
            // ㄴ프리뷰 캐릭터에서 외형을 읽어 25개 파츠 번호를 채우는 함수 = 무거움
            bool partVisible = IsPartVisible(part);   
            // 도감 수집률 계산
            int totalCount = names.Count;
            int ownedCount = 0;
            for (int i = 0; i < names.Count; i++)
            {
                if (IsPartOwned(part, _currentGender, i))
                    ownedCount++;
            }
            UpdateCollectionProgress(ownedCount, totalCount);

            // 선택적 파츠: 맨 앞에 "없음(-1)" 슬롯 삽입
            if (_optionalParts.Contains(part))
            {
                var noneBtn = Instantiate(_thumbnailButtonPrefab, _thumbnailContent);
                noneBtn.Setup(part, -1, null, OnThumbnailClicked);  // sprite null
                noneBtn.SetSelected(selectedIndex == -1);
                noneBtn.SetLocked(false); // "착용 안 함"은 항상 무료
                noneBtn.SetLabel("해제");
                noneBtn.SetGenderBadge(null); // 해제 버튼에는 성별 배지를 달지 않는다
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
                btn.SetSelected(partVisible && capturedIndex == selectedIndex);
                // 잠금 체크
                btn.SetLocked(!IsPartOwned(part, _currentGender, capturedIndex));

                // 도감 라벨 및 등급 색상
                var reqItem = _keyTable.GetRequiredItem(part, _currentGender, capturedIndex);
                // 성별 뱃지 추가
                btn.SetGenderBadge(_keyTable.IsGenderSpecific(part, _currentGender, capturedIndex) ? _currentGender : (Gender?)null);

                if (reqItem != null)
                {
                    btn.SetLabel(reqItem.Title);
                    btn.SetRarity(reqItem.GetRarityColor());
                }
                else
                {
                    string sub = _keyTable.GetSubType(part, _currentGender, capturedIndex);
                    btn.SetLabel(!string.IsNullOrEmpty(sub) && sub != "None" && part != PartType.Head ? sub : $"#{capturedIndex + 1:D2}");
                }

                _spawnedButtons.Add(btn);
            }

            if (_showAllGenders) AppendOtherGenderThumbnails(part);
            UpdateDetailCard(part, selectedIndex, _currentGender);
        }

        // 썸네일 클릭시 해당 파트, 해당 인덱스로 변경
        private void OnThumbnailClicked(PartType part, int index)
        {
            // 투구 장식 차단
            if (part == PartType.HelmetAttachment && index > 0
                && !MutualExclusionResolver.IsHelmetAttachmentAllowed(BuildData(), _currentGender, _keyTable))
                return;   // 헬멧을 고르기 전에는 투구 장식을 입어 볼 수 없다

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
            bool partVisible = IsPartVisible(_currentTab);   // 한 번만 계산

            foreach (var btn in _spawnedButtons)
                btn.SetSelected(partVisible && btn.Index == selected);

            UpdateDetailCard(_currentTab, selected, _currentGender);
        }

        private void UpdateCollectionProgress(int owned, int total)
        {
            if (_collectionRateText != null)
            {
                float pct = total > 0 ? ((float)owned / total * 100f) : 100f;
                _collectionRateText.text = $"도감 수집률: <color=#FFE680>{owned}</color> / {total} ({pct:F0}%)";
            }
            if (_collectionProgressBar != null)
            {
                _collectionProgressBar.minValue = 0;
                _collectionProgressBar.maxValue = total > 0 ? total : 1;
                _collectionProgressBar.value = owned;
            }
        }

        // UpdateDetailCard가 바꾸는 건 화면에 하나뿐인 썸네일 상세 정보 패널(이름, 등급, 보유 상태, 설명)
        private void UpdateDetailCard(PartType part, int index, Gender gender)
        {
            if (_detailCardRoot == null) return;

            if (index == -1)
            {
                if (_detailCardName != null) _detailCardName.text = "장착 해제 (미착용)";
                if (_detailCardRarity != null) _detailCardRarity.text = "<color=#B0C4DE>[기본]</color>";
                if (_detailCardStatus != null) _detailCardStatus.text = "<color=#4BC6DF>● 착용 안 함</color>";
                if (_detailCardDesc != null) _detailCardDesc.text = "해당 부위의 외형 파츠를 착용하지 않습니다.";
                return;
            }

            var reqItem = _keyTable.GetRequiredItem(part, gender, index);
            bool isOwned = IsPartOwned(part, gender, index);

            if (reqItem != null)
            {
                if (_detailCardName != null) _detailCardName.text = reqItem.Title;
                if (_detailCardRarity != null)
                {
                    string colorHex = ColorUtility.ToHtmlStringRGB(reqItem.GetRarityColor());
                    _detailCardRarity.text = $"<color=#{colorHex}>[{reqItem.Rarity}]</color>";
                }
                if (_detailCardStatus != null)
                {
                    _detailCardStatus.text = isOwned
                        ? "<color=#55E88F>● 도감 수집 완료 (소유 중)</color>"
                        : "<color=#FF8A8A>○ 미소유 (상점/던전 획득 필요)</color>";
                }
                if (_detailCardDesc != null) _detailCardDesc.text = reqItem.Description;
            }
            else
            {
                string sub = _keyTable.GetSubType(part, gender, index);
                string title = !string.IsNullOrEmpty(sub) && sub != "None" && part != PartType.Head ? sub : $"{part} #{index + 1:D2}";
                if (_detailCardName != null) _detailCardName.text = title;
                if (_detailCardRarity != null) _detailCardRarity.text = "<color=#B0C4DE>[기본 외형]</color>";
                if (_detailCardStatus != null)
                {
                    _detailCardStatus.text = isOwned
                        ? "<color=#55E88F>● 기본 해금</color>"
                        : "<color=#FF8A8A>○ 미소유</color>";
                }
                if (_detailCardDesc != null) _detailCardDesc.text = "모험가의 기본 외형 파츠입니다.";
            }
        }

        private void ClearThumbnailContent()
        {
            foreach (var btn in _spawnedButtons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }
            _spawnedButtons.Clear();

            foreach (var btn in _otherGenderButtons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }
            _otherGenderButtons.Clear();

            // 탭 전환 시 스크롤 맨 위로
            if (_thumbnailScrollRect != null)
                _thumbnailScrollRect.normalizedPosition = Vector2.up;
        }

        // 이 파츠가 지금 화면에 보이는가 (헬멧·투구 장식·모자 등에 가려지면 선택 표시를 끈다)
        private bool IsPartVisible(PartType part)
        {
            // 가려질 수 있는 파츠만 판정한다 (나머지는 항상 보이는 것으로)
            if (part != PartType.HeadCoverings && part != PartType.Hair
                && part != PartType.Eyebrows && part != PartType.FacialHair)
                return true;

            var desired = BuildData();   // UI가 고른 값
            var visible = MutualExclusionResolver.ComputeVisibleAppearance(desired, _currentGender, _keyTable, NoLastSelected);
            return visible[part] == desired[part];   // 보이는 값이 입은 값과 다르면 가려진 것
        }

        // "전체 보기" : 반대 성별 전용 파츠를 목록 뒤에 덧붙인다. 눌러도 입어 보지 않고 상세 카드만 보여 준다.
        private void AppendOtherGenderThumbnails(PartType part)
        {
            Gender other = _currentGender == Gender.Male ? Gender.Female : Gender.Male;
            var names = _keyTable.GetThumbnailNames(part, other);

            // 버튼 N개를 만들고 각자에게 "눌렸을 때 할 일"을 달아 둠
            for (int i = 0; i < names.Count; i++)
            {
                if (!_keyTable.IsGenderSpecific(part, other, i)) continue; // 공용은 이미 위 목록에 있다
                Sprite sprite = !string.IsNullOrEmpty(names[i]) ? _thumbnailAtlas?.GetSprite(names[i]) : null;
                var btn = Instantiate(_thumbnailButtonPrefab, _thumbnailContent);
                // 람다가 반복 변수 i를 쓰지 않고 (p, idx) 인자만 써서, 반복문 변수 캡처 문제가 없다
                //                          ┌ "버튼이 눌리면 이걸 실행해라"라는 예약(여기서 실행되지 않음)
                btn.Setup(part, i, sprite, (p, idx) => UpdateDetailCard(p, idx, other));   // 입지 못하고 카드만 볼수 있음
                btn.SetSelected(false);
                btn.SetLocked(!IsPartOwned(part, other, i));
                btn.SetGenderBadge(other);

                var reqItem = _keyTable.GetRequiredItem(part, other, i);
                btn.SetLabel(reqItem != null ? reqItem.Title : $"#{i + 1:D2}");
                if (reqItem != null) btn.SetRarity(reqItem.GetRarityColor());

                _otherGenderButtons.Add(btn);
            }
        }

        #endregion

        #region 파츠·성별 적용

        // 외형 데이터(CharacterAppearanceData)의 파츠 번호를 UI 캐시(_currentIndices)로 복사한다 (외형 데이터 → UI 방향)
        private void SetIndicesFromData(CharacterAppearanceData data)
        {
            // 저장된 외형을 로드해서 인덱스/ 프리뷰에 반영
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
                _currentIndices[part] = data[part];
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

            SyncIndicesFromPreview();
            UpdateThumbnailSelection();
        }

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

        private async UniTask ApplyCurrentAppearance()
        {
            await _previewCustomizer.ApplyAppearance(BuildData());

            SyncIndicesFromPreview();
            UpdateThumbnailSelection();
        }

        // 프리뷰 캐릭터의 현재 파츠 번호를 UI 캐시(_currentIndices)로 가져온다 (프리뷰 → UI 방향)
        // CharacterCustomizerUI.ApplyAppearance의 상호배제 연쇄(헬멧을 쓰면 머리카락이 0이 되는 식)로
        // 프리뷰가 UI가 고른 값과 달라질 수 있어서, 프리뷰에 적용한 뒤에는 항상 이 함수로 UI 캐시를 프리뷰 쪽에 맞춘다
        private void SyncIndicesFromPreview()
        {
            var current = _previewCustomizer.GetCurrentAppearance();
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
                _currentIndices[part] = current[part];
        }
        #endregion

        #region 색상
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
            var appearance = _previewCustomizer.GetCurrentAppearance();

            // 미로드 상태(모든 프리뷰 색상버튼 default(Color) = (0, 0, 0, 0) = 완전 투명) 방어
            // → SO 기본값으로 대체
            if (appearance.PrimaryColor == default && appearance.SkinColor == default)
                appearance = _defaultAppearanceSO.DefaultAppearance;

            return appearance[target];
        }

        private void RefreshColorRows()
        {
            foreach (var kvp in _colorRows)
                kvp.Value.UpdatePreview(GetColorFromAppearance(kvp.Key));

            if (_colorPicker.gameObject.activeSelf)
                _colorPicker.color = GetColorFromAppearance(_activeColorTarget); // 색상탭 보고 있는 중이면 피커도 갱신
        }
        #endregion

        #region 랜덤·리셋
        private async UniTaskVoid ApplyRandom()
        {
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                if (MirroredPartPairs.IsRight(part)) continue; // Right는 아래서 Left 값을 그대로 복사받음

                var count = _keyTable.GetCount(part, _currentGender);
                if (count <= 0)
                {
                    // 현재 성별에서 옵션이 아예 0개인 경우 (여자 : 수염 없음)
                    // "선택 불가/없음"을 뜻하는 센티널 값 -1을 넣어서 이후 로직이 이 파츠를 렌더링 안 하고 건너뜀
                    _currentIndices[part] = -1;
                }
                else if (OptionalAccessoryParts.Contains(part) && Random.value < 0.5f)
                {
                    // 7개 액세서리 슬롯(헤드커버링/등장식/어깨/팔꿈치/무릎/헬멧장식/허리장식) 중 하나인가 && 0.5 미만인가
                    // 둘 다 참이면 무조건 인덱스 0(착용 안 함)으로 고정
                    _currentIndices[part] = 0; // 절반 확률로 "착용 안 함"
                }
                else
                {
                    // 1. 애초에 액세서리 슬롯이 아닌 파츠(예: Torso, Hair)
                    // — "착용안함 확률 부스트" 대상이 아니니까 원래 하던 대로 그냥 균등 랜덤
                    // 2. 액세서리 슬롯이긴 한데 Random.value < 0.5f에서 안 걸린 나머지 절반의 경우
                    // — 이때는 "뭔가 착용은 하기로 했으니" 전체 옵션 중에서 균등하게 랜덤 선택
                    _currentIndices[part] = Random.Range(0, count);
                }

                if (MirroredPartPairs.TryGetMirror(part, out var mirror))
                    _currentIndices[mirror] = _currentIndices[part];
            }

            // HelmetAttachment를 실제로 착용했는데, Head가 아직 No_Elements(헬멧용 얼굴)가 아니면,
            // No_Elements 후보 중 하나를 랜덤으로 골라준다.
            // (안 그러면 MutualExclusionResolver의 폴백이 "기억된 값 없음" 상태로 항상 첫 번째만 고름)
            if (_currentIndices.TryGetValue(PartType.HelmetAttachment, out var helmetAttachmentIndex)
                && helmetAttachmentIndex > 0
                && _keyTable.GetSubType(PartType.Head, _currentGender, _currentIndices[PartType.Head]) != "No_Elements")
            {
                // Head 파츠 중에서 '헬멧 쓸 수 있는(No_Elements) 얼굴'들만 골라서 모아놓고, 그중 하나를 랜덤으로 뽑아야 함
                int headIndex = _keyTable.GetCount(PartType.Head, _currentGender);
                var noElementsCandidates = new List<int>();
                for (int i = 0; i < headIndex; i++)
                {
                    // 각 인덱스가 "No_Elements"인지 확인후 후보에 넣는다
                    if (_keyTable.GetSubType(PartType.Head, _currentGender, i) == "No_Elements")
                        noElementsCandidates.Add(i);
                }
                // 후보가 있으면 랜덤으로 선택
                if (noElementsCandidates.Count > 0)
                    _currentIndices[PartType.Head] = noElementsCandidates[Random.Range(0, noElementsCandidates.Count)];
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
            SyncIndicesFromPreview();
            // 현재 탭 썸네일 하이라이트 갱신
            UpdateThumbnailSelection();
            // 랜덤 색상도 스와치/피커에 반영
            RefreshColorRows();
        }


        private async UniTaskVoid ResetToDefault()
        {
            var d = _defaultAppearanceSO.DefaultAppearance;
            d.Gender = _currentGender; // SO의 Gender(항상 Male)로 덮어쓰지 않고, 지금 커스터마이징 중인 성별 유지
            SetIndicesFromData(d);
            await _previewCustomizer.ApplyAppearance(d); // 인덱스+색상 전부 기본값으로
            SyncIndicesFromPreview(); // 상호배제 캐스케이드 결과로 _currentIndices 재동기화

            UpdateFacialHairTabVisibility(); // 성별이 바뀌었을 수 있으니 탭 가시성도 갱신
            UpdateThumbnailSelection(); // 새 성별 기준으로 현재 탭 썸네일 목록 재생성 + 선택 하이라이트
            RefreshColorRows(); // 색상 탭 스와치/피커도 갱신
        }
        #endregion

        #region 소유 검증
        private CharacterAppearanceData ValidateOwnership(CharacterAppearanceData data, CharacterAppearanceData? fallback = null)
             => PartOwnership.ValidateOwnership(_keyTable, data, _inventoryProvider.IsOwned, fallback);
        private bool IsPartOwned(PartType part, Gender gender, int index)
            => PartOwnership.IsPartOwned(_keyTable, part, gender, index, _inventoryProvider.IsOwned);
        #endregion

        #region 저장·불러오기
        private void OnSaveClicked()
        {
            var previouslySaved = _appearanceCache.Get();// 되돌릴 기준값 = 직전 저장 상태 (성별이 같을 때만 쓰인다)
            var data = ValidateOwnership(BuildData(), previouslySaved); // 미소유 파츠는 기준값으로 되돌린다.
                                                                        // 성별이 바뀌었으면 번호가 다른 옷을 가리키므로 맨몸(0)으로 되돌린다
            _appearanceCache.Set(data);                                 // 새 외형을 클라 기억에 넣음
            var slots = _appearanceService.ReconcileEquippedSlots(_inventoryProvider.Inventory); // 서로 어긋난 두 값을 맞춰서 일치시킴
            _saveRequestPub.Publish(new CharacterAppearanceSyncRequestedEvent(data, slots));     // 서버로 보내 네트워크 동기화 요청

            // 검증 후 되돌려진 인덱스가 있으면 UI/미리보기도 즉시 그 모습으로 맞춘다
            SetIndicesFromData(data);
            _previewCustomizer.ApplyAppearance(data).Forget();
            UpdateThumbnailSelection();

            _isInitialSetup = false;
            if (_exitButton != null) _exitButton.interactable = true;
        }

        private async UniTask ReloadFromSave()
        {
            await EnsureAtlasLoadedAsync();

            var saved = _appearanceCache.Get(); // 열릴 때는 현재 착용 중인 로컬 세이브 외형을 그대로 프리뷰에 로드
            _currentGender = saved.Gender;
            SetIndicesFromData(saved); // 저장된 외형으로 인덱스 캐시 채움

            UpdateFacialHairTabVisibility();
            ShowTopTab(0); // 파츠 탭(0) 먼저

            if (_previewCustomizer != null)
                await _previewCustomizer.ApplyAppearance(saved); // 프리뷰 캐릭터에 현재 옷 그대로 반영

            SyncIndicesFromPreview(); // 상호배제 캐스케이드 결과로 _currentIndices 재동기화
            UpdateThumbnailSelection();
            RefreshColorRows(); // 색상 스와치/피커도 저장된 색으로 갱신
        }

        private CharacterAppearanceData BuildData()
        {
            var data = _previewCustomizer.GetCurrentAppearance(); // 현재 색상 유지
            data.Gender = _currentGender;
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
                data[part] = _currentIndices[part]; //인덱서로 직접 접근
            return data;
        }
        #endregion
    }
}