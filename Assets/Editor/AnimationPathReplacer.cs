using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Editor
{
    public class AnimationPathReplacer : EditorWindow
    {
        private List<string> _originalPathList ;
        private List<string> _updatedPathList;

        private ListView _originalPathListView;
        private ListView _updatedPathListView;
        private TextField _prefixTextField;
        private HelpBox _helpbox;
        private TextField _searchTextField; 
        private DropdownField _searchDropDown; 
        private AnimationClip _activeClip;
        List<EditorCurveBinding> _bindingGlobal;
        List<EditorCurveBinding> _newBindingGlobal;
        List<EditorCurveBinding> _oldNewBindingGlobal;


        private Button _discardButton;
        private Button _updateButton;
    

        SearchDropdownList _selectedOption;

        EditorCurveBinding[] _curve;

        [MenuItem("Animation/Animation Path Replacer")]
        public static void ShowWindow()
        {
            var window = GetWindow<AnimationPathReplacer>();
            window.titleContent = new GUIContent("Animation Path Replacer");
            window.Show();
        }

        private enum SearchDropdownList
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

            root.Q<Label>("versionTitle").text = "v1.2.0";

            // Find and assign ListViews and TextFields
            _originalPathListView = root.Q<ListView>("OriginalListView");
            _updatedPathListView = root.Q<ListView>("ModifiedListview");

            _prefixTextField = root.Q<TextField>("PrefixTextField");
            _prefixTextField.RegisterValueChangedCallback(evt => UpdateListview(evt.newValue, _searchTextField.text, _selectedOption));

            // Button to load paths
            var loadButton = root.Q<Button>("LoadButton");
            loadButton.clicked += LoadAnimationPaths;

            // Button to update paths
            _updateButton = root.Q<Button>("UpdateButton");
            _updateButton.clicked += UpdateAnimationPaths;

            _discardButton = root.Q<Button>("DiscardButton");
            _discardButton.clicked += DiscardAllChanged;

            // 
            _helpbox = root.Q<HelpBox>("Help");
            _helpbox.messageType = HelpBoxMessageType.None;

            _searchTextField = root.Q<TextField>("Search");
            _searchTextField.RegisterValueChangedCallback(evt => UpdateListview(_prefixTextField.text, evt.newValue, _selectedOption));


            _searchDropDown = root.Q<DropdownField>("SearchDropdown");

            _searchDropDown.choices = new List<string>(System.Enum.GetNames(typeof(SearchDropdownList)));
            _searchDropDown.value = SearchDropdownList.Prefix.ToString();

            _selectedOption = SearchDropdownList.Prefix;
            _searchDropDown.RegisterValueChangedCallback(evt =>
            {
                _selectedOption = (SearchDropdownList)System.Enum.Parse(typeof(SearchDropdownList), evt.newValue);
                UpdateListview(_prefixTextField.text, _searchTextField.text, _selectedOption);
            });
        }


        private void LoadAnimationPaths()
        {
            _discardButton.text = "Discard All Changes";
            // Orijinal path'leri ve güncellenmiş path'leri listeye ekle
            _activeClip = GetActiveAnimation();
            GetActiveCurve();
            UpdateListview(_prefixTextField.text, _searchTextField.text, _selectedOption);
        }

        private AnimationClip GetActiveAnimation()
        {
            // Animation Window türünü al
            Type animationWindowType = Type.GetType("UnityEditor.AnimationWindow,UnityEditor");
            if (animationWindowType == null)
            {
                _helpbox.messageType = HelpBoxMessageType.Error;
                _helpbox.text = "AnimationWindow tipi bulunamadı. Unity sürümüne göre farklılık gösterebilir.";
                return null;
            }

            // Animation Window örneğini bul
            var animationWindow = Resources.FindObjectsOfTypeAll(animationWindowType);
            if (animationWindow.Length == 0)
            {
                _helpbox.messageType = HelpBoxMessageType.Error;
                _helpbox.text = "Animation Window açık değil veya bulunamadı.";
                return null;
            }

            var animationWindowInstance = animationWindow[0];

            // m_AnimEditor alanını al
            var animEditorField = animationWindowType.GetField("m_AnimEditor", BindingFlags.NonPublic | BindingFlags.Instance);
            if (animEditorField == null)
            {
                _helpbox.messageType = HelpBoxMessageType.Error;
                _helpbox.text = "m_AnimEditor alanı bulunamadı. Unity sürümüne göre değişiklik gösterebilir.";
                return null;
            }

            var animEditor = animEditorField.GetValue(animationWindowInstance);
            if (animEditor == null)
            {
                _helpbox.messageType = HelpBoxMessageType.Warning;
                _helpbox.text = "Animasyon editörü aktif değil.";
                return null;
            }

            // m_State alanini al ve animationClip ozelligini kullan
            var animEditorStateField = animEditor.GetType().GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
            if (animEditorStateField == null)
            {
                _helpbox.messageType = HelpBoxMessageType.Error;
                _helpbox.text = "m_State alanı bulunamadı. Unity sürümüne göre değişiklik gösterebilir.";
                return null;
            }

            var animEditorState = animEditorStateField.GetValue(animEditor);
            if (animEditorState == null)
            {
                _helpbox.messageType = HelpBoxMessageType.Warning;
                _helpbox.text = "Animasyon editör durumu aktif değil.";
                return null;
            }

            // m_State içindeki activeAnimationClip özelliğini al
            var currentClipProperty = animEditorState.GetType().GetProperty("activeAnimationClip", BindingFlags.Public | BindingFlags.Instance);
            if (currentClipProperty == null)
            {
                _helpbox.messageType = HelpBoxMessageType.Error;
                _helpbox.text = "activeAnimationClip özelliği bulunamadı. Unity sürümüne göre değişiklik gösterebilir.";
                return null;
            }

            var activeClip = currentClipProperty.GetValue(animEditorState) as AnimationClip;
            if (activeClip == null)
            {
                _helpbox.messageType = HelpBoxMessageType.Warning;
                _helpbox.text = "Aktif animasyon klip bulunamadı.";
                return null;
            }
            return activeClip;

        }

        private EditorCurveBinding[] GetActiveCurve()
        {
            _curve = AnimationUtility.GetCurveBindings(_activeClip);
            return _curve;
        }

        private void UpdateListview(string path, string searchTerm, SearchDropdownList option)
        {
            _originalPathList.Clear();
            _updatedPathList.Clear();

            if (_activeClip == null)
            {
                _activeClip = GetActiveAnimation();
            }

            _newBindingGlobal = new List<EditorCurveBinding>();
            _bindingGlobal = new List<EditorCurveBinding>();


            foreach (EditorCurveBinding binding in _curve)
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

                            _originalPathList.Add(binding.path);
                            _updatedPathList.Add(newPath); // Güncellenmiş path'i updatedPathList'e ekle
                            break;

                        case SearchDropdownList.Infix: //Option 2
                            newPath = binding.path.Replace(searchTerm, path);

                            _originalPathList.Add(binding.path);
                            _updatedPathList.Add(newPath); // Güncellenmiş path'i updatedPathList'e ekle
                            break;

                        case SearchDropdownList.Suffix: //Option 3

                            if (path == "") newPath = binding.path;
                            else newPath = binding.path + "/" + path;

                            _originalPathList.Add(binding.path);
                            _updatedPathList.Add(newPath); // Güncellenmiş path'i updatedPathList'e ekle
                            break;
                        default:
                            newPath = "";
                            break;
                    }

                    //Binding al ve değiştir
                    newBinding = binding;
                    newBinding.path = newPath;

                    _newBindingGlobal.Add(newBinding);
                    _bindingGlobal.Add(binding);
                }
            }




            // ListView'lere öğeleri ekle
            _originalPathListView.itemsSource = _originalPathList; // Orijinal path'leri listele
            _updatedPathListView.itemsSource = _updatedPathList; // Güncellenmiş path'leri listele

            _helpbox.messageType = HelpBoxMessageType.Info;
            _helpbox.text = "Liste Yüklendi. Toplam Path: " + _originalPathList.Count;


            _originalPathListView.RefreshItems();
            _updatedPathListView.RefreshItems();
        }

        private void UpdateAnimationPaths()
        {
            _helpbox.messageType = HelpBoxMessageType.Info;
            _helpbox.text = "Güncelleme butonuna tıklandı.";
            _discardButton.text = "Discard All Changes (saved)";

            if (_originalPathList.Count == 0)
            {
                _helpbox.messageType = HelpBoxMessageType.Warning;
                _helpbox.text = "Güncellenecek path yok. Lütfen önce path'leri yükleyin.";
                return;
            }

            string prefix = _prefixTextField?.value;
            if (string.IsNullOrEmpty(prefix))
            {
                _helpbox.messageType = HelpBoxMessageType.Warning;
                _helpbox.text = "Ön ek boş veya null.";
                return;
            }

            // Animation Window ve aktif klip referansları
            Type animationWindowType = Type.GetType("UnityEditor.AnimationWindow,UnityEditor");
            var animationWindow = Resources.FindObjectsOfTypeAll(animationWindowType);
            if (animationWindow.Length == 0)
            {
                _helpbox.messageType = HelpBoxMessageType.Error;
                _helpbox.text = "Animation Window bulunamadı.";
                return;
            }

            var animationWindowInstance = animationWindow[0];
            var animEditorField = animationWindowType.GetField("m_AnimEditor", BindingFlags.NonPublic | BindingFlags.Instance);
            var animEditor = animEditorField?.GetValue(animationWindowInstance);
            if (animEditor == null)
            {
                _helpbox.messageType = HelpBoxMessageType.Error;
                _helpbox.text = "Animasyon editörü örneği bulunamadı.";
                return;
            }

            var animEditorStateField = animEditor.GetType().GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
            var animEditorState = animEditorStateField?.GetValue(animEditor);
            if (animEditorState == null)
            {
                _helpbox.messageType = HelpBoxMessageType.Error;
                _helpbox.text = "Animasyon editör durumu aktif değil.";
                return;
            }

            //var currentClipProperty = animEditorState.GetType().GetProperty("activeAnimationClip", BindingFlags.Public | BindingFlags.Instance);
            //var activeClip = currentClipProperty?.GetValue(animEditorState) as AnimationClip;

            if (_activeClip == null)
            {
                _helpbox.messageType = HelpBoxMessageType.Warning;
                _helpbox.text = "Aktif animasyon klip bulunamadı. Güncelleme işlemi yapılamaz.";
                return;
            }

            // Path'leri güncelle ve animasyon klibe yaz


            if (_oldNewBindingGlobal != null)
            {
                int index2 = 0;
                foreach (var newbinding in _newBindingGlobal)
                {
                    var curve = AnimationUtility.GetEditorCurve(_activeClip, _oldNewBindingGlobal[index2]);
                    AnimationUtility.SetEditorCurve(_activeClip, _oldNewBindingGlobal[index2], null); // Eski path'i kaldır
                    AnimationUtility.SetEditorCurve(_activeClip, newbinding, curve);
                    index2++;
                }
                _helpbox.messageType = HelpBoxMessageType.Info;
                _helpbox.text = "Aktif Animasyonun üzerine tekrar yazıldı! Toplam Path : " + index2.ToString();
            }
            else
            {

                int index = 0;
                foreach (var newbinding in _newBindingGlobal)
                {
                    var curve = AnimationUtility.GetEditorCurve(_activeClip, _bindingGlobal[index]);
                    AnimationUtility.SetEditorCurve(_activeClip, _bindingGlobal[index], null); // Eski path'i kaldır
                    AnimationUtility.SetEditorCurve(_activeClip, newbinding, curve);
                    index++;
                }
                _helpbox.messageType = HelpBoxMessageType.Info;
                _helpbox.text = "Aktif Animasyonun üzerine yazıldı! Toplam Path : " + index.ToString();
            }
            _oldNewBindingGlobal = _newBindingGlobal;

        }
        private void DiscardAllChanged()
        {
            _discardButton.text = "Discard All Changes";
            int index2 = 0;
            foreach (var binding in _bindingGlobal)
            {
                var curve = AnimationUtility.GetEditorCurve(_activeClip, _oldNewBindingGlobal[index2]);
                AnimationUtility.SetEditorCurve(_activeClip, _oldNewBindingGlobal[index2], null); // Eski path'i kaldır
                AnimationUtility.SetEditorCurve(_activeClip, binding, curve);
                index2++;
            }
            _helpbox.messageType = HelpBoxMessageType.Info;
            _helpbox.text = "Aktif Animasyonun Eski haline getirildi.";
            _oldNewBindingGlobal = null;
        }
    }
}



