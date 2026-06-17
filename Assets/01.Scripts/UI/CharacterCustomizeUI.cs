using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using MySecondWorld.Character;
using MySecondWorld.Customization;

namespace MySecondWorld.UI
{
    public class CharacterCustomizeUI : MonoBehaviour
    {
        [Header("패널")]
        [SerializeField] private GameObject _partsPanel;
        [SerializeField] private GameObject _accessaryPanel;
        [SerializeField] private GameObject _colorPanel;

        [Header("Gender")]
        [SerializeField] private Button _maleButton;
        [SerializeField] private Button _femaleButton;

        [Header("파츠 슬롯 — 인덱스 = (int)PartType, 크기 25")]
        [SerializeField] private Button[] _prevButtons = new Button[25];
        [SerializeField] private Button[] _nextButtons = new Button[25];

        [Header("색상")]
        [SerializeField] private FlexibleColorPicker _colorPicker;
        [SerializeField] private Image _colorPreview;
        [SerializeField] private ColorTarget _activeColorTarget = ColorTarget.Primary;

        [Header("공통")]
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _randomButton;

        private CharacterCustomizer _customizer;
        private AvatarPartKeyTableSO _keyTable;
        private Gender _currentGender = Gender.Male;
        private readonly Dictionary<PartType, int> _currentIndices = new(); // 각 파츠의 인덱스

        [Inject]
        public void Construct(CharacterCustomizer customizer, AvatarPartKeyTableSO keyTable)
        {
            _customizer = customizer;
            _keyTable = keyTable;
        }

        private void Start()
        {
            InitIndices();
            RegisterButtons();
            ShowTab(_partsPanel);
        }

        // 초기값 설정
        private void InitIndices()
        {
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
                _currentIndices[part] = 0;
        }

        private void RegisterButtons()
        {
            _maleButton.onClick.AddListener(() => SetGender(Gender.Male));
            _femaleButton.onClick.AddListener(() => SetGender(Gender.Female));

            for (int i = 0; i < 25; i++)
            {
                var part = (PartType)i;
                _prevButtons[i]?.onClick.AddListener(() => CyclePart(part, -1).Forget());
                _nextButtons[i]?.onClick.AddListener(() => CyclePart(part, +1).Forget());
            }

            _colorPicker.onColorChange.AddListener(color => _customizer.SetColor(_activeColorTarget, color));
            _saveButton.onClick.AddListener(OnSaveClicked);
            _randomButton.onClick.AddListener(() => ApplyRandom().Forget());
        }

        // 탭 버튼 OnClick에서 인자로 각 패널 GameObject 드래그해서 호출
        public void ShowTab(GameObject targetPanel)
        {
            _partsPanel.SetActive(_partsPanel == targetPanel);
            _accessaryPanel.SetActive(_accessaryPanel == targetPanel);
            _colorPanel.SetActive(_colorPanel == targetPanel);
        }

        // 색상 탭 내 Primary/Secondary/Skin 버튼 OnClick에서 호출 (int 캐스팅)
        public void SetActiveColorTarget(int target) => _activeColorTarget = (ColorTarget)target;

        private void SetGender(Gender gender)
        {
            _currentGender = gender;
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
            {
                var count = _keyTable.GetCount(part, _currentGender);
                
                // 추출한 count보다 index가 높으면 0
                if (_currentIndices[part] >= count)
                    _currentIndices[part] = 0;
            }
            ApplyCurrentAppearance().Forget();
        }

        private async UniTask CyclePart(PartType part, int delta)
        {
            var count = _keyTable.GetCount(part, _currentGender);
            if (count == 0) return;

            // +count는 음수 모듈로 버그를 막는 안전장치.
            // delta가 항상 -1 또는 +1이라 count 하나만 더해도 항상 양수가 보장된다.
            // count 없으면 (0 + -1) % 5 = -1 % 5 = -1  ← C#에서 음수 모듈로는 음수가 나옴!
            // count 더하면
            // 뒤로 갈 때  (-1) (0 + -1 + 5) % 5 = 4 % 5 = 4  ← 맨 마지막 인덱스로 순환
            // 앞으로 갈 때(+1) (4 + 1 + 5) % 5 = 10 % 5 = 0  ← 맨 처음으로 순환
            _currentIndices[part] = (_currentIndices[part] + delta + count) % count;
            await _customizer.SetPart(part, _currentIndices[part]);
        }

        // 외부(Phase D)에서 저장된 색상 로드 시 슬라이더 초기화에 사용
        public void SyncSliderToColor(Color color)
        {
            _colorPicker.color = color;
        }

        private async UniTask ApplyCurrentAppearance()
        {
            var data = BuildData();
            await _customizer.ApplyAppearance(data);
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
            data.SkinColor = Random.ColorHSV(0.03f, 0.10f, 0.2f, 0.5f, 0.5f, 1.0f);
            await _customizer.ApplyAppearance(data);
        }

        private CharacterAppearanceData BuildData()
        {
            var data = new CharacterAppearanceData { Gender = _currentGender };
            foreach (PartType part in System.Enum.GetValues(typeof(PartType)))
                data[part] = _currentIndices[part]; //인덱서로 직접 접근
            return data;
        }

        private void OnSaveClicked()
        { 
        }
    }
}
