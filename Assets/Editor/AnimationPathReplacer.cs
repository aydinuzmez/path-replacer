using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class AnimationPathReplacer : EditorWindow
{
    private List<string> originalPathList = new List<string>();
    private List<string> updatedPathList = new List<string>();

    private ListView originalPathListView;
    private ListView updatedPathListView;
    private TextField prefixTextField;

    [MenuItem("Window/Animation Path Replacer")]
    public static void ShowWindow()
    {
        var window = GetWindow<AnimationPathReplacer>();
        window.titleContent = new GUIContent("Animation Path Replacer");
        window.Show();
    }

    private void CreateGUI()
    {
        // Load the UXML file
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Editor/AnimationPathReplacer_UI.uxml");
        var root = visualTree.CloneTree();
        rootVisualElement.Add(root);

        // Find and assign ListViews and TextFields
        originalPathListView = root.Q<ListView>("OriginalListView");
        updatedPathListView = root.Q<ListView>("ModifiedListview");
        prefixTextField = root.Q<TextField>("PrefixTextField");

        // Button to load paths
        var loadButton = root.Q<Button>("LoadButton");
        loadButton.clicked += LoadAnimationPaths;

        // Button to update paths
        var updateButton = root.Q<Button>("UpdateButton");
        updateButton.clicked += UpdateAnimationPaths;
    }

    private void LoadAnimationPaths()
    {
        originalPathList.Clear();
        updatedPathList.Clear();

        // Animation Window türünü al
        Type animationWindowType = Type.GetType("UnityEditor.AnimationWindow,UnityEditor");
        if (animationWindowType == null)
        {
            Debug.LogError("AnimationWindow tipi bulunamadı. Unity sürümüne göre farklılık gösterebilir.");
            return;
        }

        // Animation Window örneğini bul
        var animationWindow = Resources.FindObjectsOfTypeAll(animationWindowType);
        if (animationWindow.Length == 0)
        {
            Debug.LogError("Animation Window açık değil veya bulunamadı.");
            return;
        }

        var animationWindowInstance = animationWindow[0];

        // m_AnimEditor alanını al
        var animEditorField = animationWindowType.GetField("m_AnimEditor", BindingFlags.NonPublic | BindingFlags.Instance);
        if (animEditorField == null)
        {
            Debug.LogError("m_AnimEditor alanı bulunamadı. Unity sürümüne göre değişiklik gösterebilir.");
            return;
        }

        var animEditor = animEditorField.GetValue(animationWindowInstance);
        if (animEditor == null)
        {
            Debug.LogWarning("Animasyon editörü aktif değil.");
            return;
        }

        // m_State alanını al ve animationClip özelliğini kullan
        var animEditorStateField = animEditor.GetType().GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
        if (animEditorStateField == null)
        {
            Debug.LogError("m_State alanı bulunamadı. Unity sürümüne göre değişiklik gösterebilir.");
            return;
        }

        var animEditorState = animEditorStateField.GetValue(animEditor);
        if (animEditorState == null)
        {
            Debug.LogWarning("Animasyon editör durumu aktif değil.");
            return;
        }

        // m_State içindeki activeAnimationClip özelliğini al
        var currentClipProperty = animEditorState.GetType().GetProperty("activeAnimationClip", BindingFlags.Public | BindingFlags.Instance);
        if (currentClipProperty == null)
        {
            Debug.LogError("activeAnimationClip özelliği bulunamadı. Unity sürümüne göre değişiklik gösterebilir.");
            return;
        }

        var activeClip = currentClipProperty.GetValue(animEditorState) as AnimationClip;
        if (activeClip == null)
        {
            Debug.LogWarning("Aktif animasyon klip bulunamadı.");
            return;
        }

        // Orijinal path'leri ve güncellenmiş path'leri listeye ekle
        string prefix = prefixTextField.value;
        foreach (var binding in AnimationUtility.GetCurveBindings(activeClip))
        {
            originalPathList.Add(binding.path);
            string newPath = prefix + "/" + binding.path;
            updatedPathList.Add(newPath); // Güncellenmiş path'i updatedPathList'e ekle
        }

        // ListView'lere öğeleri ekle
        originalPathListView.itemsSource = originalPathList; // Orijinal path'leri listele
        updatedPathListView.itemsSource = updatedPathList; // Güncellenmiş path'leri listele

        originalPathListView.RefreshItems();
        updatedPathListView.RefreshItems();
    }

    private void UpdateAnimationPaths()
    {
        Debug.Log("Güncelleme butonuna tıklandı.");

        if (originalPathList.Count == 0)
        {
            Debug.LogWarning("Güncellenecek path yok. Lütfen önce path'leri yükleyin.");
            return;
        }

        string prefix = prefixTextField?.value;
        if (string.IsNullOrEmpty(prefix))
        {
            Debug.LogWarning("Önek boş veya null.");
            return;
        }

        // Animation Window ve aktif klip referansları
        Type animationWindowType = Type.GetType("UnityEditor.AnimationWindow,UnityEditor");
        var animationWindow = Resources.FindObjectsOfTypeAll(animationWindowType);
        if (animationWindow.Length == 0)
        {
            Debug.LogError("Animation Window bulunamadı.");
            return;
        }

        var animationWindowInstance = animationWindow[0];
        var animEditorField = animationWindowType.GetField("m_AnimEditor", BindingFlags.NonPublic | BindingFlags.Instance);
        var animEditor = animEditorField?.GetValue(animationWindowInstance);
        if (animEditor == null)
        {
            Debug.LogError("Animasyon editörü örneği bulunamadı.");
            return;
        }

        var animEditorStateField = animEditor.GetType().GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
        var animEditorState = animEditorStateField?.GetValue(animEditor);
        if (animEditorState == null)
        {
            Debug.LogError("Animasyon editör durumu aktif değil.");
            return;
        }

        var currentClipProperty = animEditorState.GetType().GetProperty("activeAnimationClip", BindingFlags.Public | BindingFlags.Instance);
        var activeClip = currentClipProperty?.GetValue(animEditorState) as AnimationClip;

        if (activeClip == null)
        {
            Debug.LogWarning("Aktif animasyon klip bulunamadı. Güncelleme işlemi yapılamaz.");
            return;
        }

        // Path'leri güncelle ve animasyon klibe yaz
        updatedPathList.Clear();
        foreach (var binding in AnimationUtility.GetCurveBindings(activeClip))
        {
            string newPath = prefix + "/" + binding.path;
            updatedPathList.Add(newPath);

            // Yeni binding ile eğriyi klibe yaz
            var newBinding = binding;
            newBinding.path = newPath;

            // Mevcut eğriyi al ve yeni path ile ayarla
            var curve = AnimationUtility.GetEditorCurve(activeClip, binding);
            AnimationUtility.SetEditorCurve(activeClip, binding, null); // Eski path'i kaldır
            AnimationUtility.SetEditorCurve(activeClip, newBinding, curve); // Yeni path ile aynı eğriyi ekle
        }

        originalPathListView.itemsSource = originalPathList;
        updatedPathListView.itemsSource = updatedPathList;
        updatedPathListView.RefreshItems();

        Debug.Log("Path'ler güncellendi ve aktif klibe yazıldı!");
    }

}
