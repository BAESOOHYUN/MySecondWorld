using MySecondWorld.Customization;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.UI
{
    // 썸네일 스크롤뷰 안에 동적으로 생성되는 아이템 1개
    public class PartThumbnailButton : MonoBehaviour
    {
        [SerializeField] private Image _thumbnail; // 썸네일 스프라이트
        [SerializeField] private GameObject _selectedFrame; // 선택 강조 테두리 (껐다 켬)
        [SerializeField] private Button _button;
        [SerializeField] private Sprite _noneSprite;
        [SerializeField] private GameObject _lockIcon; // 미소유 표시 오버레이

        public PartType Part { get; private set; }

        public int Index { get; private set; }

        public void Setup(PartType part, int index, Sprite sprite, Action<PartType, int> onClick)
        {
            Part = part;
            Index = index;
            _thumbnail.sprite = sprite != null ? sprite : _noneSprite; // null이면 X 아이콘
            _thumbnail.enabled = sprite != null; // 못 받은 슬롯은 빈칸 처리
            _thumbnail.enabled = _thumbnail.sprite != null; // 스프라이트가 배정되어 있으면 Image 활성화
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onClick?.Invoke(Part, Index));
        }

        public void SetSelected(bool selected)
        {
            if (_selectedFrame != null)
                _selectedFrame.SetActive(selected);
        }

        public void SetLocked(bool locked)
        {
            if (_lockIcon != null)
                _lockIcon.SetActive(locked);
        }
    }
}