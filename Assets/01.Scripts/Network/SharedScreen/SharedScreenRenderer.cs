using UnityEngine;
using Paroxe.PdfRenderer;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 완성된 byte[]를 Texture2D로 변환
    /// </summary>
    public static class SharedScreenRenderer
    {
        // 외부에서 호출하는 메인 메서드
        public static Texture2D Render(byte[] fileBytes, FileType fileType, int pageIndex = 0)
        { 
            switch (fileType) 
            {   
                case FileType.Image:
                        return RenderImage(fileBytes);
                case FileType.PDF:
                        return RenderPDF(fileBytes, pageIndex);
                default:
                    return null;
            }   
        }

        public static Texture2D Render(PDFDocument cachedDoc, int pageIndex)
        {
            return RenderPDF(cachedDoc, pageIndex);
        }

        // 내부 헬퍼
        private static Texture2D RenderImage(byte[] imageBytes)
        {
            Texture2D tex = new Texture2D(2, 2);  // 임시 크기, LoadImage가 덮어씀
            tex.LoadImage(imageBytes);            // byte[] → 텍스처 변환
            return tex;
        }

        private static Texture2D RenderPDF(byte[] pdfBytes, int pageIndex)
        {
            using (PDFDocument doc = new PDFDocument(pdfBytes)) // byte[]로 PDF 문서 열기
            {
                PDFPage page = doc.GetPage(pageIndex); // 해당 페이지 가져오기
                using (PDFRenderer renderer = new PDFRenderer()) // IDisposable이면 통과, 아니면 컴파일 에러
                {
                    Texture2D tex = renderer.RenderPageToTexture(page, 1024, 1024); // 페이지를 Texture2D로 렌더
                    page.Dispose(); // 리소스 정리. 네이티브 리소스라서 Dispose() 필수.
                                    // doc.Dispose()는 using으로 감싸져서 지워도 됨.
                    return tex;
                }
            }
        }

        private static Texture2D RenderPDF(PDFDocument doc, int pageIndex)
        {
            PDFPage page = doc.GetPage(pageIndex);
            using (PDFRenderer renderer = new PDFRenderer())
            {
                Texture2D tex = renderer.RenderPageToTexture(page, 1024, 1024);
                page.Dispose();
                // doc.Dispose() 호출하지 않음 — 소유권은 Controller
                return tex;
            }
        }

        // 전체 페이지 수 조회
        public static int GetPageCount(byte[] fileByte, FileType fileType)
        {
            switch(fileType) 
            {
                case FileType.Image:
                    return 1;
                case FileType.PDF:
                    {
                        PDFDocument doc = new PDFDocument(fileByte);
                        int count = doc.GetPageCount();
                        doc.Dispose();
                        return count;
                    }
                default:
                    return 0;

            }
        }

        public static int GetPageCount(PDFDocument doc)
        {
            return doc.GetPageCount();
        }

        // Quad 비율 조절용 페이지 사이즈
        public static Vector2 GetPageSize(byte[] fileByte, FileType fileType, int pageIndex)
        {
            switch (fileType)
            {
                case FileType.Image:
                    {
                        Texture2D tex = new Texture2D(2, 2);
                        tex.LoadImage(fileByte);
                        Vector2 imageSize = new Vector2(tex.width, tex.height);

                        #if UNITY_EDITOR
                            Object.DestroyImmediate(tex);
                        #else
                            Object.Destroy(tex); 
                        #endif
                            tex = null;
                        
                        return imageSize;
                    }
                case FileType.PDF:
                    {
                        PDFDocument doc = new PDFDocument(fileByte);
                        PDFPage page = doc.GetPage(pageIndex);
                        Vector2 pageSize = page.GetPageSize(1f);
                        page.Dispose();
                        doc.Dispose();
                        return pageSize;
                    }
                default:
                    return Vector2.zero;
            }
        }

        public static Vector2 GetPageSize(PDFDocument doc, int pageIndex)
        {
            PDFPage page = doc.GetPage(pageIndex);
            Vector2 pageSize = page.GetPageSize(1f);
            page.Dispose();
            return pageSize;  // doc.Dispose() 호출하지 않음
        }
    }
}