using UnityEngine;

namespace MySecondWorld.AI
{
    // API 키처럼 git에 올리면 안 되는 값을 로컬 전용 파일에서 읽어온다.
    // 실제 값은 Assets/Resources/Secrets/<keyName>.txt에 두고 .gitignore로 제외한다 —
    // 이 스크립트 자체와 파일 경로는 커밋돼도 안전하다(값이 없으면 그냥 null).
    public static class SecretKeyLoader
    {
        public static string Load(string keyName)
        {
            var asset = Resources.Load<TextAsset>($"Secrets/{keyName}");
            if (asset == null)
            {
                Debug.LogWarning($"[SecretKeyLoader] 'Assets/Resources/Secrets/{keyName}.txt'가 없습니다 — 이 값이 필요한 기능은 동작하지 않습니다.");
                return null;
            }
            return asset.text.Trim();
        }
    }
}
