# PAW ARENA — UNITY UI KIT

Bộ UI 2D **tự tạo**, lấy cảm hứng về bố cục/phong cách từ ảnh Roblox người dùng cung cấp (không trích xuất asset gốc). Bộ này dành cho **Unity uGUI** (Canvas / Image / Button / Text / Slider), ưu tiên Unity **2022.3 LTS** hoặc **Unity 6**.

## Tích hợp trong project Paw Arena này

- Bộ kit đã được đặt tại `Assets/Plugins/PawArenaUI`.
- Scene xem và kiểm tra toàn bộ UI nằm tại `Assets/_Project/Scenes/UI/PawArenaUIShowcase.unity`.
- Canvas tổng, HUD, EventSystem và sáu cửa sổ tái sử dụng nằm tại `Assets/_Project/Prefabs/UI/PawArenaUI`.
- Có thể sinh lại các asset trên bằng `Tools > Paw Arena UI > Generate Demo Scene`. Hãy lưu scene đang mở trước khi chạy.
- EventSystem và phím tắt demo đã được đổi sang Input System đang dùng trong project.

## Trong gói có gì?

- **105 PNG sprite** (RGBA, nền trong suốt): panels 9-slice, nút các trạng thái, tabs, rarity cards, badges, toggle, slider, icon, emote wheel.
- **47 file SVG nguồn** dành cho icon, có thể chỉnh vector, đổi màu hoặc xuất lại PNG.
- **7 màn hình và 7 prefab tái sử dụng** được tạo từ script Editor: HUD, Inventory (5 tabs), Settings, Stats, Daily Rewards, Update Log, Emote Wheel.
- **Script demo tương tác**: điều hướng mở/đóng, đổi tab, chọn item, thử equip/mua bằng coins, cài đặt cơ bản, slider, chọn emote, đổi bản cập nhật.
- **Importer tự động** đặt Texture Type = Sprite (2D and UI), Full Rect và 9-slice Border phù hợp.
- 3 ảnh minh họa: Preview_Inventory.png, Preview_Sprite_Catalog.jpg và Layout_1920x1080.png.
- Catalog item mẫu và bảng thông số import trong `sprite_import_settings.json`.

**Lưu ý:** Đây là UI template có khả năng tương tác trong Play Mode, **không phải** toàn bộ gameplay, dịch vụ shop thật, hệ thống lưu dữ liệu, multiplayer, networking hay animation nhân vật. Không kèm font chữ có bản quyền và không phải file `.unitypackage` — bạn nhập bằng cách chép thư mục `Assets/PawArenaUI` vào project Unity.

## 1. Cài vào Unity trong 5 bước

1. Tạo project Unity 2022.3 LTS / Unity 6 (2D Core, 3D Core hoặc URP đều được).
2. Kiểm tra Package Manager có **Unity UI (uGUI), `com.unity.ugui`**. Nếu thiếu, vào **Window → Package Manager → Unity Registry → Unity UI → Install**.
3. Giải nén file ZIP, copy nguyên thư mục **`Assets/PawArenaUI`** vào **`<YourUnityProject>/Assets/PawArenaUI`**. **Không copy riêng PNG trước** nếu muốn import tự động 9-slice.
4. Quay lại Unity, đợi compile/import. Chọn **Tools → Paw Arena UI → Generate Demo Scene**. Trong project này, cảnh demo được tạo ở `Assets/_Project/Scenes/UI/PawArenaUIShowcase.unity`; các prefab được sinh tại `Assets/_Project/Prefabs/UI/PawArenaUI`.
5. Nhấn **Play**. Thử click Inventory, Settings, Rewards, Stats, Event, Emotes; phím **I** mở/tắt Inventory, **G** mở/tắt Emotes, **Esc** đóng popup.

Nếu đang có scene riêng, **save scene hiện tại trước khi chọn Generate**, vì trình tạo mở một scene trống và tạo demo mới.

### Cấu hình thiết kế đã tích hợp

```
Canvas
  Render Mode             Screen Space - Overlay
Canvas Scaler
  UI Scale Mode           Scale With Screen Size
  Reference Resolution    1920 × 1080
  Screen Match Mode       Match Width Or Height
  Match                   0.5
Graphic Raycaster        Enabled
EventSystem              Standalone Input Module
```

Scene demo chủ yếu sử dụng RectTransform anchor với vị trí tương đối, cộng Canvas Scaler; mobile cần tinh chỉnh thêm như hướng dẫn phía dưới.

## 2. Cấu trúc thư mục

```
Assets/
└── PawArenaUI/
    ├── Sprites/
    │   ├── Panels/           Khung cửa sổ, content, side, header, quest, radial wheel
    │   ├── Buttons/          Green, Blue, Red, Gold, Purple, Gray (normal/hover/pressed)
    │   ├── Cards/            Item owned/selected/locked + 6 rarity + rewards
    │   ├── Badges/           Rarity badges, slider, toggles, money counter
    │   └── Icons/            47 icon RGBA 160×160
    ├── VectorSource/         SVG tương ứng 47 icon
    ├── Editor/
    │   ├── PawSpriteImporter.cs
    │   └── PawArenaDemoBuilder.cs
    ├── Scripts/
    │   ├── PawCatalog.cs
    │   └── PawUIController.cs
    ├── Demo/                 Tự sinh PawArenaDemo.unity + Prefabs/ sau khi chạy menu
    ├── Documentation/        Preview + hướng dẫn bổ sung
    └── sprite_import_settings.json
```

