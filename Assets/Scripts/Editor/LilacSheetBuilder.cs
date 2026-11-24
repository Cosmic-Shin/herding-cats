using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Palette swaps of Lilac's art: her ultimate "bloom" sheets (lilac hair) and her lilac-bomb icons.
// Re-run Tools > Build Lilac Sheets after editing her sprites or the bomb icons.
public static class LilacSheetBuilder
{
    private const string Dir = "Assets/Resources/Heroes/Lilac", BloomDir = "Assets/Resources/Heroes/LilacBloom", IconDir = "Assets/Resources/Icons";

    // Her hair shades (shadow, base, highlight) -> bloom shades. Tweak here if you change her palette.
    private static readonly Dictionary<Color32, Color32> HairToBloom = new Dictionary<Color32, Color32>
    {
        { new Color32(34, 18, 42, 255), new Color32(98, 62, 150, 255) },
        { new Color32(55, 31, 65, 255), new Color32(150, 104, 204, 255) },
        { new Color32(112, 72, 132, 255), new Color32(214, 184, 250, 255) },
    };

    // The grey bomb body used across the bomb icons -> lilac.
    private static readonly Dictionary<Color32, Color32> BombToLilac = new Dictionary<Color32, Color32>
    {
        { new Color32(56, 56, 77, 255), new Color32(132, 96, 186, 255) },
    };

    private static readonly string[] BombIcons = { "bomb", "skill_cluster", "carpet", "concussion", "firebomb", "furyfuse", "magnetbomb" };

    [MenuItem("Tools/Build Lilac Sheets")]
    public static void Build()
    {
        Directory.CreateDirectory(BloomDir);
        foreach (string src in Directory.GetFiles(Dir, "Lilac_*.png"))
        {
            string dst = BloomDir + "/LilacBloom_" + Path.GetFileName(src).Substring("Lilac_".Length);
            if (!File.Exists(dst)) AssetDatabase.CopyAsset(src.Replace('\\', '/'), dst); // keeps her slicing and import settings
            Recolor(src, dst, HairToBloom);
        }
        foreach (string name in BombIcons)
        {
            string id = name.StartsWith("skill_") ? name.Substring(6) : name;
            string src = $"{IconDir}/{name}.png", dst = $"{IconDir}/lilac_{id}.png";
            if (!File.Exists(dst)) AssetDatabase.CopyAsset(src, dst);
            Recolor(src, dst, BombToLilac);
        }
        Debug.Log("Lilac sheets built.");
    }

    private static void Recolor(string src, string dst, Dictionary<Color32, Color32> map)
    {
        var tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes(src));
        Color32[] px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++)
            if (map.TryGetValue(px[i], out Color32 c)) px[i] = c;
        tex.SetPixels32(px);
        File.WriteAllBytes(dst, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
    }
}
