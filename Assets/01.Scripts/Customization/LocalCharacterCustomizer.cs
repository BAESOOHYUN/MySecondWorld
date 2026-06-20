using UnityEngine;

namespace MySecondWorld.Customization
{
    // 로컬에서만 외형 적용, 이벤트 없음
    public class LocalCharacterCustomizer : CharacterCustomizerBase
    {
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
