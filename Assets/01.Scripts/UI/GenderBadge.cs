using MySecondWorld.Customization;
using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.UI
{
    // 남/여 아이콘 한 개를 성별에 맞게 바꿔 보여 주는 작은 부품 (커스터마이징 썸네일·인벤토리 슬롯 공용)
    public class GenderBadge : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private Sprite _maleSprite;
        [SerializeField] private Sprite _femaleSprite;

        public void Set(Gender? gender)
        {
            gameObject.SetActive(gender.HasValue);   // 값이 없으면 숨김
            if (gender.HasValue)
                _image.sprite = gender.Value == Gender.Male ? _maleSprite : _femaleSprite;
        }
    }
}