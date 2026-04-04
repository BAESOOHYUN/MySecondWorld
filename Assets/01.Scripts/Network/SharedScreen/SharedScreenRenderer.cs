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

        // 내부 헬퍼
        private static Texture2D RenderImage(byte[] imageBytes)
        {
            Texture2D tex = new Texture2D(2, 2);  // 임시 크기, LoadImage가 덮어씀
            tex.LoadImage(imageBytes);            // byte[] → 텍스처 변환
            return tex;
        }

        private static Texture2D RenderPDF(byte[] pdfBytes, int pageIndex)
        { 
            PDFDocument doc = new PDFDocument(pdfBytes); // byte[]로 PDF 문서 열기
            PDFPage page = doc.GetPage(pageIndex); // 해당 페이지 가져오기
            PDFRenderer renderer = new PDFRenderer(); 
            Texture2D tex = renderer.RenderPageToTexture(page, 1024, 1024); // 페이지를 Texture2D로 렌더

            // 리소스 정리. 네이티브 리소스라서 Dispose() 필수
            page.Dispose();
            doc.Dispose();

            return tex;
        }
    }
}