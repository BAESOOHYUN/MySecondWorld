using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Paroxe.PdfRenderer;
using SimpleFileBrowser;
using R3;
using Cysharp.Threading.Tasks;

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
        [SerializeField] private Button _closeFileLoadPannelButton;
        [SerializeField] private Button _localFileSearchButton;
        [SerializeField] private Button _clearLocalFileUrlButton;
        [SerializeField] private Button _fileLoadButton;
        [SerializeField] private Toggle _allowDownloadToggle;

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
        private CancellationTokenSource _pageZoomCts;  // 페이지/줌 동기화 코루틴
        private CancellationTokenSource _scrollCts;    // 스크롤 위치 동기화 코루틴 (페이지 코루틴과 독립)

        private readonly CompositeDisposable _controllerDisposables = new();

        private void Awake()
        {
            // FileLoad 패널
            _urlInputField.onValueChanged.AddListener(OnUrlInputChanged);
            _localFileSearchButton.onClick.AddListener(OnLocalFileSelect);
            _clearLocalFileUrlButton.onClick.AddListener(OnClearLocalFile);
            _fileLoadButton.onClick.AddListener(OnLoad);
            _closeFileLoadPannelButton.onClick.AddListener(OnCloseFileLoad);
            _openViewButton.onClick.AddListener(OnOpenView);

            // 오버레이 버튼
            _endPresentButton.onClick.AddListener(OnEndPresent);
            _saveButton.onClick.AddListener(OnSave);
            _closeViewButton.onClick.AddListener(OnCloseView);
            _followPresenterToggle.onValueChanged.AddListener(OnFollowPresenterToggleChanged);
            _followPresenterToggle.isOn = true;

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
            // CTS 정리
            _pageZoomCts?.Cancel();
            _pageZoomCts?.Dispose();
            _scrollCts?.Cancel();
            _scrollCts?.Dispose();

            // FileLoad 패널
            _urlInputField.onValueChanged.RemoveListener(OnUrlInputChanged);
            _localFileSearchButton.onClick.RemoveListener(OnLocalFileSelect);
            _clearLocalFileUrlButton.onClick.RemoveListener(OnClearLocalFile);
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

            _controllerDisposables.Dispose(); //구독들을 해제하고 CompositeDisposable 자체도 폐기. 이후에 .AddTo() 해도 무시
        }

        public void Init(SharedScreenController controller)
        {
            _controllerDisposables.Clear();  // 이전 controller 구독 해제
                                             // 구독들을 Dispose(해제)하고 리스트를 비움
                                             // CompositeDisposable 자체는 살아있어서 이후에 새 구독을 .AddTo() 할 수 있다
            _controller = controller;
            _controller.onPresenterChanged.Subscribe(OnPresenterChanged).AddTo(_controllerDisposables);
            _controller.onAllowDownloadChanged.Subscribe(OnAllowDownloadChanged).AddTo(_controllerDisposables);
            _controller.onFileReceived.Subscribe(_ => OnFileReceived()).AddTo(_controllerDisposables);
            _controller.onPageChanged.Subscribe(OnPageChanged).AddTo(_controllerDisposables);
            _controller.onZoomChanged.Subscribe(OnZoomChanged).AddTo(_controllerDisposables);
            _controller.onViewportRectChanged.Subscribe(OnViewportRectChanged).AddTo(_controllerDisposables);

            uint myNetId = Mirror.NetworkClient.localPlayer?.netId ?? 0;
            _isPresenter = _controller.IsPresenter(myNetId);

            if (!_controller.HasPresenter)
                ShowFileLoadPanel();
            else
                ShowOpenViewButton();
        }

        // 패널 전환
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
            {
                _followPresenterToggle.gameObject.SetActive(!_isPresenter);
            }
        }

        // PDFViewer 이벤트 — 발표자 동기화
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
                SendViewportRectNextFrame().Forget();
            }
        }

        private async UniTaskVoid SendViewportRectNextFrame()
        {
            await UniTask.NextFrame(); // 줌 후 레이아웃 리빌드 대기
            if (_isPresenter && _controller != null)
            {
                Vector4 viewRect = ComputePageViewportRect();
                _controller.CmdChangeViewportRect(viewRect);
            }
        }

        private void OnPdfDocumentLoaded(PDFViewer sender, PDFDocument document)
        {
            if (_controller == null) return;

            // PDFViewer 내부 ScrollRect 캐시 + 스크롤 변경 감지 등록
            if (_pdfScrollRect == null && _pdfViewer.m_Internal != null)
            {
                _pdfScrollRect = _pdfViewer.m_Internal.ScrollRect;
                if (_pdfScrollRect != null)
                    _pdfScrollRect.onValueChanged.AddListener(OnPdfScrollChanged);
            }

            // 비동기 로드 완료 후 발표자 상태로 동기화 (코루틴으로 순서 보장)
            StartPageZoomTask(ct => SyncFromPresenterTask(
                _controller.CurrentPage,
                _controller.ZoomLevel,
                _controller.ViewportRect,
                ct
            ));
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

            // X축만 클램프 (페이지 좌우 밖은 의미 없음)
            // Y축은 클램프하지 않음 — 페이지 경계 스크롤 시 ny > 1 또는 ny < 0 가능
            // 관객 측에서 anchoredPosition으로 역산하므로 경계면도 정확히 재현됨
            nx = Mathf.Clamp(nx, 0f, Mathf.Max(0f, 1f - nw));
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

        // 이벤트 핸들러 — 파일 로드
        private void OnLocalFileSelect()
        {

#if UNITY_STANDALONE || UNITY_EDITOR
            OpenWithSimpleFileBrowser();
#elif UNITY_ANDROID || UNITY_IOS
            OpenWithNativeFilePicker();
#endif
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
                if (type == FileType.Unknown)
                {
                    Debug.LogWarning("지원하지 않는 파일 형식입니다.");
                    return;
                }
                FileChunkSender.SendFileFromLocalstore(_controller, _localFilePath, type, allowDownload);
                return;
            }

            string url = _urlInputField.text;
            if (!string.IsNullOrEmpty(url))
            {
                FileType type = DetectFileType(url);
                if (type == FileType.Unknown)
                {
                    Debug.LogWarning("지원하지 않는 파일 형식입니다.");
                    return;
                }
                StartCoroutine(FileChunkSender.SendFileFromUrl(_controller, url, type, allowDownload));
            }
        }

        // 이벤트 핸들러 — 뷰어 열기
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
                    // 최초 로드만 — 1프레임 대기 후 로드 (BookMark/Thumbnail 초기화 대기)
                    LoadPdfNextFrame().Forget();
                }
                // 이미 로드된 상태면 활성화만으로 충분 (Document 유지)
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

        // 이벤트 핸들러 — 닫기/종료/저장
        private void OnCloseFileLoad()
        {
            _fileLoadPanel.SetActive(false);
        }

        private void OnCloseView()
        {
            ShowOpenViewButton();
            // CloseDocument 하지 않음 — Document 유지, 다시 열 때 재로드 불필요
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
            {
                string ext = _controller.CurrentFileType == FileType.PDF ? ".pdf" : ".png";
                fileName = $"download_{System.DateTime.Now:yyyyMMdd_HHmmss}{ext}";
            }
#if UNITY_STANDALONE || UNITY_EDITOR
            // PC: 저장 위치 직접 선택
            FileBrowser.ShowSaveDialog(
                (paths) =>
                {
                    File.WriteAllBytes(paths[0], buffer);
                    Debug.Log($"파일 저장 완료: {paths[0]}");
                },
                () => { Debug.Log("Save cancelled"); },
                FileBrowser.PickMode.Files,
                false, null, fileName, "Save", "Save");
#elif UNITY_ANDROID || UNITY_IOS
              // 모바일: 샌드박스에 임시 저장 후 OS 공유 시트 호출
              // iOS → "파일에 저장" / AirDrop 등 선택 가능
              // Android → Downloads / Drive 등 선택 가능
              string tempPath = Path.Combine(Application.temporaryCachePath, fileName);
              File.WriteAllBytes(tempPath, buffer);
              NativeFilePicker.ExportFile(tempPath, (success) => {
                  Debug.Log(success ? "파일 내보내기 완료" : "파일 내보내기 취소");
      });
#endif
        }

        // Controller 이벤트 콜백
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
                    LoadPdfNextFrame().Forget();
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

        private async UniTaskVoid LoadPdfNextFrame()
        {
            await UniTask.NextFrame(); // PDFViewer 내부 초기화 대기 (Awake/OnEnable/Start)
            if (_pdfViewer != null && _controller.FileBuffer != null)
            {
                // 시작시 LeftPanel 닫는 용도. 나중의 기획 변경을 위해 남겨둠.
                //_pdfViewer.ShowBookmarksViewer = false;
                //_pdfViewer.ShowThumbnailsViewer = false;
                _pdfViewer.LoadDocumentFromBuffer(_controller.FileBuffer);
            }
        }

        private void OnPageChanged(int page)
        {
            // Quad 동기화는 Controller.RenderToScreen()에서 항상 처리됨
            // 스크롤 경계 넘기에 의한 페이지 변경은 OnViewportRectChanged에서 통합 처리
            // 여기서는 prev/next 버튼에 의한 페이지 이동 시 줌만 동기화
            if (_isPresenter) return;
            if (!IsFollowingPresenter()) return;

            StartPageZoomTask(ct => SyncPageZoomTask(page, _controller.ZoomLevel, ct));
        }

        private void OnZoomChanged(float zoom)
        {
            if (_isPresenter) return;
            if (!IsFollowingPresenter()) return;

            StartPageZoomTask(ct => SyncPageZoomTask(_controller.CurrentPage, zoom, ct));
        }

        private void OnViewportRectChanged(Vector4 rect)
        {
            if (_isPresenter) return;
            if (!IsFollowingPresenter()) return;

            // 스크롤만 적용 — _pageZoomCoroutine과 독립 슬롯 사용
            StartScrollTask(ct => SyncScrollTask(_controller.CurrentPage, rect, ct));
        }

        // 토글 on 시 발표자의 현재 상태를 가져와서 즉시 동기화
        private void OnFollowPresenterToggleChanged(bool isOn)
        {
            if (_isPresenter || !isOn) return;
            if (!IsFollowingPresenter()) return;

            // 토글 ON: 전체 상태를 순서대로 맞춤 (페이지→줌→스크롤)
            StartPageZoomTask(ct => SyncFromPresenterTask(
                _controller.CurrentPage,
                _controller.ZoomLevel,
                _controller.ViewportRect,
                ct
            ));
        }

        private bool IsFollowingPresenter()
        {
            return _followPresenterToggle != null && _followPresenterToggle.isOn
                && _controller != null
                && _controller.CurrentFileType == FileType.PDF
                && _pdfViewer != null && _pdfViewer.IsLoaded;
        }

        // 페이지/줌 코루틴 슬롯 — 스크롤 코루틴을 취소하지 않음
        private void StartPageZoomTask(Func<CancellationToken, UniTask> taskFactory)
        {
            _pageZoomCts?.Cancel();
            _pageZoomCts?.Dispose();
            _pageZoomCts = new CancellationTokenSource();
            taskFactory(_pageZoomCts.Token).Forget();
        }

        // 스크롤 코루틴 슬롯 — 페이지/줌 코루틴을 취소하지 않음
        private void StartScrollTask(Func<CancellationToken, UniTask> taskFactory)
        {
            _scrollCts?.Cancel();
            _scrollCts?.Dispose();
            _scrollCts = new CancellationTokenSource();
            // taskFactory가 UniTask를 반환하는데, 이걸 await하지 않으면 컴파일러가 경고를 냄.
            // Forget()은 그 경고를 없애면서 "의도적으로 await 안 함"을 명시
            taskFactory(_scrollCts.Token).Forget();
        }

        // 페이지 + 줌만 동기화
        private async UniTask SyncPageZoomTask(int page, float zoom, CancellationToken ct)
        {
            _isSyncingFromNetwork = true;

            if (_pdfViewer.CurrentPageIndex != page)
            {
                _pdfViewer.GoToPage(page);
                await UniTask.NextFrame(ct);
                // ct는 취소 신호. 새 작업이 시작되면 StartPageZoomTask에서 이전 CTS를 Cancel()하는데,
                // 이때 ct가 취소 신호를 받고 await 지점에서 즉시 중단
            }

            if (!Mathf.Approximately(_pdfViewer.ZoomFactor, zoom))
            {
                _pdfViewer.ZoomFactor = zoom;
                await UniTask.NextFrame(ct);
            }

            _isSyncingFromNetwork = false;
        }

        // 스크롤 위치 동기화 — GoToPage 없이 anchoredPosition만 직접 조작
        // GoToPage는 내부 레이아웃 리셋 + 페이지 상단 점프를 유발하므로 사용하지 않음.
        // pageContainer는 모든 페이지를 세로로 나열하므로 anchoredPosition만으로 경계면 포함 어디든 이동 가능.
        private async UniTask SyncScrollTask(int page, Vector4 viewportRect, CancellationToken ct)
        {
            _isSyncingFromNetwork = true;

            // ScrollRect 비활성화 → anchoredPosition 직접 조작 → 1프레임 대기 → 재활성화
            if (_pdfScrollRect != null) _pdfScrollRect.enabled = false;
            ApplyScrollFromViewportRect(page, viewportRect);
            await UniTask.NextFrame(ct);

            if (_pdfScrollRect != null) _pdfScrollRect.enabled = true;

            _isSyncingFromNetwork = false;
        }

        /// <summary>
        /// 페이지 → 줌 → 스크롤 순서로 전체 동기화. 토글 ON 시 또는 문서 로드 완료 시 사용.
        /// </summary>
        private async UniTask SyncFromPresenterTask(int page, float zoom, Vector4 viewportRect, CancellationToken ct)
        {
            _isSyncingFromNetwork = true;

            if (_pdfViewer.CurrentPageIndex != page)
            {
                _pdfViewer.GoToPage(page);
                await UniTask.NextFrame(ct);
            }

            if (!Mathf.Approximately(_pdfViewer.ZoomFactor, zoom))
            {
                _pdfViewer.ZoomFactor = zoom;
                await UniTask.NextFrame(ct);
            }

            // ScrollRect 비활성화 → anchoredPosition 세팅 → 1프레임 대기 → 재활성화
            if (_pdfScrollRect != null) _pdfScrollRect.enabled = false;
            ApplyScrollFromViewportRect(page, viewportRect);
            await UniTask.NextFrame(ct);

            if (_pdfScrollRect != null) _pdfScrollRect.enabled = true;

            _isSyncingFromNetwork = false;
        }

        /// <summary>
        /// 발표자의 (page, viewportRect)를 관객 쪽 pageContainer 실제 레이아웃으로 역산해
        /// pageContainer.anchoredPosition을 직접 조작.
        ///
        /// ScrollRect.normalizedPosition 대신 anchoredPosition을 직접 쓰는 이유:
        /// PDFViewer는 X축 스크롤 가능 범위(containerW - viewportW)가 거의 0이라
        /// normalizedPosition X가 항상 0으로 고정됨. anchoredPosition으로 직접 이동해야 X축도 동작.
        /// </summary>
        /// <summary>
        /// ComputePageViewportRect의 정확한 역함수.
        /// Compute: ny = (containerY - pageTopY) / pageH, pageTopY = -anchoredY - h*0.5
        /// Compute: nx = (viewLeftX - pageLeftX) / pageW, viewLeftX = -containerX - viewportW*0.5
        /// </summary>
        private void ApplyScrollFromViewportRect(int page, Vector4 viewportRect)
        {
            if (_pdfScrollRect == null || _pdfViewer.m_Internal == null) return;

            RectTransform pageContainer = _pdfViewer.m_Internal.PageContainer;
            RectTransform viewport = _pdfViewer.m_Internal.Viewport;

            if (pageContainer == null || viewport == null
                || page < 0 || page >= pageContainer.childCount) return;

            RectTransform pageRT = (RectTransform)pageContainer.GetChild(page);

            // Y축 역산: containerY = ny * pageH + pageTopY
            // pageTopY는 ComputePageViewportRect와 동일한 공식 사용
            float pageTopY = -pageRT.anchoredPosition.y - pageRT.sizeDelta.y * 0.5f;
            float targetContainerY = viewportRect.y * pageRT.sizeDelta.y + pageTopY;

            // X축 역산: containerX = -(nx * pageW + pageLeftX + viewportW * 0.5)
            float pageLeftX = pageRT.anchoredPosition.x - pageRT.sizeDelta.x * 0.5f;
            float targetContainerX = -(viewportRect.x * pageRT.sizeDelta.x + pageLeftX + viewport.rect.width * 0.5f);

            pageContainer.anchoredPosition = new Vector2(targetContainerX, targetContainerY);
        }


        // 이미지 렌더 + 유틸
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
            // URL의 쿼리 파라미터 제거
            int queryIdx = path.IndexOf('?');
            if (queryIdx >= 0) path = path.Substring(0, queryIdx);

            string ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".pdf":
                    return FileType.PDF;
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                case ".webp":
                    return FileType.Image;
                default:
                    return FileType.Unknown;  // 알 수 없는 형식은 거부
            }
        }


