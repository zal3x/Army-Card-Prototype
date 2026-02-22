using UnityEngine;
using System.IO;

[ExecuteInEditMode]
public class PixelBattlefieldGenerator : MonoBehaviour
{
    public int width = 1920;
    public int height = 1080;
    public int landHeight = 700;
    public int seed = 12345;

    [ContextMenu("Generate Battlefield PNG")]
    public void Generate()
    {
        Random.InitState(seed);

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        // --- COLORS (VIBRANT) ---
        Color skyClearTop = new Color(0.35f, 0.65f, 1f);
        Color skyClearBottom = new Color(0.55f, 0.8f, 1f);
        Color cloudColor = new Color(1f, 1f, 1f, 0.9f);

        Color mountainColor = new Color(0.35f, 0.35f, 0.45f);

        Color grass = new Color(0.25f, 0.7f, 0.25f);
        Color field = new Color(0.45f, 0.8f, 0.3f);
        Color dirt = new Color(0.6f, 0.45f, 0.25f);
        Color road = new Color(0.25f, 0.25f, 0.25f);

        Color treeDark = new Color(0.1f, 0.4f, 0.15f);
        Color fence = new Color(0.55f, 0.45f, 0.3f);

        // --- SKY ---
        for (int y = landHeight; y < height; y++)
        {
            float t = (float)(y - landHeight) / (height - landHeight);
            Color sky = Color.Lerp(skyClearBottom, skyClearTop, t);

            for (int x = 0; x < width; x++)
                tex.SetPixel(x, y, sky);
        }

        // --- MOUNTAINS ---
        int mountainBase = landHeight + 80;
        int peakHeight = 140;

        for (int x = 0; x < width; x++)
        {
            float n = Mathf.PerlinNoise((x + seed) * 0.002f, 0);
            int peak = mountainBase + Mathf.FloorToInt(n * peakHeight);

            for (int y = mountainBase; y < peak; y++)
                tex.SetPixel(x, y, mountainColor);
        }

        // --- CLOUDS (SPARSE) ---
        for (int i = 0; i < 40; i++)
        {
            int cx = Random.Range(0, width);
            int cy = Random.Range(landHeight + 200, height - 50);
            int r = Random.Range(30, 70);

            for (int x = -r; x <= r; x++)
            for (int y = -r / 2; y <= r / 2; y++)
            {
                int px = cx + x;
                int py = cy + y;
                if (px < 0 || px >= width || py < 0 || py >= height) continue;
                if (x * x + y * y < r * r)
                    tex.SetPixel(px, py, cloudColor);
            }
        }

        // --- LAND BASE ---
        for (int y = 0; y < landHeight; y++)
        for (int x = 0; x < width; x++)
            tex.SetPixel(x, y, grass);

        // --- TERRAIN PATCHES (LEFT HALF ONLY) ---
        int half = width / 2;

        for (int i = 0; i < 120; i++)
        {
            Color patchColor = Random.value > 0.5f ? field : dirt;
            int px = Random.Range(0, half);
            int py = Random.Range(0, landHeight - 50);
            int w = Random.Range(80, 200);
            int h = Random.Range(40, 120);

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                int tx = px + x;
                int ty = py + y;
                if (tx >= half || ty >= landHeight) continue;
                tex.SetPixel(tx, ty, patchColor);
                tex.SetPixel(width - tx - 1, ty, patchColor);
            }
        }

        // --- ROADS ---
        for (int y = 100; y < landHeight; y += Random.Range(140, 200))
        {
            for (int x = 0; x < width; x++)
            for (int r = -6; r <= 6; r++)
                if (y + r < landHeight)
                    tex.SetPixel(x, y + r, road);
        }

        // --- HORIZON SEPARATOR (TREES / FENCES) ---
        for (int x = 0; x < width; x += Random.Range(20, 40))
        {
            int h = Random.Range(20, 50);
            Color c = Random.value > 0.3f ? treeDark : fence;

            for (int y = landHeight - h; y < landHeight; y++)
                tex.SetPixel(x, y, c);
        }

        tex.Apply();

        // --- SAVE PNG ---
        byte[] png = tex.EncodeToPNG();
        string dir = Application.dataPath + "/Generated";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(dir + "/battlefield.png", png);

        Debug.Log("Battlefield PNG generated!");
    }
}