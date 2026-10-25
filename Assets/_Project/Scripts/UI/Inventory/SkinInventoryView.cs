using System;
using System.Collections.Generic;
using PawArena.Core.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawArena.UI.Inventory
{
    /// <summary>Local skin browser. Selection is read-only until the explicit equip button is pressed.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SkinInventoryView : MonoBehaviour
    {
        [SerializeField] private StyleSheet _styleSheet = null;
        private readonly Dictionary<string, Button> _cards = new(StringComparer.Ordinal);
        private SkinInventorySession _session;
        private Func<string, Texture2D> _thumbnail;
        private VisualElement _root;
        private VisualElement _browser;
        private VisualElement _details;
        private Label _title;
        private Label _description;
        private Label _feedback;
        private Label _equipped;
        private Image _image;
        private Button _equip;
        private Button _retry;
        private Button _unequip;
        private string _category = "Skin";
        private bool _listening;
        private string _search = string.Empty;
        private string _filter = "Tất cả";

        private void OnEnable()
        {
            if (_session != null) { Build(); Listen(); Refresh(); }
        }
        private void OnDisable()
        {
            StopListening();
            _root?.Clear();
            _cards.Clear();
        }

        /// <summary>Binds a domain session and a thumbnail resolver provided by the composition root.</summary>
        public void Bind(SkinInventorySession session, Func<string, Texture2D> thumbnail)
        {
            Unbind();
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _thumbnail = thumbnail;
            if (isActiveAndEnabled) { Build(); Listen(); Refresh(); }
        }

        /// <summary>Disconnects the view without destroying or saving the shared session.</summary>
        public void Unbind()
        {
            StopListening();
            _root?.Clear();
            _cards.Clear();
            _session = null;
            _thumbnail = null;
        }

        private void Listen()
        {
            if (_listening) { return; }
            _session.Changed += Refresh;
            _listening = true;
        }
        private void StopListening()
        {
            if (!_listening) { return; }
            _session.Changed -= Refresh;
            _listening = false;
        }

        private void Build()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _root.Clear();
            _cards.Clear();
            if (_styleSheet != null && !_root.styleSheets.Contains(_styleSheet)) { _root.styleSheets.Add(_styleSheet); }
            _root.pickingMode = PickingMode.Ignore;
            _root.AddToClassList("inventory-root");
            var header = new VisualElement();
            header.AddToClassList("header");
            header.Add(new Label("PAW ARENA  /  TỦ ĐỒ") { name = "inventory-title" });
            var toggle = new Button(() => SetOpen(_browser.style.display == DisplayStyle.None)) { text = "Mở / đóng tủ đồ" };
            header.Add(toggle);
            _root.Add(header);

            _browser = new VisualElement();
            _browser.AddToClassList("browser");
            _browser.Add(new Label("SKIN & PHỤ KIỆN") { name = "section-title" });
            var categories = new DropdownField("Nhóm", new List<string> { "Skin", "Đầu", "Eo", "Tay" }, 0) { name = "item-category" };
            categories.value = _category;
            categories.RegisterValueChangedCallback(evt =>
            {
                _category = evt.newValue;
                foreach (var item in _session.Items) { if (MatchesCategory(item)) { _session.Select(item.ItemId); break; } }
                Refresh();
            });
            _browser.Add(categories);
            var filters = new VisualElement();
            filters.AddToClassList("filters");
            var search = new TextField { value = _search, tooltip = "Tìm vật phẩm theo tên" };
            search.AddToClassList("search");
            search.RegisterValueChangedCallback(evt => { _search = evt.newValue; Refresh(); });
            filters.Add(new Label("Tìm:"));
            filters.Add(search);
            var dropdown = new DropdownField(new List<string> { "Tất cả", "Đã sở hữu", "Đang dùng" }, 0);
            dropdown.value = _filter;
            dropdown.RegisterValueChangedCallback(evt => { _filter = evt.newValue; Refresh(); });
            filters.Add(dropdown);
            _browser.Add(filters);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("skin-scroll");
            scroll.contentContainer.AddToClassList("skin-grid");
            foreach (var item in _session.Items)
            {
                var id = item.ItemId;
                var card = new Button(() => _session.Select(id));
                card.name = id;
                card.AddToClassList("skin-card");
                card.Add(new Image { image = _thumbnail?.Invoke(id), scaleMode = ScaleMode.ScaleToFit });
                card.Add(new Label(item.DisplayName));
                var rarity = new Label(item.Rarity.ToString());
                rarity.AddToClassList("rarity");
                card.Add(rarity);
                card.Add(new Label { name = "ownership" });
                scroll.Add(card);
                _cards.Add(id, card);
            }
            _browser.Add(scroll);
            _browser.Add(new Label("Chọn để xem • Bấm Trang bị để đổi trên nhân vật") { name = "hint" });
            _root.Add(_browser);

            _details = new VisualElement();
            _details.AddToClassList("details");
            _title = new Label();
            _title.AddToClassList("selected-title");
            _details.Add(_title);
            _image = new Image { scaleMode = ScaleMode.ScaleToFit };
            _image.AddToClassList("selected-image");
            _details.Add(_image);
            _description = new Label();
            _description.AddToClassList("description");
            _details.Add(_description);
            _equip = new Button(() => _session.EquipSelected()) { name = "equip-skin" };
            _equip.AddToClassList("equip");
            _details.Add(_equip);
            _unequip = new Button(() => _session.UnequipSelected()) { name = "unequip-gear", text = "Tháo phụ kiện" };
            _details.Add(_unequip);
            _feedback = new Label();
            _feedback.AddToClassList("feedback");
            _details.Add(_feedback);
            _retry = new Button(() => _session.Save()) { text = "Thử lưu lại" };
            _details.Add(_retry);
            _root.Add(_details);
            _equipped = new Label();
            _equipped.AddToClassList("equipped-label");
            _root.Add(_equipped);
        }

        private void SetOpen(bool open)
        {
            _browser.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _details.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Refresh()
        {
            if (_root == null || _session == null) { return; }
            foreach (var item in _session.Items)
            {
                var card = _cards[item.ItemId];
                var owned = _session.Inventory.IsOwned(item.ItemId);
                var equipped = _session.Inventory.IsEquipped(item.ItemId);
                var visible = MatchesCategory(item) && item.DisplayName.IndexOf(_search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (_filter != "Đã sở hữu" || owned) && (_filter != "Đang dùng" || equipped);
                card.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                card.EnableInClassList("selected", item.ItemId == _session.SelectedItemId);
                card.Q<Label>("ownership").text = equipped ? "ĐANG DÙNG" : owned ? "Đã sở hữu" : "Chưa sở hữu";
            }
            var selected = _session.Find(_session.SelectedItemId);
            _title.text = selected?.DisplayName ?? "Chưa có skin";
            _image.image = selected != null ? _thumbnail?.Invoke(selected.ItemId) : null;
            _description.text = selected == null ? "Danh mục đang trống." :
                selected.Rarity + (selected.Category == ItemCategory.Gear ? " • Phụ kiện trang trí\nMột món mỗi slot • Không tăng chỉ số." : " • Chỉ thay diện mạo\nGiữ nguyên rig, animation và gameplay.");
            var isEquipped = selected != null && _session.Inventory.IsEquipped(selected.ItemId);
            var isOwned = selected != null && _session.Inventory.IsOwned(selected.ItemId);
            _equip.text = isEquipped ? "Đang trang bị" : isOwned ? "Trang bị" : "Chưa sở hữu";
            _equip.SetEnabled(isOwned && !isEquipped);
            _unequip.style.display = selected?.Category == ItemCategory.Gear ? DisplayStyle.Flex : DisplayStyle.None;
            _unequip.SetEnabled(isEquipped);
            _feedback.text = _session.Status;
            _feedback.EnableInClassList("save-error", _session.HasSaveError);
            _retry.style.display = _session.HasSaveError ? DisplayStyle.Flex : DisplayStyle.None;
            _equipped.text = _session.Inventory.TryGetEquipped(SkinInventorySession.SkinSlot, out var id)
                ? "NHÂN VẬT ĐANG DÙNG  ·  " + (_session.Find(id)?.DisplayName ?? id) : "NHÂN VẬT  ·  Skin mặc định";
            for (var i = 0; i < CatGearSlots.Count; i++)
            {
                var label = i == 0 ? "Đầu" : i == 1 ? "Eo" : "Tay";
                _equipped.text += "\n" + label + ": " + (_session.Inventory.TryGetEquipped(new EquipmentSlotId(EquipmentSlotType.Gear, i), out var gearId)
                    ? (_session.Find(gearId)?.DisplayName ?? gearId) : "Trống");
            }
        }

        private bool MatchesCategory(ItemDescriptor item)
        {
            if (_category == "Skin") { return item.Category == ItemCategory.Fur; }
            var index = _category == "Đầu" ? 0 : _category == "Eo" ? 1 : 2;
            return item.Category == ItemCategory.Gear && item.SupportsSlot(new EquipmentSlotId(EquipmentSlotType.Gear, index));
        }
    }
}
