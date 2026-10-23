using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class UiAssetBuilder
{
    private const string FontSource = "Packages/com.unity.render-pipelines.core/Runtime/Debugging/Prefabs/Fonts/PerfectDOSVGA437.ttf";
    private const string FontDir = "Assets/UI/Fonts";
    private const string UiDir = "Assets/Resources/UI";
    private const string IconDir = "Assets/Resources/Icons";

    [MenuItem("Tools/Build UI Assets")]
    public static void BuildAll()
    {
        TMP_FontAsset font = BuildFont();
        BuildFrames();
        BuildIcons();
        AssetDatabase.SaveAssets();
        Debug.Log($"UI assets built. Default font: {font.name}");
    }

    public static TMP_FontAsset BuildFont()
    {
        Directory.CreateDirectory(FontDir);
        string ttfPath = FontDir + "/PerfectDOSVGA437.ttf";
        if (!File.Exists(ttfPath)) AssetDatabase.CopyAsset(FontSource, ttfPath);
        AssetDatabase.ImportAsset(ttfPath);
        Font source = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);

        string assetPath = FontDir + "/PixelFont SDF.asset";
        TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fa == null)
        {
            fa = TMP_FontAsset.CreateFontAsset(source, 32, 4, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic);
            fa.name = "PixelFont SDF";
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.material.name = "PixelFont SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.atlasTexture.name = "PixelFont SDF Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
        }
        fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");

        TMP_FontAsset liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (liberation != null && (fa.fallbackFontAssetTable == null || !fa.fallbackFontAssetTable.Contains(liberation)))
        {
            fa.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            fa.fallbackFontAssetTable.Add(liberation);
        }
        EditorUtility.SetDirty(fa);

        TMP_Settings.defaultFontAsset = fa;
        EditorUtility.SetDirty(TMP_Settings.instance);
        return fa;
    }

    private static readonly Color Outline = new Color(0.04f, 0.03f, 0.09f, 1f);

    private static void BuildFrames()
    {
        Directory.CreateDirectory(UiDir);
        SaveFrame("box", new Color(0.82f, 0.88f, 1f), new Color(0.11f, 0.13f, 0.28f, 0.94f));
        SaveFrame("box_gold", new Color(1f, 0.82f, 0.3f), new Color(0.22f, 0.18f, 0.34f, 0.97f));
        SaveFrame("slot", new Color(0.45f, 0.52f, 0.72f), new Color(0.05f, 0.06f, 0.15f, 0.9f));
    }

    private static void SaveFrame(string name, Color rim, Color fill)
    {
        const int n = 12;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, n - 1 - x), Mathf.Min(y, n - 1 - y));
                bool corner = (x == 0 || x == n - 1) && (y == 0 || y == n - 1);
                Color c = corner ? Color.clear : edge == 0 ? Outline : edge == 1 ? rim : fill;
                tex.SetPixel(x, y, c);
            }
        SavePng(tex, $"{UiDir}/{name}.png", new Vector4(3, 3, 3, 3));
    }

    private static readonly Dictionary<char, Color> Palette = new Dictionary<char, Color>
    {
        { 'K', Outline },
        { 'W', Color.white },
        { 'Y', new Color(1f, 0.85f, 0.25f) },
        { 'O', new Color(1f, 0.55f, 0.15f) },
        { 'R', new Color(0.95f, 0.25f, 0.25f) },
        { 'G', new Color(0.45f, 0.9f, 0.35f) },
        { 'B', new Color(0.3f, 0.55f, 1f) },
        { 'C', new Color(0.5f, 0.85f, 1f) },
        { 'L', new Color(0.7f, 0.92f, 1f) },
        { 'P', new Color(0.4f, 0.18f, 0.7f) },
        { 'V', new Color(0.75f, 0.45f, 1f) },
        { 'M', new Color(1f, 0.45f, 0.75f) },
        { 'N', new Color(0.6f, 0.38f, 0.2f) },
        { 'S', new Color(0.8f, 0.83f, 0.92f) },
        { 'D', new Color(0.22f, 0.22f, 0.3f) },
    };

    private static readonly Dictionary<string, string[]> Icons = new Dictionary<string, string[]>
    {
        { "boomerang", new[] {
            "................", "................", "..KKKK..........", ".KOOOOK.........",
            ".KOYOOOK........", "..KOOOOOK.......", "...KKOOOOK......", ".....KOOOOK.....",
            "......KOOOOK....", ".....KOOOOK.....", "....KOOOOK......", "...KOOOOK.......",
            "..KOOOOK........", "..KOOOK.........", "...KKK..........", "................" } },
        { "shuriken", new[] {
            ".......KK.......", "......KSSK......", "......KSSK......", ".....KSWSSK.....",
            ".....KSWSSK.....", ".KKKKSSKKSSKKKK.", "KSSSSSK..KSSSSSK", "KSWWWSK..KSSSSSK",
            ".KKKKSSKKSSKKKK.", ".....KSSSSK.....", ".....KSSWSK.....", "......KSSK......",
            "......KSSK......", ".......KK.......", "................", "................" } },
        { "missiles", new[] {
            ".......KK.......", "......KRRK......", ".....KRRRRK.....", ".....KWWWWK.....",
            ".....KWBBWK.....", ".....KWBBWK.....", ".....KWWWWK.....", ".....KWWWWK.....",
            "....KRKWWKRK....", "...KRRKWWKRRK...", "...KRKKKKKKRK...", "...KK.KOOK.KK...",
            "......KYOK......", ".......KYK......", "........K.......", "................" } },
        { "molotov", new[] {
            ".......KK.......", "......KYOK......", ".....KYORK......", "......KOK.......",
            "......KWK.......", "......KNK.......", ".....KNNNK......", ".....KGGGK......",
            "....KGGGGGK.....", "...KGGWGGGGK....", "...KGWGGGGGK....", "...KGGGGGGGK....",
            "...KGGGGGGGK....", "...KGGGGGGGK....", "....KKKKKKK.....", "................" } },
        { "orbs", new[] {
            "................", "......KKK.......", ".....KCWCK......", ".....KCCCK......",
            "......KKK.......", "..KKK.....KKK...", ".KCWCK...KCWCK..", ".KCCCK...KCCCK..",
            "..KKK.....KKK...", "................", "......KKK.......", ".....KCWCK......",
            ".....KCCCK......", "......KKK.......", "................", "................" } },
        { "aura", new[] {
            "................", ".....KKKKKK.....", "...KKMMMMMMKK...", "..KMMKKKKKKMMK..",
            ".KMMK......KMMK.", ".KMK........KMK.", "KMMK........KMMK", "KMK....WW....KMK",
            "KMK....WW....KMK", "KMMK........KMMK", ".KMK........KMK.", ".KMMK......KMMK.",
            "..KMMKKKKKKMMK..", "...KKMMMMMMKK...", ".....KKKKKK.....", "................" } },
        { "frost", new[] {
            "................", ".......KK.......", "...K..KLLK..K...", "..KLK.KLLK.KLK..",
            "...KLKKLLKKLK...", "....KLLWWLLK....", ".KKKKKLWWLKKKKK.", "KLLLLLWWWWLLLLLK",
            "KLLLLLWWWWLLLLLK", ".KKKKKLWWLKKKKK.", "....KLLWWLLK....", "...KLKKLLKKLK...",
            "..KLK.KLLK.KLK..", "...K..KLLK..K...", ".......KK.......", "................" } },
        { "stomp", new[] {
            "................", "....KKKK........", "...KNNNNK.......", "...KNNNNK.......",
            "...KNNNNK.......", "...KNNNNK.......", "...KNNNNKKK.....", "...KNNNNNNNK....",
            "...KNNNNNNNNK...", "...KKKKKKKKKK...", "................", ".YY.YY....YY.YY.",
            "Y..............Y", ".YYYY......YYYY.", "................", "................" } },
        { "lightning", new[] {
            ".........KKKK...", "........KYYYK...", ".......KYYYK....", "......KYYYK.....",
            ".....KYYYK......", "....KYYYKKKKK...", "...KYYYYYYYYK...", "...KKKKKYYYK....",
            ".......KYYK.....", "......KYYK......", ".....KYYK.......", "....KYYK........",
            "....KYK.........", "....KK..........", "................", "................" } },
        { "laser", new[] {
            "................", "................", "................", ".KKKK...........",
            "KSSSSK..........", "KSDDSKKKKKKKKKKK", "KSDRRRRRRRRRRRRR", "KSDRWWWWWWWWWWWW",
            "KSDRRRRRRRRRRRRR", "KSDDSKKKKKKKKKKK", "KSSSSK..........", ".KKKK...........",
            "................", "................", "................", "................" } },
        { "blackhole", new[] {
            "................", ".....KKKKKK.....", "...KKPPPPPPKK...", "..KPPVVVVVVPPK..",
            ".KPVVKKKKKKVVPK.", ".KPVKKDDDDKKVPK.", "KPVKKDDDDDDKKVPK", "KPVKDDDKKDDDKVPK",
            "KPVKDDDKKDDDKVPK", "KPVKKDDDDDDKKVPK", ".KPVKKDDDDKKVPK.", ".KPVVKKKKKKVVPK.",
            "..KPPVVVVVVPPK..", "...KKPPPPPPKK...", ".....KKKKKK.....", "................" } },
        { "mines", new[] {
            "................", ".......KK.......", "...K...KK...K...", "....K.KRRK.K....",
            ".....KRRRRK.....", "....KRRWRRRK....", "...KRRWRRRRRK...", ".KKKRRRRRRRRKKK.",
            "...KRRRRRRRRK...", "....KRRRRRRK....", ".....KRRRRK.....", "....K.KRRK.K....",
            "...K...KK...K...", ".......KK.......", "................", "................" } },
        { "glue", new[] {
            "................", "................", "......KKKK......", "....KKGGGGKK....",
            "...KGGWWGGGGK...", "..KGGWGGGGGGGK..", "..KGGGGGGGGGGK..", ".KGGGGGGGGGGGGK.",
            ".KGGGGGGGGGGGGK.", ".KGGGGGGGGGGGGK.", "..KGGKGGGGKGGK..", "...KK.KGGK.KK...",
            "......KGGK......", ".......KK.......", "................", "................" } },
        { "bomb", new[] {
            "..........Y.O...", "...........Y....", "..........KN....", ".........KN.....",
            ".....KKKKN......", "...KKDDDDKK.....", "..KDDDDDDDDK....", ".KDWWDDDDDDDK...",
            ".KDWDDDDDDDDK...", ".KDDDDDDDDDDK...", ".KDDDDDDDDDDK...", "..KDDDDDDDDK....",
            "...KKDDDDKK.....", ".....KKKK.......", "................", "................" } },
        { "chest", new[] {
            "................", "................", "..KKKKKKKKKKKK..", ".KBBBBBYYBBBBBK.",
            ".KBCCCBYYBCCCBK.", ".KBBBBBYYBBBBBK.", ".KKKKKKYYKKKKKK.", ".KBBBBKYYKBBBBK.",
            ".KBBBBKWWKBBBBK.", ".KBBBBKKKKBBBBK.", ".KBBBBBYYBBBBBK.", ".KBBBBBYYBBBBBK.",
            ".KBBBBBYYBBBBBK.", ".KKKKKKKKKKKKKK.", "................", "................" } },
        { "anvil", new[] {
            "................", "................", "................", "................",
            "..KKKKKKKKKKKK..", ".KSWWSSSSSSSSSK.", "KSSSSSSSSSSSSSK.", ".KKKSSSSSSSSKK..",
            "....KSSSSSSK....", "....KSDDDDSK....", "...KSSSSSSSSK...", "..KSSSSSSSSSSK..",
            "..KKKKKKKKKKKK..", "................", "................", "................" } },
        { "magnet", new[] {
            "................", "................", "..KKKK....KKKK..", "..KSSK....KSSK..",
            "..KSSK....KSSK..", "..KRRK....KRRK..", "..KRRK....KRRK..", "..KRRK....KRRK..",
            "..KRRRK..KRRRK..", "...KRRRKKRRRK...", "....KRRRRRRK....", ".....KKKKKK.....",
            "................", "................", "................", "................" } },
        { "speed", new[] {
            "................", "................", "..KK.....KK.....", "..KCK....KCK....",
            "...KCK....KCK...", "....KCK....KCK..", ".....KCK....KCK.", "......KCK....KCK",
            ".....KCK....KCK.", "....KCK....KCK..", "...KCK....KCK...", "..KCK....KCK....",
            "..KK.....KK.....", "................", "................", "................" } },
        { "buddy", new[] {
            "................", "..KK......KK....", "..KYK....KYK....", "..KYYKKKKYYK....",
            ".KYYYYYYYYYYK...", ".KYYYYYYYYYYK...", ".KYKKYYYYKKYK...", ".KYKWYYYYKWYK...",
            ".KYYYYYYYYYYK...", ".KYYYYKKYYYYK...", ".KYYYKYYKYYYK...", "..KYYYYYYYYK....",
            "...KKKKKKKK.....", "................", "................", "................" } },
        { "skill_blink", new[] {
            "................", "................", ".........KK.....", ".........KCK....",
            ".........KCCK...", ".LL..KKKKKCCCK..", "....KLLLLLLCCCK.", "LLL.KLWWWWWLCCCK",
            "LLL.KLWWWWWLCCCK", "....KLLLLLLCCCK.", ".LL..KKKKKCCCK..", ".........KCCK...",
            ".........KCK....", ".........KK.....", "................", "................" } },
        { "skill_chain", new[] {
            "....K...K.......", "...KYK.KYK......", "..KYYYKYYYK.....", ".KYYOOYOOYYK....",
            "..KYOWWWOYK.....", ".KYOWWWWWOYK....", "..KYOWWWOYK.....", ".KYYOOYOOYYK....",
            "..KYYYKYYYK.....", "...KYK.KYK.O....", "....K...K...K...", "...........KYK..",
            "..........KYWYK.", "...........KYK..", "............K...", "................" } },
        { "skill_cluster", new[] {
            "................", ".......Y........", ".......N........", "......KKK.......",
            ".....KDWDK......", ".....KDDDK......", "......KKK.......", "...N........N...",
            "..KKK......KKK..", ".KDWDK....KDWDK.", ".KDDDK....KDDDK.", "..KKK......KKK..",
            "................", "................", "................", "................" } },
        { "skill_napalm", new[] {
            "................", ".......KK.......", "......KRRK......", "......KRRK......",
            ".....KRRRRK.....", "....KRRORRRK....", "....KRROORRK....", "...KRROOORRRK...",
            "..KRROOYOORRK...", "..KROOYYYOORK...", "..KROYYWYYORK...", "..KROYWWWYORK...",
            "...KROYYYORK....", "....KKRRRKK.....", "..OOOKKKKKOOO...", "................" } },
        { "skill_sharpshooter", new[] {
            "................", ".......KK.......", ".....KKRRKK.....", "...KKRRKKRRKK...",
            "..KRRK.RR.KRRK..", "..KRK..RR..KRK..", ".KRK...RR...KRK.", ".KRKRRRWWRRRKRK.",
            ".KRKRRRWWRRRKRK.", ".KRK...RR...KRK.", "..KRK..RR..KRK..", "..KRRK.RR.KRRK..",
            "...KKRRKKRRKK...", ".....KKRRKK.....", ".......KK.......", "................" } },
        { "skill_berserk", new[] {
            "................", "....KKKKKKKK....", "...KRRRRRRRRK...", "..KRRRRRRRRRRK..",
            ".KRRKRRRRRRKRRK.", ".KRRRKRRRRKRRRK.", ".KRRWWKRRKWWRRK.", ".KRRWKKRRKKWRRK.",
            ".KRRRRRRRRRRRRK.", ".KRRRRRRRRRRRRK.", ".KRRRKKKKKKRRRK.", "..KRKWWWWWWKRK..",
            "...KRKKKKKKRK...", "....KKKKKKKK....", "................", "................" } },
        { "skill_thickskin", new[] {
            "................", "..KKKKKKKKKKKK..", "..KSSSSSSSSSSK..", "..KSWWSSSSSSSK..",
            "..KSWSSSSSSSSK..", "..KSSSSSSSSSSK..", "..KSSSSSSSSSSK..", "...KSSSSSSSSK...",
            "...KSSSSSSSSK...", "....KSSSSSSK....", ".....KSSSSK.....", "......KSSK......",
            ".......KK.......", "................", "................", "................" } },
        { "skill_regen", new[] {
            "................", "................", ".....KKKKKK.....", ".....KGGGGK.....",
            ".....KGWGGK.....", ".KKKKKGGGGKKKKK.", ".KGGGGGGGGGGGGK.", ".KGWGGGGGGGGGGK.",
            ".KGGGGGGGGGGGGK.", ".KKKKKGGGGKKKKK.", ".....KGGGGK.....", ".....KGGGGK.....",
            ".....KKKKKK.....", "................", "................", "................" } },
        { "skill_dodge", new[] {
            "................", "...........KKK..", "..........KWLLK.", ".........KWLLLK.",
            "........KWLLLLK.", ".......KWLLLLK..", "......KWLLLLK...", ".....KWLLLLK....",
            "....KWLLLLK.....", "...KWLLLK.......", "...KLLK.........", "..KKK...........",
            ".KK.............", "KK..............", "................", "................" } },
        { "skill_shockwave", new[] {
            "................", "................", ".....KKKKKK.....", "...KKBBBBBBKK...",
            "..KBBKKKKKKBBK..", ".KBK..KKKK..KBK.", ".KBK.KCCCCK.KBK.", "KBK.KCWWWWCK.KBK",
            "KBK.KCWWWWCK.KBK", ".KBK.KCCCCK.KBK.", ".KBK..KKKK..KBK.", "..KBBKKKKKKBBK..",
            "...KKBBBBBBKK...", ".....KKKKKK.....", "................", "................" } },
        { "skill_secondwind", new[] {
            "................", "................", "..KKK...KKK.....", ".KRRRK.KRRRK....",
            "KRWRRRKRRRRRK...", "KRWRRRRRRRRRK...", "KRRRRRRRRRRRK...", ".KRRRRRRRRRK....",
            "..KRRRRRRRK..G..", "...KRRRRRK..GGG.", "....KRRRK..GGGGG", ".....KRK.....G..",
            "......K......G..", ".............G..", "................", "................" } },
        { "skill_scholar", new[] {
            "................", "...KKKKKKKKKK...", "...KVVVVVVVVKK..", "...KVWWWWWWVKWK.",
            "...KVVVVVVVVKWK.", "...KVVWWWWVVKWK.", "...KVVVVVVVVKWK.", "...KVVVVVVVVKWK.",
            "...KVVVYYVVVKWK.", "...KVVYVVYVVKWK.", "...KVVVYYVVVKWK.", "...KVVVVVVVVKWK.",
            "...KKKKKKKKKKWK.", "....KWWWWWWWWWK.", ".....KKKKKKKKKK.", "................" } },
        { "skill_gourmet", new[] {
            "................", "................", "................", ".....KKKKKK.....",
            "...KKOOOOOOKK...", "..KOOWOOOWOOOK..", ".KOOOOOOOOOOOOK.", ".KKKKKKKKKKKKKK.",
            ".KGGGGGGGGGGGGK.", ".KRRRRRRRRRRRRK.", ".KNNNNNNNNNNNNK.", ".KNNNNNNNNNNNNK.",
            ".KKKKKKKKKKKKKK.", ".KOOOOOOOOOOOOK.", "..KKKKKKKKKKKK..", "................" } },
        { "skill_blacksmith", new[] {
            "................", ".KKKKKKKK.......", "KSSWWSSSSK......", "KSSSSSSSSK......",
            "KDDDDDDDDK......", ".KKKKNNKKK......", "Y....KNNK.......", ".Y....KNNK......",
            "Y......KNNK.....", "........KNNK....", ".........KNNK...", "..........KNNK..",
            "...........KNNK.", "............KKK.", "................", "................" } },
        { "skill_overdrive", new[] {
            "................", ".......KK.......", "......KYYK......", ".....KYYYYK.....",
            "....KYYKKYYK....", "...KYYK..KYYK...", "..KYYK....KYYK..", "..KKK......KKK..",
            ".......KK.......", "......KOOK......", ".....KOOOOK.....", "....KOOKKOOK....",
            "...KOOK..KOOK...", "..KOOK....KOOK..", "..KKK......KKK..", "................" } },
        // Kai (greatsword hero)
        { "sword", new[] {
            "..........KKKKK.", ".........KSSSWK.", "........KSSSWDK.", ".......KSSSWDK..",
            "......KSSSWDK...", ".....KSSSWDK....", "....KSSSWDK.....", "...KSSSWDK......",
            "..KKSSWDK.......", ".KYKKWDKK.......", "..KYKKKYK.......", "...KNKYK........",
            "..KNK.K.........", ".KNK............", "KYK.............", "KK.............." } },
        { "passive_heavy", new[] {
            "................", "...Y.......Y....", "..YWY.....YWY...", "...Y.......Y....",
            "......KKKK......", ".....KSSSSK.....", ".....KSSWSK.....", ".....KSSWSK.....",
            ".....KSSWSK.....", ".....KSSWSK.....", "..Y..KSSWSK..Y..", ".YWY.KSSWSK.YWY.",
            "..Y..KSSSSK..Y..", "...KKKKKKKKKK...", "..KDDDDDDDDDDK..", "...KKKKKKKKKK..." } },
        { "passive_execute", new[] {
            "................", "................", ".....KKKKKK.....", "....KWWWWWWK....",
            "...KWWWWWWWWK...", "...KWRRWWRRWK...", "...KWRRWWRRWK...", "...KWWWWWWWWK...",
            "....KWWKKWWK....", ".....KWWWWK.....", ".....KWKWKK.....", "......KKKK......",
            "................", "................", "................", "................" } },
        { "passive_dashslash", new[] {
            "................", "...........KK...", "..........KWK...", ".........KWK....",
            "........KWK.....", ".CCCC..KWK......", "......KWK.......", ".CCCCKWK........",
            "....KWK.........", ".CCKWK..........", "..KWK...........", ".KWK............",
            ".KK.............", "................", "................", "................" } },
        { "kai_chain", new[] {
            "................", ".......KK....KK.", "......KWK...KCK.", ".....KWK...KCK..",
            "....KWK...KCK...", "...KWK...KCK....", "..KWK...KCK.....", ".KWK...KCK......",
            ".KK...KCK.......", ".....KCK........", ".....KK.........", "................",
            "................", "................", "................", "................" } },
        { "kai_cluster", new[] {
            "................", "..........KK....", ".........KSWK...", "..KK......KK....",
            ".KSWK...........", "..KK.....KK.....", "........KSWK....", "...KK....KK.....",
            "..KSWK..........", "...KK....KKK....", ".........KSSK...", "..........KK....",
            "................", "................", "................", "................" } },
        { "kai_napalm", new[] {
            "................", "......Y.........", ".....YOY....Y...", "....YOROY..YOY..",
            "...YORRROYYOROY.", "...ORRRRRORRRRO.", "..KKKKKKKKKKKKK.", "..KSSSSSSSSSSWK.",
            "..KDDDDDDDDDDDK.", "..KKKKKKKKKKKKK.", "................", "................",
            "................", "................", "................", "................" } },
        // Nyx (drone hacker)
        { "drone", new[] {
            "................", "................", ".KKKK......KKKK.", "KSSSSK....KSSSSK",
            ".KKKK......KKKK.", "...KK......KK...", "....KKKKKKKK....", "...KDDDDDDDDK...",
            "..KDDCCCCCCDDK..", "..KDDCWWWWCDDK..", "..KDDCCCCCCDDK..", "...KDDDDDDDDK...",
            "....KKKKKKKK....", ".....K....K.....", "....KK....KK....", "................" } },
        { "nyx_chain", new[] {
            "................", "..........KK....", ".........KCCK...", "........KCCK....",
            ".......KCCK.....", "......KCCKKKK...", ".....KCCCCCCK...", "....KKKKCCK.....",
            "......KCCK......", ".....KCCK.......", "....KCCK........", "...KCCK.........",
            "...KKK..........", "................", "................", "................" } },
        { "nyx_cluster", new[] {
            "................", "................", "......KKKK......", "......KWCK......",
            "......KCCK......", "......KKKK......", "..KKK......KKK..", "..KCK......KCK..",
            "..KKK......KKK..", "................", ".....KKK..KKK...", ".....KCK..KCK...",
            ".....KKK..KKK...", "................", "................", "................" } },
        { "nyx_napalm", new[] {
            "................", "................", "................", "................",
            "................", "................", "...C......C.....", "..CWC....CWC....",
            "...C..C...C.....", ".....CWC........", "......C.....C...", "...........CWC..",
            ".KKKKKKKKKKKKKK.", ".KLLLLLLLLLLLLK.", ".KKKKKKKKKKKKKK.", "................" } },
        { "passive_hack", new[] {
            "................", "................", ".KKKKKKKKKKKKKK.", ".KDDDDDDDDDDDDK.",
            ".KDMDDDDDDDDDDK.", ".KDDMDDDDDDDDDK.", ".KDMDDMMMDDDDDK.", ".KDDDDDDDDDDDDK.",
            ".KDCCDCDCCCDDDK.", ".KDDDDDDDDDDDDK.", ".KKKKKKKKKKKKKK.", "......KSSK......",
            "....KKKKKKKK....", "................", "................", "................" } },
        { "passive_mark", new[] {
            "................", ".......RR.......", ".....KKRRKK.....", "....K..RR..K....",
            "...K...RR...K...", "..K..........K..", "..K....KK....K..", "RRRRR.KRRK.RRRRR",
            "RRRRR.KRRK.RRRRR", "..K....KK....K..", "..K..........K..", "...K...RR...K...",
            "....K..RR..K....", ".....KKRRKK.....", ".......RR.......", "................" } },
        { "passive_backdoor", new[] {
            "................", "................", "....KKKKKKKK....", "....KNNNNNNK....",
            "....KNPPPPNK....", "....KNPMMPNK....", "....KNPMMPNK....", "....KNPPPPNK....",
            "....KNNNNYNK....", "....KNNNNNNK....", "....KNPPPPNK....", "....KNPPPPNK....",
            "....KNNNNNNK....", "..KKKKKKKKKKKK..", "................", "................" } },
    };

    // Builds just the named icons (BuildIcons rewrites every icon).
    public static void BuildIcons(params string[] names)
    {
        Directory.CreateDirectory(IconDir);
        foreach (string name in names)
        {
            string[] rows = Icons[name];
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    tex.SetPixel(x, y, x < rows[15 - y].Length && Palette.TryGetValue(rows[15 - y][x], out Color c) ? c : Color.clear);
            SavePng(tex, $"{IconDir}/{name}.png", Vector4.zero);
        }
    }

    private static void BuildIcons()
    {
        Directory.CreateDirectory(IconDir);
        foreach (KeyValuePair<string, string[]> icon in Icons)
        {
            string[] rows = icon.Value;
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                string row = rows[15 - y];
                if (row.Length != 16) Debug.LogWarning($"Icon {icon.Key} row {15 - y} is {row.Length} wide");
                for (int x = 0; x < 16; x++)
                    tex.SetPixel(x, y, x < row.Length && Palette.TryGetValue(row[x], out Color c) ? c : Color.clear);
            }
            SavePng(tex, $"{IconDir}/{icon.Key}.png", Vector4.zero);
        }
    }

    private static void SavePng(Texture2D tex, string path, Vector4 border)
    {
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.spritePixelsPerUnit = 100;
        imp.spriteBorder = border;
        imp.SaveAndReimport();
    }
}
