using UnityEngine;

namespace MySecondWorld.Save
{
    [System.Serializable]
    public struct SaveData
    {
        public string[] lastSceneNames;
        public Vector3 lastPosition;
    }
}
