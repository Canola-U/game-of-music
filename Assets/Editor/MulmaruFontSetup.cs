using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

// 물마루(도트 폰트)를 TMP 폰트 에셋으로 만들고, 모든 씬/프리팹의 텍스트에 적용한다.
// 메뉴: Tools > 폰트 > ...
public static class MulmaruFontSetup
{
    const string fontPath = "Assets/Fonts/Mulmaru/Mulmaru.ttf";
    const string assetPath = "Assets/Fonts/Mulmaru/Mulmaru Pixel.asset";

    // 물마루는 1em = 12px 격자로 그려진 폰트. 이 크기로 찍고, 화면에서도 12의 배수로 쓰면 가장 선명하다
    public const int pixelGrid = 12;

    [MenuItem("Tools/폰트/물마루 폰트 에셋 만들고 전체 적용")]
    static void CreateAndApply()
    {
        TMP_FontAsset fontAsset = CreateOrLoadFontAsset();
        if (fontAsset == null) return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = SceneManager.GetActiveScene().path;

        int texts = ForEachTextInProject(t => SetFont(t, fontAsset), inputField => inputField.fontAsset = fontAsset);
        SetDefaultFont(fontAsset);

        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene);

        EditorUtility.DisplayDialog("물마루 폰트 적용",
            $"텍스트 {texts}개에 물마루를 적용했어요.\n코드로 만드는 텍스트도 기본 폰트가 물마루로 바뀌었어요.", "확인");
    }

    [MenuItem("Tools/폰트/글자 크기를 12의 배수로 맞추기 (선택)")]
    static void SnapFontSizes()
    {
        if (!EditorUtility.DisplayDialog("글자 크기 맞추기",
            "모든 씬/프리팹 텍스트의 크기를 가장 가까운 12의 배수로 바꿔요.\n(도트가 가장 선명해지지만 레이아웃이 조금 바뀔 수 있어요)", "바꾸기", "취소"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = SceneManager.GetActiveScene().path;

        int texts = ForEachTextInProject(t =>
        {
            t.fontSize = Snap(t.fontSize);
            t.fontSizeMin = Snap(t.fontSizeMin);
            t.fontSizeMax = Snap(t.fontSizeMax);
        }, null);

        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene);
        Debug.Log($"[MulmaruFontSetup] 텍스트 {texts}개의 크기를 12의 배수로 맞췄어요.");
    }

    public static float Snap(float size)
    {
        return Mathf.Max(pixelGrid, Mathf.Round(size / pixelGrid) * pixelGrid);
    }

    static TMP_FontAsset CreateOrLoadFontAsset()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null)
        {
            EditorUtility.DisplayDialog("물마루 폰트", "폰트 파일을 찾을 수 없어요:\n" + fontPath, "확인");
            return null;
        }

        // 도트 폰트는 SDF 대신 래스터(비트맵)로 찍어야 뭉개지지 않는다.
        // 한글은 글자가 많아서 쓰는 글자만 그때그때 추가되는 Dynamic 방식 (12px라 2048 한 장에 한글 전부 들어감)
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            font, pixelGrid, 1, GlyphRenderMode.RASTER_HINTED, 2048, 2048, AtlasPopulationMode.Dynamic, false);
        fontAsset.name = "Mulmaru Pixel";

        AssetDatabase.CreateAsset(fontAsset, assetPath);

        Texture2D atlas = fontAsset.atlasTexture;
        atlas.name = "Mulmaru Pixel Atlas";
        AssetDatabase.AddObjectToAsset(atlas, fontAsset);

        Material material = fontAsset.material;
        material.name = "Mulmaru Pixel Material";
        if (!material.shader.name.Contains("Bitmap"))
        {
            Shader bitmap = Shader.Find("TextMeshPro/Bitmap");
            if (bitmap != null) material.shader = bitmap;
        }
        material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
        AssetDatabase.AddObjectToAsset(material, fontAsset);

        // 자주 쓰는 영문/숫자/기호는 미리 넣어둔다
        fontAsset.TryAddCharacters(
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·", out _);

        // 확대해도 도트가 번지지 않게 Point 필터
        fontAsset.atlasTexture.filterMode = FilterMode.Point;

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MulmaruFontSetup] 폰트 에셋 생성: " + assetPath);
        return fontAsset;
    }

    static void SetFont(TMP_Text text, TMP_FontAsset fontAsset)
    {
        text.font = fontAsset;
        text.fontSharedMaterial = fontAsset.material;
    }

    // 코드로 만드는 텍스트(결과 화면, 기록 리스트 등)도 물마루를 쓰도록 TMP 기본 폰트를 바꾼다
    static void SetDefaultFont(TMP_FontAsset fontAsset)
    {
        TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
        if (settings == null) return;

        SerializedObject so = new SerializedObject(settings);
        SerializedProperty prop = so.FindProperty("m_defaultFontAsset");
        if (prop == null) return;

        prop.objectReferenceValue = fontAsset;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    // Assets/Scenes의 모든 씬과 프로젝트의 프리팹에 있는 TMP 텍스트를 돌면서 action을 실행하고 저장한다
    static int ForEachTextInProject(System.Action<TMP_Text> action, System.Action<TMP_InputField> inputAction)
    {
        int total = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                count += ApplyUnder(root, action, inputAction);

            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            total += count;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/TextMesh Pro")) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int count = ApplyUnder(root, action, inputAction);
            if (count > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            total += count;
        }

        return total;
    }

    static int ApplyUnder(GameObject root, System.Action<TMP_Text> action, System.Action<TMP_InputField> inputAction)
    {
        int count = 0;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            action(text);
            EditorUtility.SetDirty(text);
            count++;
        }

        if (inputAction != null)
        {
            foreach (TMP_InputField input in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                inputAction(input);
                EditorUtility.SetDirty(input);
            }
        }
        return count;
    }
}
