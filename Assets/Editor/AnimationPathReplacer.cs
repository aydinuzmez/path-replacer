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
    private HelpBox helpbox;
    private TextField searchTextField; // Arama textfield'ı
    private DropdownField searchDropDown; // 
    private AnimationClip activeClip;
    List<EditorCurveBinding> binding_global;
    List<EditorCurveBinding> newbinding_global;

    SearchDropdownList selectedOption;

    EditorCurveBinding[] curve;

    [MenuItem("Animation/Animation Path Replacer")]
    public static void ShowWindow()
    {
        var window = GetWindow<AnimationPathReplacer>();
        window.titleContent = new GUIContent("Animation Path Replacer");
        window.Show();
    }

    public enum SearchDropdownList
    {
        Prefix,
        Infix,
        Suffix
    }



    private void CreateGUI()
    {
        // Load the UXML file
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Editor/AnimationPathReplacer_UI.uxml");
        var root = visualTree.CloneTree();
        rootVisualElement.Add(root);

        root.Q<Label>("versionTitle").text = "v1.1.0";

        // Find and assign ListViews and TextFields
        originalPathListView = root.Q<ListView>("OriginalListView");
        updatedPathListView = root.Q<ListView>("ModifiedListview");

        prefixTextField = root.Q<TextField>("PrefixTextField");
        prefixTextField.RegisterValueChangedCallback(evt => UpdateListview(evt.newValue, searchTextField.text, selectedOption));

        // Button to load paths
        var loadButton = root.Q<Button>("LoadButton");
        loadButton.clicked += LoadAnimationPaths;

        // Button to update paths
        var updateButton = root.Q<Button>("UpdateButton");
        updateButton.clicked += UpdateAnimationPaths;

        // 
        helpbox = root.Q<HelpBox>("Help");
        helpbox.messageType = HelpBoxMessageType.None;

        searchTextField = root.Q<TextField>("Search");
        searchTextField.RegisterValueChangedCallback(evt => UpdateListview(prefixTextField.text, evt.newValue, selectedOption));


        searchDropDown = root.Q<DropdownField>("SearchDropdown");

        searchDropDown.choices = new List<string>(System.Enum.GetNames(typeof(SearchDropdownList)));
        searchDropDown.value = SearchDropdownList.Prefix.ToString();

        selectedOption = SearchDropdownList.Prefix;
        searchDropDown.RegisterValueChangedCallback(evt =>
        {
            selectedOption = (SearchDropdownList)System.Enum.Parse(typeof(SearchDropdownList), evt.newValue);
            UpdateListview(prefixTextField.text, searchTextField.text, selectedOption);
        });
    }


    private void LoadAnimationPaths()
    {
        // Orijinal path'leri ve güncellenmiş path'leri listeye ekle
        activeClip = GetActiveAnimation();
        GetActiveCurve();
        UpdateListview(prefixTextField.text, searchTextField.text, selectedOption);
    }

    private AnimationClip GetActiveAnimation()
    {
        // Animation Window türünü al
        Type animationWindowType = Type.GetType("UnityEditor.AnimationWindow,UnityEditor");
        if (animationWindowType == null)
        {
            helpbox.messageType = HelpBoxMessageType.Error;
            helpbox.text = "AnimationWindow tipi bulunamadı. Unity sürümüne göre farklılık gösterebilir.";
            return null;
        }

        // Animation Window örneğini bul
        var animationWindow = Resources.FindObjectsOfTypeAll(animationWindowType);
        if (animationWindow.Length == 0)
        {
            helpbox.messageType = HelpBoxMessageType.Error;
            helpbox.text = "Animation Window açık değil veya bulunamadı.";
            return null;
        }

        var animationWindowInstance = animationWindow[0];

        // m_AnimEditor alanını al
        var animEditorField = animationWindowType.GetField("m_AnimEditor", BindingFlags.NonPublic | BindingFlags.Instance);
        if (animEditorField == null)
        {
            helpbox.messageType = HelpBoxMessageType.Error;
            helpbox.text = "m_AnimEditor alanı bulunamadı. Unity sürümüne göre değişiklik gösterebilir.";
            return null;
        }

        var animEditor = animEditorField.GetValue(animationWindowInstance);
        if (animEditor == null)
        {
            helpbox.messageType = HelpBoxMessageType.Warning;
            helpbox.text = "Animasyon editörü aktif değil.";
            return null;
        }

        // m_State alanını al ve animationClip özelliğini kullan
        var animEditorStateField = animEditor.GetType().GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
        if (animEditorStateField == null)
        {
            helpbox.messageType = HelpBoxMessageType.Error;
            helpbox.text = "m_State alanı bulunamadı. Unity sürümüne göre değişiklik gösterebilir.";
            return null;
        }

        var animEditorState = animEditorStateField.GetValue(animEditor);
        if (animEditorState == null)
        {
            helpbox.messageType = HelpBoxMessageType.Warning;
            helpbox.text = "Animasyon editör durumu aktif değil.";
            return null;
        }

        // m_State içindeki activeAnimationClip özelliğini al
        var currentClipProperty = animEditorState.GetType().GetProperty("activeAnimationClip", BindingFlags.Public | BindingFlags.Instance);
        if (currentClipProperty == null)
        {
            helpbox.messageType = HelpBoxMessageType.Error;
            helpbox.text = "activeAnimationClip özelliği bulunamadı. Unity sürümüne göre değişiklik gösterebilir.";
            return null;
        }

        var activeClip = currentClipProperty.GetValue(animEditorState) as AnimationClip;
        if (activeClip == null)
        {
            helpbox.messageType = HelpBoxMessageType.Warning;
            helpbox.text = "Aktif animasyon klip bulunamadı.";
            return null;
        }
        return activeClip;

    }

    private EditorCurveBinding[] GetActiveCurve()
    {
        curve = AnimationUtility.GetCurveBindings(activeClip);
        return curve;
    }

    private void UpdateListview(string path, string searchTerm, SearchDropdownList option)
    {
        originalPathList.Clear();
        updatedPathList.Clear();

        if (activeClip == null)
        {
            activeClip = GetActiveAnimation();
        }

        newbinding_global = new List<EditorCurveBinding>();
        binding_global = new List<EditorCurveBinding>();


        foreach (EditorCurveBinding binding in curve)
        {
            string newPath;
            EditorCurveBinding newBinding;
            if (binding.path.Contains(searchTerm) == true)
            {
                switch (option)
                {
                    case SearchDropdownList.Prefix: //Option 1
                        if (path == "") newPath = binding.path;
                        else newPath = path + "/" + binding.path;

                        originalPathList.Add(binding.path);
                        updatedPathList.Add(newPath); // Güncellenmiş path'i updatedPathList'e ekle
                        break;

                    case SearchDropdownList.Infix: //Option 2
                        newPath = binding.path.Replace(searchTerm, path);

                        originalPathList.Add(binding.path);
                        updatedPathList.Add(newPath); // Güncellenmiş path'i updatedPathList'e ekle
                        break;

                    case SearchDropdownList.Suffix: //Option 3

                        if (path == "") newPath = binding.path;
                        else newPath = binding.path + "/" + path;

                        originalPathList.Add(binding.path);
                        updatedPathList.Add(newPath); // Güncellenmiş path'i updatedPathList'e ekle
                        break;
                    default:
                        newPath = "";
                        break;
                }

                //Binding al ve değiştir
                newBinding = binding;
                newBinding.path = newPath;

                newbinding_global.Add(newBinding);
                binding_global.Add(binding);
            }
        }




        // ListView'lere öğeleri ekle
        originalPathListView.itemsSource = originalPathList; // Orijinal path'leri listele
        updatedPathListView.itemsSource = updatedPathList; // Güncellenmiş path'leri listele

        helpbox.messageType = HelpBoxMessageType.Info;
        helpbox.text = "Liste Yüklendi. Toplam Path: " + originalPathList.Count;


        originalPathListView.RefreshItems();
        updatedPathListView.RefreshItems();
    }


    private void UpdateAnimationPaths()
    {
        helpbox.messageType = HelpBoxMessageType.Info;
        helpbox.text = "Güncelleme butonuna tıklandı.";

        if (originalPathList.Count == 0)
        {
            helpbox.messageType = HelpBoxMessageType.Warning;
            helpbox.text = "Güncellenecek path yok. Lütfen önce path'leri yükleyin.";
            return;
        }

        string prefix = prefixTextField?.value;
        if (string.IsNullOrEmpty(prefix))
        {
            helpbox.messageType = HelpBoxMessageType.Warning;
            helpbox.text = "Ön ek boş veya null.";
            return;
        }

        // Animation Window ve aktif klip referansları
        Type animationWindowType = Type.GetType("UnityEditor.AnimationWindow,UnityEditor");
        var animationWindow = Resources.FindObjectsOfTypeAll(animationWindowType);
        if (animationWindow.Length == 0)
        {
            helpbox.messageType = HelpBoxMessageType.Error;
            helpbox.text = "Animation Window bulunamadı.";
            return;
        }

        var animationWindowInstance = animationWindow[0];
        var animEditorField = animationWindowType.GetField("m_AnimEditor", BindingFlags.NonPublic | BindingFlags.Instance);
        var animEditor = animEditorField?.GetValue(animationWindowInstance);
        if (animEditor == null)
        {
            helpbox.messageType = HelpBoxMessageType.Error;
            helpbox.text = "Animasyon editörü örneği bulunamadı.";
            return;
        }

        var animEditorStateField = animEditor.GetType().GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
        var animEditorState = animEditorStateField?.GetValue(animEditor);
        if (animEditorState == null)
        {
            helpbox.messageType = HelpBoxMessageType.Error;
            helpbox.text = "Animasyon editör durumu aktif değil.";
            return;
        }

        var currentClipProperty = animEditorState.GetType().GetProperty("activeAnimationClip", BindingFlags.Public | BindingFlags.Instance);
        var activeClip = currentClipProperty?.GetValue(animEditorState) as AnimationClip;

        if (activeClip == null)
        {
            helpbox.messageType = HelpBoxMessageType.Warning;
            helpbox.text = "Aktif animasyon klip bulunamadı. Güncelleme işlemi yapılamaz.";
            return;
        }

        // Path'leri güncelle ve animasyon klibe yaz

        int index = 0;
        foreach (var newbinding in newbinding_global)
        {

            var curve = AnimationUtility.GetEditorCurve(activeClip, binding_global[index]);
            AnimationUtility.SetEditorCurve(activeClip, binding_global[index], null); // Eski path'i kaldır
            AnimationUtility.SetEditorCurve(activeClip, newbinding, curve);
            index++;

        }

        helpbox.messageType = HelpBoxMessageType.Info;
        helpbox.text = "Aktif Animasyonun üzerine yazıldı! Toplam Path : " + index.ToString();

    }

}