#if UNITY_STANDALONE || UNITY_EDITOR
        private void OpenWithSimpleFileBrowser()
        {
            FileBrowser.SetFilters(true, new FileBrowser.Filter("PDF Files", ".pdf"), new FileBrowser.Filter("Images", ".jpg", ".jpeg", ".png"));
            FileBrowser.SetDefaultFilter(".pdf");
            FileBrowser.SetExcludedExtensions(".lnk", ".tmp", ".zip", ".rar", ".exe");
            FileBrowser.ShowLoadDialog((paths) =>
            {
                if (paths.Length > 0)
                    _localFilePath = paths[0]; // 여기서 대입 가능
                _localFilePathText.text = _localFilePath; // 콜백 안에서 갱신
                _urlInputField.text = "";
            },
            () => { Debug.Log("Canceled"); },
            FileBrowser.PickMode.Files, false, null, null, "Select File", "Select");
        }
#endif


#if UNITY_ANDROID || UNITY_IOS
        private void OpenWithNativeFilePicker()
        {
            string[] fileTypes = new[]
            {
                NativeFilePicker.ConvertExtensionToFileType("pdf"),
                NativeFilePicker.ConvertExtensionToFileType("jpg"),
                NativeFilePicker.ConvertExtensionToFileType("jpeg"),
                NativeFilePicker.ConvertExtensionToFileType("png"),
            };
            NativeFilePicker.PickFile((path) =>
            {
                if (path != null)
                {
                    _localFilePath = path;
                    _localFilePathText.text = path;
                    _urlInputField.text = "";
                }
            }, fileTypes);
        }
#endif
    }
}
