using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.Network
{
    /// <summary>
    /// Canvas UI 패널의 컨트롤러. 버튼 눌렀을 때 동작을 연결하는 역할.
    /// </summary>
    public class SharedScreenUI : MonoBehaviour
    {
        [SerializeField] private SharedScreenController _controller;
        [SerializeField] private Button _openFileButton;
        [SerializeField] private Button _prevPageButton;
        [SerializeField] private Button _nextPageButton;

        private void Awake()
        {
            _openFileButton.onClick.AddListener(OpenFileDialog);
            _prevPageButton.onClick.AddListener(PrevPage);
            _nextPageButton.onClick.AddListener(NextPage);
        }
        private void OnDestroy()
        {
            _openFileButton.onClick.RemoveListener(OpenFileDialog);
            _prevPageButton.onClick.RemoveListener(PrevPage);
            _nextPageButton.onClick.RemoveListener(NextPage);
        }

        private void OpenFileDialog()
        {
            // TODO: 파일 다이얼로그로 교체
            string testPath = "C:/test/sample.png";
            FileType type = FileType.Image;

            FileChunkSender.SendFile(_controller, testPath, type);
        }

        private void PrevPage()
        {
            _controller.RequestPrevPage();
        }

        private void NextPage()
        {
            _controller.RequestNextPage();
        }
    }
}