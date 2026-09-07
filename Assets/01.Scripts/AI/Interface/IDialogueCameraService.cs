using UnityEngine;

namespace MySecondWorld.AI
{
    /// <summary>
    /// NPC 대화 시 카메라 클로즈업 및 복귀를 제어하는 서비스 인터페이스.
    /// 순환 참조를 방지하기 위해 AI 어셈블리에 인터페이스를 둡니다.
    /// </summary>
    public interface IDialogueCameraService
    {
        void FocusOnNPC(Transform npcTransform, Transform playerTransform = null);
        void ReleaseFocus();
    }
}
