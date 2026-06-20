using UnityEngine;

namespace MySecondWorld.Customization
{
    public class PartBoneNames : MonoBehaviour
    {
        // 추출된 파츠 프리팹에 부착 — 런타임 RemapBones에서 본 이름 매칭에 사용
        public string[] BoneNames;
        // 원본 루트본 이름
        public string RootBoneName;
    }
}