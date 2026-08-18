using UnityEngine;

namespace MetaMystia.Debugger
{
    /// <summary>
    /// 将 UnityEngine.Sprite 转为 PNG 字节数组，供 WebDebugger 前端预览。
    /// 核心逻辑来自 ExportUtils.TrySaveSprite。
    /// </summary>
    public static class SpritePreview
    {
        public static byte[] SpriteToPng(Sprite sprite)
        {
            Texture2D readableTexture = null;
            Texture2D finalTexture = null;
            try
            {
                var originalTexture = sprite.texture;
                var rect = sprite.textureRect;

                if (!originalTexture.isReadable)
                {
                    var rt = RenderTexture.GetTemporary(
                        originalTexture.width,
                        originalTexture.height,
                        0,
                        RenderTextureFormat.ARGB32,
                        RenderTextureReadWrite.Default);

                    Graphics.Blit(originalTexture, rt);
                    var previous = RenderTexture.active;
                    RenderTexture.active = rt;

                    var tempFullTexture = new Texture2D(originalTexture.width, originalTexture.height, TextureFormat.RGBA32, false);
                    tempFullTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tempFullTexture.Apply();

                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(rt);

                    readableTexture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
                    var pixels = tempFullTexture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
                    readableTexture.SetPixels(pixels);
                    readableTexture.Apply();

                    Object.DestroyImmediate(tempFullTexture);
                }
                else
                {
                    readableTexture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
                    var pixels = originalTexture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
                    readableTexture.SetPixels(pixels);
                    readableTexture.Apply();
                }

                int targetWidth = Mathf.RoundToInt(sprite.rect.width);
                int targetHeight = Mathf.RoundToInt(sprite.rect.height);
                finalTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);

                Color[] transparent = new Color[targetWidth * targetHeight];
                for (int i = 0; i < transparent.Length; i++) transparent[i] = Color.clear;
                finalTexture.SetPixels(transparent);

                Vector2 offset = sprite.textureRectOffset;
                finalTexture.SetPixels(
                    Mathf.RoundToInt(offset.x),
                    Mathf.RoundToInt(offset.y),
                    (int)rect.width,
                    (int)rect.height,
                    readableTexture.GetPixels());
                finalTexture.Apply();

                return ImageConversion.EncodeToPNG(finalTexture);
            }
            finally
            {
                if (readableTexture != null)
                    Object.DestroyImmediate(readableTexture);
                if (finalTexture != null)
                    Object.DestroyImmediate(finalTexture);
            }
        }
    }
}
