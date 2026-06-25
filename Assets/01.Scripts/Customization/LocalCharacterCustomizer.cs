using UnityEngine;

namespace MySecondWorld.Customization
{
    // 로컬에서만 외형 적용, 이벤트 없음
    public class LocalCharacterCustomizer : CharacterCustomizerBase
    {
        protected override void Start()
        {
            var anim = GetComponent<Animator>() ?? GetComponentInParent<Animator>();
            if (anim != null)
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // 외형 적용은 CharacterCustomizeUI가 저장된 데이터로 직접 호출한다
        }
        
        protected override void OnPartInstantiated(GameObject instance)
        {
            SetLayerRecursive(instance, gameObject.layer);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
