using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using VContainer;
using MessagePipe;
using MySecondWorld.Customization;
using MySecondWorld.Save;
using Random = UnityEngine.Random;

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

        [Header("파츠 탭 (Hair~LegRight 14개)")]
        [SerializeField] private PartTabEntry[] _partsTabEntries;

        [Header("액세서리 탭 (Ear~HipsAttachment 11개)")]
        [SerializeField] private PartTabEntry[] _accessoryTabEntries;

        // ── 썸네일 스크롤뷰 ──
        [Header("썸네일 스크롤뷰")]
        [SerializeField] private ScrollRect _thumbnailScrollRect;
        [SerializeField] private Transform _thumbnailContent;  // GridLayoutGroup 부착
        [SerializeField] private PartThumbnailButton _thumbnailButtonPrefab;

        [Header("Gender")]
        [SerializeField] private Button _maleButton;
        [SerializeField] private Button _femaleButton;

        [Header("색상")]
        [SerializeField] private Transform _colorRowParent;        // VerticalLayoutGroup 부모
        [SerializeField] private ColorChannelRow _colorRowPrefab;  // 행 프리팹
        [SerializeField] private FlexibleColorPicker _colorPicker; // 공유 팝업 피커
        [SerializeField] private ColorTarget _activeColorTarget = ColorTarget.Primary;

        [Header("공통")]
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _randomButton;
        [SerializeField] private Button _resetButton;
        
        // ── DI ──
        private CharacterCustomizer _customizer;
        private AvatarPartKeyTableSO _keyTable;
        private CharacterSaveService _saveService;
        private IDisposable _subscription;

        // ── 런타임 상태 ──
        private Gender _currentGender = Gender.Male;
        private PartType _currentTab = PartType.Hair;
        private readonly Dictionary<PartType, int> _currentIndices = new(); // 각 파츠의 인덱스
        // 현재 탭에 표시된 썸네일 버튼 목록 (탭 전환 시 정리용)
        private readonly List<PartThumbnailButton> _spawnedButtons = new();
        // 현재 탭 썸네일 Addressables 핸들 (탭 전환 시 Release 필요)
        private readonly List<UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Sprite>> _thumbHandles = new();
        private bool _isSwitchingTab; 
        private readonly Dictionary<ColorTarget, ColorChannelRow> _colorRows = new();

        [Inject]
        public void Construct(CharacterCustomizer customizer,
                              AvatarPartKeyTableSO keyTable,
                              CharacterSaveService saveService,
                              ISubscriber<CharacterCustomizeOpenEvent> openSub)
        {
            _customizer = customizer;
            _keyTable = keyTable;
            _saveService = saveService;
            _subscription = openSub.Subscribe(_ => gameObject.SetActive(true));
        }

        private void Start()
        {
            InitIndices();
            RegisterButtons();
            InitColorRows();
            _colorPicker.gameObject.SetActive(false);
            UpdateFacialHairTabVisibility();
            ShowTopTab(0); // 파츠 탭 먼저
            SwitchTab(_currentTab).Forget();
            gameObject.SetActive(false); // 초기화 끝나고 패널 숨기기
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            ReleaseThumbnailHandles();
        }

        // 인덱스 초기화
        private void InitIndices()
        {
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
                _currentIndices[part] = 0;
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
                    if (isOn) SwitchTab(part).Forget();
                });
            }
            foreach (var entry in _accessoryTabEntries)
            {
                var part = entry.Part;
                entry.Toggle?.onValueChanged.AddListener(isOn =>
                {
                    if (isOn) SwitchTab(part).Forget();
                });
            }

            _colorPicker.onColorChange.AddListener(color =>
            {
                _customizer.SetColor(_activeColorTarget, color);
                if (_colorRows.TryGetValue(_activeColorTarget, out var row))
                    row.UpdatePreview(color);
            });

            _saveButton.onClick.AddListener(OnSaveClicked);
            _randomButton.onClick.AddListener(() => ApplyRandom().Forget());
            _resetButton.onClick.AddListener(() => ResetToDefault().Forget());
        }

        // 탭 전환

        // 최상위 탭 전환 — 0=파츠, 1=액세서리, 2=색상
        public void ShowTopTab(int index)
        {
            _partsTabGroup.SetActive(index == 0);
            _accessoryTabGroup.SetActive(index == 1);
            _colorPanel.SetActive(index == 2);
        }

        // 하위 탭
        private async UniTaskVoid SwitchTab(PartType part)
        {
            if (_isSwitchingTab) 
                return;
            
            _isSwitchingTab = true;

            _currentTab = part;
            await PopulateThumbnails(part);
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
        private async UniTask PopulateThumbnails(PartType part)
        {
            ClearThumbnailContent();

            // thumbRefs = AssetReferenceSprite
            var thumbRefs = _keyTable.GetThumbnails(part, _currentGender);
            int selectedIndex = _currentIndices[part];

            for (int i = 0; i < thumbRefs.Count; i++)
            {
                Sprite sprite = null;
                if (thumbRefs[i] != null && thumbRefs[i].RuntimeKeyIsValid())
                {
                    // 로드 요청 → 영수증(AsyncOperationHandle) 반환
                    var handle = Addressables.LoadAssetAsync<Sprite>(thumbRefs[i]);
                    // _thumbHandles 리스트에 영수증을 모아뒀다가 탭 전환 시 ReleaseThumbnailHandles()에서 일괄 반납
                    _thumbHandles.Add(handle);
                    // 영수증으로 실제 스프라이트 꺼내기
                    sprite = await handle;
                }
                var btn = Instantiate(_thumbnailButtonPrefab, _thumbnailContent);
                int capturedIndex = i;
                btn.Setup(part, capturedIndex, sprite, OnThumbnailClicked);
                btn.SetSelected(capturedIndex == selectedIndex);
                _spawnedButtons.Add(btn);
            }
        }

        // 썸네일 클릭시 해당 파트, 해당 인덱스로 변경
        private void OnThumbnailClicked(PartType part, int index)
        {
            _currentIndices[part] = index;
            UpdateThumbnailSelection();
            _customizer.SetPart(part, index).Forget();
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

        private void ClearThumbnailContent()
        {
            foreach (var btn in _spawnedButtons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }
            _spawnedButtons.Clear();
            ReleaseThumbnailHandles();

            // 탭 전환 시 스크롤 맨 위로
            if (_thumbnailScrollRect != null)
                _thumbnailScrollRect.normalizedPosition = Vector2.up;
        }

        private void ReleaseThumbnailHandles()
        {
            foreach (var handle in _thumbHandles)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            _thumbHandles.Clear();
        }

        // Gender
        private void SetGender(Gender gender)
        {
            _currentGender = gender;
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                var count = _keyTable.GetCount(part, _currentGender);

                // 추출한 count보다 index가 높으면 0
                // ex) Male → Female로 바꿀 때 성별마다 파츠 개수가 다름
                // FacialHair 파츠 개수 Male = 20개, Female = 0개
                if (_currentIndices[part] >= count)
                    _currentIndices[part] = 0;
            }

            UpdateFacialHairTabVisibility();
            // FacialHair 탭이 숨겨지면 Hair로 이동
            if (_currentGender == Gender.Female &&
                _currentTab == PartType.FacialHair &&
                _keyTable.GetCount(PartType.FacialHair, Gender.Female) == 0)
                _currentTab = PartType.Hair;

            ApplyCurrentAppearance().Forget();
            SwitchTab(_currentTab).Forget();
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
            _colorPicker.color = GetColorFromAppearance(target);
            _colorPicker.gameObject.SetActive(true);
        }

        private Color GetColorFromAppearance(ColorTarget target)
        {
            var a = _customizer.GetCurrentAppearance();
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

        // 외형 적용
        private async UniTask ApplyCurrentAppearance()
        {
            await _customizer.ApplyAppearance(BuildData());
        }

        private async UniTaskVoid ApplyRandom()
        {
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                var count = _keyTable.GetCount(part, _currentGender);
                _currentIndices[part] = count > 0 ? Random.Range(0, count) : 0;
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
            await _customizer.ApplyAppearance(data);

            // 현재 탭 썸네일 하이라이트 갱신
            UpdateThumbnailSelection();
        }


        // 리셋
        private async UniTaskVoid ResetToDefault()
        {
            InitIndices();
            await ApplyCurrentAppearance();
            UpdateThumbnailSelection();
        }

        // 저장
        private void OnSaveClicked()
        {
            var data = BuildData();
            _saveService.Save(data);
        }

        // 유틸
        private CharacterAppearanceData BuildData()
        {
            var data = new CharacterAppearanceData { Gender = _currentGender };
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
                data[part] = _currentIndices[part]; //인덱서로 직접 접근
            return data;
        }
    }
}
