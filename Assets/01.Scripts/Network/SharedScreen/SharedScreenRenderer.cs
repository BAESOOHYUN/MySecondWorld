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

        public static Texture2D Render(PDFDocument cachedDoc, int pageIndex, int resolution = 1024)
        {
            return RenderPDF(cachedDoc, pageIndex, resolution);
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
            using (PDFDocument doc = new PDFDocument(pdfBytes))
            {
                PDFPage page = doc.GetPage(pageIndex);
                Vector2 pageSize = page.GetPageSize(1f);

                int texW, texH;
                if (pageSize.x >= pageSize.y)
                {
                    texW = 1024;
                    texH = Mathf.RoundToInt(1024 * (pageSize.y / pageSize.x));
                }
                else
                {
                    texH = 1024;
                    texW = Mathf.RoundToInt(1024 * (pageSize.x / pageSize.y));
                }

                using (PDFRenderer renderer = new PDFRenderer())
                {
                    Texture2D tex = renderer.RenderPageToTexture(page, texW, texH);
                    page.Dispose();
                    return tex;
                }
            }
        }

        private static Texture2D RenderPDF(PDFDocument doc, int pageIndex, int resolution = 1024)
        {
            PDFPage page = doc.GetPage(pageIndex);
            Vector2 pageSize = page.GetPageSize(1f);

            // 페이지 비율에 맞춘 텍스처 해상도 계산
            int texW, texH;
            if (pageSize.x >= pageSize.y)
            {
                texW = resolution;
                texH = Mathf.RoundToInt(resolution * (pageSize.y / pageSize.x));
            }
            else
            {
                texH = resolution;
                texW = Mathf.RoundToInt(resolution * (pageSize.x / pageSize.y));
            }

            using (PDFRenderer renderer = new PDFRenderer())
            {
                Texture2D tex = renderer.RenderPageToTexture(page, texW, texH);
                page.Dispose();
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