Các script ở `Editor` không được đưa vào runtime build. Các script ở `Scripts` là MonoBehaviour/catalog chạy trong Play Mode.

## 3. Hướng dẫn bố cục chi tiết

Bảng dưới là kích thước **tham khảo** trên Canvas 1920×1080; anchor quan trọng hơn giá trị pixel cố định.

| Khu vực | Anchor | Kích thước mẫu | Tác dụng |
|---|---|---:|---|
| `HUD_CoinBG` | Top Left | 255×78 | Hiện coin và icon |
| `HUD_LeftNav` | Top Left | 235×220 | Shop, Inventory, Emotes, Rewards |
| `HUD_Quests` | Right, Y=0.70 | 355×230 | Daily quests và timer |
| `HUD_CandyBG` | Bottom Left | 195×65 | Event currency |
| `ModalDimmer` | Stretch toàn Canvas | Full screen | Phủ xanh đen alpha 0.70 |
| `Window_Inventory` | Center | 1530×850 | Inventory chính |
| `InventoryList` | Center trong Inventory | 960×700 | Danh sách cards bên trái |
| `InventoryDetail` | Center trong Inventory | 475×700 | Item details bên phải |
| `Window_Settings` | Center | 700×825 | Settings + sliders |
| `Window_Stats` | Center | 1000×630 | Avatar + 4 KPI |
| `Window_Rewards` | Center | 1530×710 | 5 thẻ Daily Reward |
| `Window_Updates` | Center | 1380×825 | Sidebar + banner + cards |
| `Window_Emotes` | Center | 680×680 | 8-slot radial wheel |

### Inventory master-detail

- Header: `Inventory` và nút Close. Thanh tab gồm **Furs / Abilities / Gear / Effects / Emotes**.
- Left: `InventoryList` chia 2 hàng **OWNED** và **UNOWNED**, mỗi hàng có tối đa 5 card trong dữ liệu demo. Card chứa icon, tên, lock/check, giá.
- Right: `InventoryDetail` gồm `Inventory_Icon`, `Inventory_Name`, `Inventory_Rarity`, `Inventory_Description`, `Btn_Equip`.
- Trạng thái hiển thị: **selected** (sáng card), **equipped** (check icon), **locked** (lock + coin), **owned**.
- Để thêm item, chỉnh `PawCatalog.CreateItems()` và tăng card trong `PawArenaDemoBuilder.Inventory()`. Với dữ liệu nhiều hơn 5 món/hàng, nên chuyển sang `ScrollRect + GridLayoutGroup` (cell 160×201, spacing 14–18).

### Settings

Cứ mỗi hàng một `content_panel`, trượt thanh `Slider` cho 0–100%, và toggle bằng `Button`. Phần lưu cấu hình nên tích hợp `PlayerPrefs` (offline) hoặc profile backend (online). Demo **chưa tự lưu** các thiết lập.

### Daily Rewards

Năm card ngang: Day 1–4 đã nhận, Day 5 nổi bật. Nút ngày 5 hiện thời gian minh họa; cần nối server time, check-in streak, kiểm tra thời gian thực và claim rewards vào hệ thống của game.

### Emote Wheel

`emote_wheel_base.png` là hình tròn radial, **không phải 9-slice**. Mỗi button nằm trên vòng tròn theo góc 45°, hiện 3 emote mẫu. Đổi UI raycast sang drag-release nếu muốn kiểu wheel chọn bằng hướng chuột/analog stick.

## 4. Sử dụng asset 9-slice đúng cách

Đừng kéo giãn một sprite thông thường vì góc bo và outline sẽ bị méo.

**Unity Inspector:** `Image > Source Image = window_panel` → `Image Type = Sliced`. Importer đã đặt Border cho các sprite chuẩn. Ví dụ:

| Sprite | Border (L/B/R/T) |
|---|---|
| `window_panel` | 34 / 34 / 34 / 34 |
| `content_panel` | 26 / 26 / 26 / 26 |
| `tabbar_panel` | 18 / 18 / 18 / 18 |
| `button_green_normal` | 22 / 22 / 22 / 22 |
| `close_red` | 23 / 23 / 23 / 23 |
| `item_owned` | 22 / 22 / 22 / 22 |
| `reward_current` | 25 / 25 / 25 / 25 |
| `badge_epic` | 15 / 15 / 15 / 15 |
| `emote_wheel_base` | **0** — dùng Image Type Simple |

Các nút màu có bộ `normal`, `hover`, `pressed`; demo sử dụng `Button.Transition = Sprite Swap`. Dữ liệu border đầy đủ trong `sprite_import_settings.json`.

