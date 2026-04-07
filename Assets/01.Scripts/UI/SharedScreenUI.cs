using System.Collections;
using System.IO;
using Paroxe.PdfRenderer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.Network
{
    /// <summary>
    /// Canvas UI 패널의 컨트롤러.
    /// PDF → PDFViewer 프리팹, 이미지 → RawImage 폴백.
    /// </summary>
    public class SharedScreenUI : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] private SharedScreenController _controller;

        [Header("FileLoad Panel")]
        [SerializeField] private GameObject _fileLoadPanel;
        [SerializeField] private TMP_InputField _urlInputField;
        [SerializeField] private TMP_Text _localFilePathText;
        [SerializeField] private Button _localFileSearchButton;
        [SerializeField] private Button _clearLocalFileButton;
        [SerializeField] private Button _fileLoadButton;
        [SerializeField] private Toggle _allowDownloadToggle;
        [SerializeField] private Button _closeFileLoadPannelButton;

        [Header("Open View Button")]
        [SerializeField] private Button _openViewButton;

        [Header("View Panel")]
        [SerializeField] private GameObject _viewPanel;

        [Header("PDFViewer")]
        [SerializeField] private PDFViewer _pdfViewer;
        [SerializeField] private GameObject _pdfViewerObject;

        [Header("Image Fallback")]
        [SerializeField] private GameObject _imageViewPanel;
        [SerializeField] private RawImage _imageView;
        [SerializeField] private AspectRatioFitter _imageAspectFitter;

        [Header("Overlay Buttons")]
        [SerializeField] private Button _endPresentButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _closeViewButton;       // PDF/이미지 공통 닫기
        [SerializeField] private Toggle _followPresenterToggle; // 관객용: 발표자 따라가기

        private bool _isPresenter;
        private string _localFilePath;
        private bool _isSyncingFromNetwork; // 무한루프 방지 플래그
        private ScrollRect _pdfScrollRect;  // PDFViewer 내부 ScrollRect 캐시

        private void Awake()
        {
            // FileLoad 패널
            _urlInputField.onValueChanged.AddListener(OnUrlInputChanged);
            _localFileSearchButton.onClick.AddListener(OnLocalFileSelect);
            _clearLocalFileButton.onClick.AddListener(OnClearLocalFile);
            _fileLoadButton.onClick.AddListener(OnLoad);
            _closeFileLoadPannelButton.onClick.AddListener(OnCloseFileLoad);
            _openViewButton.onClick.AddListener(OnOpenView);

            // 오버레이 버튼
            _endPresentButton.onClick.AddListener(OnEndPresent);
            _saveButton.onClick.AddListener(OnSave);
            _closeViewButton.onClick.AddListener(OnCloseView);

            // PDFViewer 이벤트
            if (_pdfViewer != null)
            {
                _pdfViewer.OnCurrentPageChanged += OnPdfViewerPageChanged;
                _pdfViewer.OnZoomChanged += OnPdfViewerZoomChanged;
                _pdfViewer.OnDocumentLoaded += OnPdfDocumentLoaded;
            }
        }

        private void OnDisable()
        {
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        }

        private void OnDestroy()
        {
            // FileLoad 패널
            _urlInputField.onValueChanged.RemoveListener(OnUrlInputChanged);
            _localFileSearchButton.onClick.RemoveListener(OnLocalFileSelect);
            _clearLocalFileButton.onClick.RemoveListener(OnClearLocalFile);
            _fileLoadButton.onClick.RemoveListener(OnLoad);
            _closeFileLoadPannelButton.onClick.RemoveListener(OnCloseFileLoad);
            _openViewButton.onClick.RemoveListener(OnOpenView);

            // 오버레이 버튼
            _endPresentButton.onClick.RemoveListener(OnEndPresent);
            _saveButton.onClick.RemoveListener(OnSave);
            _closeViewButton.onClick.RemoveListener(OnCloseView);

            // PDFViewer 이벤트
            if (_pdfViewer != null)
            {
                _pdfViewer.OnCurrentPageChanged -= OnPdfViewerPageChanged;
                _pdfViewer.OnZoomChanged -= OnPdfViewerZoomChanged;
                _pdfViewer.OnDocumentLoaded -= OnPdfDocumentLoaded;
            }

            // ScrollRect 이벤트
            if (_pdfScrollRect != null)
                _pdfScrollRect.onValueChanged.RemoveListener(OnPdfScrollChanged);

            // Controller 이벤트
            if (_controller != null)
            {
                _controller.onPresenterChanged -= OnPresenterChanged;
                _controller.onAllowDownloadChanged -= OnAllowDownloadChanged;
                _controller.onFileReceived -= OnFileReceived;
                _controller.onPageChanged -= OnPageChanged;
                _controller.onZoomChanged -= OnZoomChanged;
            }
        }

        public void Init(SharedScreenController controller)
        {
            // 이전 구독 해제
            if (_controller != null)
            {
                _controller.onPresenterChanged -= OnPresenterChanged;
                _controller.onAllowDownloadChanged -= OnAllowDownloadChanged;
                _controller.onFileReceived -= OnFileReceived;
                _controller.onPageChanged -= OnPageChanged;
                _controller.onZoomChanged -= OnZoomChanged;
            }

            _controller = controller;
            _controller.onPresenterChanged += OnPresenterChanged;
            _controller.onAllowDownloadChanged += OnAllowDownloadChanged;
            _controller.onFileReceived += OnFileReceived;
            _controller.onPageChanged += OnPageChanged;
            _controller.onZoomChanged += OnZoomChanged;

            uint myNetId = Mirror.NetworkClient.localPlayer?.netId ?? 0;
            _isPresenter = _controller.IsPresenter(myNetId);

            if (!_controller.HasPresenter)
            {
                ShowFileLoadPanel();
            }
            else
            {
                ShowOpenViewButton();
            }
        }

        // ──────────────────── 패널 전환 ────────────────────
        private void ShowFileLoadPanel()
        {
            _fileLoadPanel.SetActive(true);
            _openViewButton.gameObject.SetActive(false);
            _viewPanel.SetActive(false);
        }

        private void ShowOpenViewButton()
        {
            _fileLoadPanel.SetActive(false);
            _openViewButton.gameObject.SetActive(true);
            _viewPanel.SetActive(false);
        }

        private void ShowViewPanel()
        {
            _fileLoadPanel.SetActive(false);
            _openViewButton.gameObject.SetActive(false);
            _viewPanel.SetActive(true);

            _endPresentButton.gameObject.SetActive(_isPresenter);
            _saveButton.gameObject.SetActive(!_isPresenter && _controller.AllowDownload);
            if (_followPresenterToggle != null)
                _followPresenterToggle.gameObject.SetActive(!_isPresenter);
        }

        // ──────────────────── PDFViewer 이벤트 — 발표자 동기화 ────────────────────
        private void OnPdfViewerPageChanged(PDFViewer sender, int oldIdx, int newIdx)
        {
            if (_isSyncingFromNetwork) return;
            if (_isPresenter)
                _controller.CmdChangePage(newIdx);
        }

        private void OnPdfViewerZoomChanged(PDFViewer sender, float oldZoom, float newZoom)
        {
            if (_isSyncingFromNetwork) return;
            if (_isPresenter)
            {
                _controller.CmdChangeZoom(newZoom);
                // 줌 변경 시 viewport rect도 갱신 (ScrollRect onValueChanged가 발화 안 될 수 있음)
                StartCoroutine(SendViewportRectNextFrame());
            }
        }

        private IEnumerator SendViewportRectNextFrame()
        {
            yield return null; // 줌 후 레이아웃 리빌드 대기
            if (_isPresenter && _controller != null)
            {
                Vector4 viewRect = ComputePageViewportRect();
                _controller.CmdChangeViewportRect(viewRect);
            }
        }

        private void OnPdfDocumentLoaded(PDFViewer sender, PDFDocument document)
        {
            if (_controller == null) return;

            // PDFViewer 내부 ScrollRect 캐시 + 발표자면 스크롤 변경 감지 등록
            if (_pdfScrollRect == null && _pdfViewer.m_Internal != null)
            {
                _pdfScrollRect = _pdfViewer.m_Internal.ScrollRect;
                if (_pdfScrollRect != null)
                    _pdfScrollRect.onValueChanged.AddListener(OnPdfScrollChanged);
            }

            // 비동기 로드 완료 후 현재 발표자 상태로 동기화
            _isSyncingFromNetwork = true;
            if (_controller.CurrentPage != 0)
                _pdfViewer.GoToPage(_controller.CurrentPage);
            if (_controller.ZoomLevel != 1f)
                _pdfViewer.ZoomFactor = _controller.ZoomLevel;
            _isSyncingFromNetwork = false;
        }

        private void OnPdfScrollChanged(Vector2 normalizedPosition)
        {
            if (_isSyncingFromNetwork) return;
            if (!_isPresenter || _controller == null) return;

            Vector4 viewRect = ComputePageViewportRect();
            _controller.CmdChangeViewportRect(viewRect);
        }

        /// <summary>
        /// PDFViewer의 Viewport가 현재 페이지의 어느 영역을 보고 있는지 계산.
        /// 반환: Vector4(x, y, width, height) — 모두 페이지 기준 0~1 normalized.
        /// (0,0)=페이지 좌상단 (UI 좌표계, UV 변환은 Controller에서)
        /// </summary>
        private Vector4 ComputePageViewportRect()
        {
            if (_pdfViewer == null || _pdfViewer.m_Internal == null)
                return new Vector4(0, 0, 1, 1);

            int pageIndex = _pdfViewer.CurrentPageIndex;
            RectTransform pageContainer = _pdfViewer.m_Internal.PageContainer;
            RectTransform viewport = _pdfViewer.m_Internal.Viewport;

            if (pageContainer == null || viewport == null || pageIndex < 0
                || pageIndex >= pageContainer.childCount)
                return new Vector4(0, 0, 1, 1);

            RectTransform pageRT = (RectTransform)pageContainer.GetChild(pageIndex);

            // viewport edge = anchoredPosition 기준점에서 pivot 오프셋 적용
            float viewLeftX = -pageContainer.anchoredPosition.x - viewport.rect.width * 0.5f;
            float viewTopY = pageContainer.anchoredPosition.y;

            // 페이지의 좌상단 — PageContainer 로컬 좌표
            float pageTopY = -pageRT.anchoredPosition.y - pageRT.sizeDelta.y * 0.5f;
            float pageLeftX = pageRT.anchoredPosition.x - pageRT.sizeDelta.x * 0.5f;

            // Viewport 좌상단의 페이지 내 상대 위치
            float nx = (viewLeftX - pageLeftX) / pageRT.sizeDelta.x;
            float ny = (viewTopY - pageTopY) / pageRT.sizeDelta.y;

            // Viewport가 보는 페이지 비율
            float nw = viewport.rect.width / pageRT.sizeDelta.x;
            float nh = viewport.rect.height / pageRT.sizeDelta.y;

            // clamp — 페이지 밖은 잘라냄
            nx = Mathf.Clamp(nx, 0f, Mathf.Max(0f, 1f - nw));
            ny = Mathf.Clamp(ny, 0f, Mathf.Max(0f, 1f - nh));
            nw = Mathf.Clamp01(nw);
            nh = Mathf.Clamp01(nh);

           /* Debug.Log($"[ViewRect] nx={nx:F3} ny={ny:F3} nw={nw:F3} nh={nh:F3} " +
                $"anchoredPos=({pageContainer.anchoredPosition.x:F1},{pageContainer.anchoredPosition.y:F1}) " +
                $"containerPivot=({pageContainer.pivot.x:F2},{pageContainer.pivot.y:F2}) " +
                $"viewLT=({viewLeftX:F1},{viewTopY:F1}) " +
                $"pageLT=({pageLeftX:F1},{pageTopY:F1}) " +
                $"pageAnchor=({pageRT.anchoredPosition.x:F1},{pageRT.anchoredPosition.y:F1}) " +
                $"pagePivot=({pageRT.pivot.x:F2},{pageRT.pivot.y:F2})");*/

            return new Vector4(nx, ny, nw, nh);
        }

        // ──────────────────── 이벤트 핸들러 — 파일 로드 ────────────────────
        private void OnLocalFileSelect()
        {
            // TODO: OpenFileDialog로 교체
            _localFilePath = @"C:\Users\BaeSoohyun\Documents\버킷리스트.pdf";
            _localFilePathText.text = _localFilePath;
            _urlInputField.text = "";
        }

        private void OnClearLocalFile()
        {
            _localFilePath = null;
            _localFilePathText.text = "";
        }

        private void OnUrlInputChanged(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                _localFilePath = null;
                _localFilePathText.text = "";
            }
        }

        private void OnLoad()
        {
            if (_controller.IsReceiving) return;

            bool allowDownload = _allowDownloadToggle.isOn;

            if (!string.IsNullOrEmpty(_localFilePath))
            {
                FileType type = DetectFileType(_localFilePath);
                FileChunkSender.SendFileFromLocalstore(_controller, _localFilePath, type, allowDownload);
                _isPresenter = true;
                return;
            }

            string url = _urlInputField.text;
            if (!string.IsNullOrEmpty(url))
            {
                FileType type = DetectFileType(url);
                StartCoroutine(FileChunkSender.SendFileFromUrl(_controller, url, type, allowDownload));
            }
        }

        // ──────────────────── 이벤트 핸들러 — 뷰어 열기 ────────────────────
        private void OnOpenView()
        {
            byte[] buffer = _controller.FileBuffer;
            if (buffer == null) return;

            if (_controller.CurrentFileType == FileType.PDF)
            {
                _pdfViewerObject.SetActive(true);
                _imageViewPanel?.SetActive(false);

                if (_pdfViewer != null && !_pdfViewer.IsLoaded)
                {
                    //_pdfViewer.ShowBookmarksViewer = false;
                    //_pdfViewer.ShowThumbnailsViewer = false;
                    _pdfViewer.LoadDocumentFromBuffer(buffer);
                }
                // GoToPage는 OnPdfDocumentLoaded 콜백에서 처리
            }
            else
            {
                _pdfViewerObject?.SetActive(false);
                _imageViewPanel?.SetActive(true);
                RenderImageLocal();
            }

            ShowViewPanel();
        }

        // ──────────────────── 이벤트 핸들러 — 닫기/종료/저장 ────────────────────
        private void OnCloseFileLoad()
        {
            _fileLoadPanel.SetActive(false);
        }

        private void OnCloseView()
        {
            ShowOpenViewButton();
        }

        private void OnEndPresent()
        {
            _controller.CmdReleasePresenter();
            _isPresenter = false;

            if (_pdfViewer != null && _pdfViewer.IsLoaded)
                _pdfViewer.CloseDocument();

            ShowFileLoadPanel();
        }

        private void OnSave()
        {
            byte[] buffer = _controller.FileBuffer;
            if (buffer == null) return;

            string fileName = _controller.CurrentFileName;
            if (string.IsNullOrEmpty(fileName))
                fileName = "download_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string savePath = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllBytes(savePath, buffer);
            Debug.Log($"[SharedScreenUI] 파일 저장 완료: {savePath}");
        }

        // ──────────────────── Controller 이벤트 콜백 ────────────────────
        private void OnPresenterChanged(uint newPresenterId)
        {
            uint myNetId = Mirror.NetworkClient.localPlayer?.netId ?? 0;
            _isPresenter = (newPresenterId == myNetId);

            if (newPresenterId == 0)
            {
                if (_pdfViewer != null && _pdfViewer.IsLoaded)
                    _pdfViewer.CloseDocument();
                ShowFileLoadPanel();
            }
        }

        private void OnAllowDownloadChanged(bool allowDownload)
        {
            if (!_isPresenter)
                _saveButton.gameObject.SetActive(allowDownload);
        }

        private void OnFileReceived()
        {
            uint myNetId = Mirror.NetworkClient.localPlayer?.netId ?? 0;
            _isPresenter = _controller.IsPresenter(myNetId);

            // 발표자: 즉시 ViewPanel + PDFViewer 열기
            // 관객: OpenViewButton만 활성화 (관객이 원할 때 직접 열도록)
            if (_isPresenter)
            {
                if (_controller.CurrentFileType == FileType.PDF)
                {
                    if (_controller.FileBuffer == null || _controller.FileBuffer.Length == 0)
                    {
                        Debug.LogError($"SharedScreenUI OnFileReceived  FileBuffer가 비어있습니다.");
                        return;
                    }

                    _pdfViewerObject.SetActive(true);
                    _imageViewPanel?.SetActive(false);
                    StartCoroutine(LoadPdfNextFrame());
                }
                else
                {
                    _pdfViewerObject?.SetActive(false);
                    _imageViewPanel?.SetActive(true);
                    RenderImageLocal();
                }

                ShowViewPanel();
            }
            else
            {
                // 관객: 파일 수신 완료 알림 → OpenViewButton만 표시
                ShowOpenViewButton();
            }
        }

        private IEnumerator LoadPdfNextFrame()
        {
            yield return null; // PDFViewer 내부 초기화 대기 (Awake/OnEnable/Start)
            if (_pdfViewer != null && _controller.FileBuffer != null)
            {
                //_pdfViewer.ShowBookmarksViewer = false;
                //_pdfViewer.ShowThumbnailsViewer = false;
                _pdfViewer.LoadDocumentFromBuffer(_controller.FileBuffer);
            }
        }

        private void OnPageChanged(int page)
        {
            // Quad 동기화는 Controller.RenderToScreen()에서 항상 처리됨
            // PDFViewer UI 동기화는 "발표자 따라가기" 토글 ON인 관객만
            if (_isPresenter) return;
            if (_followPresenterToggle != null && _followPresenterToggle.isOn
                && _controller.CurrentFileType == FileType.PDF
                && _pdfViewer != null && _pdfViewer.IsLoaded)
            {
                _isSyncingFromNetwork = true;
                _pdfViewer.GoToPage(page);
                _isSyncingFromNetwork = false;
            }
        }

        private void OnZoomChanged(float zoom)
        {
            if (_isPresenter) return;
            if (_followPresenterToggle != null && _followPresenterToggle.isOn
                && _controller.CurrentFileType == FileType.PDF
                && _pdfViewer != null && _pdfViewer.IsLoaded)
            {
                _isSyncingFromNetwork = true;
                _pdfViewer.ZoomFactor = zoom;
                _isSyncingFromNetwork = false;
            }
        }

        // ──────────────────── 이미지 렌더 + 유틸 ────────────────────
        private void RenderImageLocal()
        {
            byte[] buffer = _controller.FileBuffer;
            if (buffer == null || _imageView == null) return;

            Texture2D tex = SharedScreenRenderer.Render(buffer, FileType.Image, 0);
            if (tex != null)
            {
                if (_imageView.texture != null)
                    Destroy(_imageView.texture);
                _imageView.texture = tex;
                if (_imageAspectFitter != null)
                    _imageAspectFitter.aspectRatio = (float)tex.width / tex.height;
            }
        }

        private static FileType DetectFileType(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".jpg": case ".jpeg": case ".png": case ".gif": case ".bmp": case ".webp":
                    return FileType.Image;
                default:
                    return FileType.PDF;
            }
        }
    }
}
