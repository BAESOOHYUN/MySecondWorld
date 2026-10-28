using UnityEngine;

namespace MySecondWorld.CameraSystem
{
    /// <summary>
    /// NPC 대화 시 카메라 클로즈업 및 복귀를 제어하는 서비스 인터페이스.
    /// 구현체와 함께 Camera 어셈블리에 둔다. AI/UI는 Camera를 단방향으로 참조한다.
    /// </summary>
    public interface IDialogueCameraService
    {
        void FocusOnNPC(Transform npcTransform, Transform playerTransform = null);
        void ReleaseFocus();
    }
}