## 5. Palette / Typography

| Token | Màu gợi ý |
|---|---|
| `Primary` | `#2464D8` |
| `Dark Panel` | `#112856` |
| `Success` | `#4BC748` |
| `Danger` | `#E94B53` |
| `Highlight` | `#FFCA42` |
| `Rare` | `#27B3EB` |
| `Epic` | `#A447E5` |
| `Legendary` | `#FFB638` |
| `Overlay` | `#020F22` alpha ~70% |

Phông phù hợp phong cách: **Fredoka Bold**, **Baloo 2 Bold**, **Nunito ExtraBold** (kiểm tra giấy phép riêng). Sample scene dùng Unity legacy Text và font fallback sẵn có — không đóng gói font ngoài. Muốn chữ đúng phong cách hơn: cài TMP, import phông có license, chuyển các Text sang TextMeshProUGUI và thêm Underlay/Outline.

## 6. Responsive và mobile

Desktop: Canvas Scaler 1920×1080, Match=0.5. Với mobile 16:9 thì có thể giữ bố cục. Với điện thoại portrait, nên tạo layout khác:

- Inventory có thể chuyển từ 2 cột sang **1 cột**, details mở dưới dạng bottom sheet riêng.
- HUD trái giảm từ 2×2 nút xuống thanh ngang dưới hoặc nút Menu mở rộng.
- Quests thu gọn thành pill tóm tắt thay vì luôn chiếm 355×230.
- Nút bấm có vùng chạm ít nhất khoảng 44–48 dp tương đương (tùy scale).
- Tôn trọng Safe Area cho tai thỏ, viền màn hình (Screen.safeArea), dùng `RectTransform` và test trong Device Simulator.
- Card list dài nên chuyển sang ScrollRect với GridLayoutGroup, icon/text không dùng quá nhiều hardcoded pixels.

## 7. Tích hợp inventory thật

Demo dùng `PawCatalog` để mô tả item, `PawUIController` để đổi tabs và tương tác. Dữ liệu item `owned` là local trong RAM và reset khi chạy lại. Khi chuyển sang game thật:

1. Thay `PawCatalog.CreateItems()` bằng dữ liệu ScriptableObject hoặc JSON/data backend.
2. Tạo `InventoryService` cung cấp owned/equipped/currency từ save game.
3. Tách logic `EquipOrBuy()` thành validation + callback thành công/thất bại.
4. Cập nhật `InventoryDetail`, `ItemCard` qua observer/events (không dùng `Find()` cho runtime production).
5. Bổ sung âm thanh, animation DOTween/Animator và trạng thái loading/error/success.

Đoạn mã demo cố tình dễ hiểu và tự chứa, **không phải production architecture** cho một game live-service.

## 8. Cần làm gì để hoàn thiện như game tham khảo?

- Thay icon minh họa bằng icon/thumbnail/portrait theo fur/skin trong game của bạn.
- Xây thêm prefab thực từ demo scene (Right click object → Prefab trong Project).
- Dữ liệu unlock/trang bị/lưu profile; đa ngôn ngữ; accessibility; âm thanh; animation.
- Update Log thêm ScrollRect và data danh sách bản cập nhật thực.
- Stats nối API/game state của người chơi.
- Quy trình bán vật phẩm thật, server-authoritative với multiplayer.
- Daily streak dựa server timestamp thay vì text timer giả lập.

## 9. Xử lý lỗi thường gặp

**Màu/khung bị kéo méo:** Chọn Image Type `Sliced`; xem Border trong Sprite Editor. Nếu PNG import trước script, Right click `PawArenaUI/Sprites` → Reimport.

**Menu Tools không xuất hiện:** Kiểm tra Console có lỗi C# khác; Unity UI uGUI đã cài; source `Editor` nằm đúng thư mục.

**Font rỗng hoặc khác mockup:** Unity trên từng OS có fallback font khác nhau; thay bằng TMP + font được phép dùng.

**Không thấy nút / không click được:** Kiểm tra scene do generator tạo có EventSystem, GraphicRaycaster; bật Play mode. Overlay được tắt khi không có popup.

**Không thấy ảnh 3D phía sau:** Scene demo chỉ hiển thị backdrop 2D, không có gameplay. Khi tích hợp game thật, đưa Canvas vào scene gameplay, xóa phần `DemoBackground` và 2 Decoration.

**Unity báo thiếu class Script/namespace:** Các file C# phải giữ nguyên tên. Script có namespace `PawArenaUI`. Nếu đã dùng asmdef tùy chỉnh, cần reference Unity UI module.

## 10. Quyền sử dụng và ghi chú

Tất cả sprite trong bộ này được vẽ mới bằng hình học/vector phục vụ yêu cầu này. Có thể dùng/điều chỉnh trong dự án của bạn theo quyền sử dụng nội dung do dịch vụ cung cấp. **Không khẳng định là asset chính thức, không liên kết với Roblox hoặc game từ ảnh.** Không có logo game, font có bản quyền hay assets trích trực tiếp từ ảnh chụp.
