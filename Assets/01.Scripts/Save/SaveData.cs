using UnityEngine;

namespace MySecondWorld.Save
{
    [System.Serializable]
    public struct SaveData
    {
        public string[] lastSceneNames;
        public Vector3 lastPosition;

        // 컴패니언
        public string[] recruitedCompanionIds;
        public bool companionAlive;   // 마지막으로 기록된 생존 여부
        public bool companionPresent; // 마지막으로 기록된 소환/소환해제 여부
    }
}
