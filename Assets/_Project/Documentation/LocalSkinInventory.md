# Inventory skin và gear local

## Chạy thử

1. Lưu scene đang làm của bạn trước khi chuyển scene.
2. Mở `Assets/_Project/Scenes/Prototype/LocalSkinInventory.unity` rồi bấm Play.
3. Chọn một skin: chỉ thay lựa chọn/ảnh xem trước, chưa đổi nhân vật.
4. Bấm **Trang bị**: nhân vật đổi material ngay, animation Idle tiếp tục, lựa chọn được lưu trên máy.
5. Dừng rồi Play lại: skin đã trang bị được khôi phục.

Có tìm theo tên, bộ lọc Tất cả / Đã sở hữu / Đang dùng, và nút mở/đóng tủ đồ. Nếu ghi lưu thất bại, giao diện thông báo rõ và có **Thử lưu lại**; diện mạo trong phiên chơi vẫn giữ nguyên.

Chọn nhóm **Skin / Đầu / Eo / Tay** để duyệt. Gear được trang bị bằng cùng nút **Trang bị**; chọn gear đang dùng rồi bấm **Tháo phụ kiện** để bỏ riêng món đó. Đổi skin không tháo gear. Đổi món cùng slot chỉ thay món trong slot đó.

## Phạm vi hiện tại

- 19 skin dùng chung mesh/UV/rig, không tạo lại nhân vật khi đổi skin.
- Hai slot material: thân (0), mắt (1). Không sửa shader/material asset chung trong runtime.
- Không thay Avatar, Animator Controller, trạng thái animation, bone, collider, hitbox, chỉ số.
- Có 8 gear cosmetic và 3 slot độc lập; chưa thêm multiplayer, cửa hàng, giá bán hoặc trứng. Slot Fur:0 không chiếm slot Gear.
- Scene riêng có nhân vật Idle để thử inventory, không phải scene gameplay đầy đủ.
- Bộ tạo scene không mở, lưu hay thay thế scene đang làm. Không sửa danh sách scene trong Build Settings.

## Dữ liệu và lưu local

`ScriptableObjects/Inventory/CatSkins` chứa 19 `CatSkinDefinition`, `CatSkinCatalog`, `CatSkinLibrary` và cấu hình UI/Idle.

`ScriptableObjects/Inventory/CatGear` chứa 8 `CatGearDefinition` và `CatGearLibrary`. Catalog hiện có được bổ sung gear, giữ toàn bộ ID skin cũ để không mất sở hữu khi đọc lại save. Bản sao catalog/prefab trước khi mở rộng nằm trong `Blender/Backups/BeforeGear_*`.

- `Gear:0` — Đầu: Hachimaki, Topknot.
- `Gear:1` — Eo: Black Mawashi, Indigo Mawashi, Crimson Mawashi, Kesho Mawashi, Yokozuna Rope.
- `Gear:2` — Tay phải: Gyoji Fan. Đây là quạt trang trí, không có attack/collider/stats riêng.

Mỗi slot tối đa một món. ID gear có tiền tố `gear_`, ví dụ `gear_hachimaki`, `gear_yokozuna_rope`. Không đổi thứ tự enum `EquipmentSlotType` vì save cũ dùng giá trị số.

ID ổn định: `fur_ginger`, `fur_grey`, `fur_calico`, `fur_russian_blue`, ... Không đổi ID sau khi đã phát hành save.

Ginger có UnlockType.Default. Các skin còn lại có UnlockType.Grant, chưa gán giá bán. Scene thử bật **Grant All Skins For Testing** để duyệt đủ 19 skin; đây không phải luật mở khóa của game chính.

Gear đều có UnlockType.Grant. Phòng thử bật **Grant All Gear For Testing** để thử đủ 8 món, nhưng không tự mặc gear khi profile chưa trang bị. Tắt flag không thu hồi vật phẩm đã cấp; dùng Profile Id mới để thử hồ sơ sạch.

Hồ sơ thử riêng: `Application.persistentDataPath/Profiles/local-skins-preview.json`. Dùng `ProfileService`, `InventoryService`, `JsonProfileStore` có sẵn của project, không dùng một hệ thống sở hữu khác. JSON có version; lần ghi kế tiếp giữ bản `.backup` theo cơ chế hiện có.

Khi tắt Grant All Skins For Testing, các skin đã cấp vẫn thuộc hồ sơ cũ. Để thử tiến trình mới, đặt **Profile Id khác**; không cần xóa hồ sơ hiện tại. Nếu đổi catalog trong game chính, giữ các item cũ vì InventoryService lọc ID không có trong catalog khi khôi phục.

## Nối vào gameplay hiện có

- Thêm `CatSkinController` lên instance của nhân vật đúng mesh `PF_Cat_Base`, gán Body Renderer.
- Thêm `CatGearController` lên cùng root Animator, gọi `Initialize(gearLibrary, profile.Inventory)`. Dùng skeleton hiện có; không tạo Animator hoặc nhân vật thứ hai.
- Từ composition root, gọi `Initialize(skinLibrary, profile.Inventory)` trên controller.
- Tạo `SkinInventorySession(profile, catalog)` từ **cùng profile** đang dùng trong gameplay, rồi `SkinInventoryView.Bind(session, thumbnailResolver)`.
- Để bao gồm gear: `new SkinInventorySession(profile, catalog, includeGear: true)`. Thumbnail resolver trả ảnh từ skin library hoặc gear library. API tháo món đang chọn là `session.UnequipSelected()`.
- Chọn qua `session.Select(id)`, trang bị qua `session.EquipSelected()`. Session kiểm tra sở hữu qua InventoryService và lưu profile sau thao tác trang bị.
- Nếu cấp item từ hệ thống khác, gọi `session.Save()` hoặc `profile.Save()` sau khi cấp. Dữ liệu ScriptableObject chỉ chứa cấu hình, không chứa ownership runtime.
- Khi UI đóng vĩnh viễn hoặc đổi profile, gọi `view.Unbind()` và `session.Dispose()`. Controller tự gỡ event khi disable và đọc lại skin khi enable.
- Không kéo cả `PF_LocalSkinInventoryRoom` vào scene gameplay: prefab này có camera, đèn và hồ sơ thử riêng. Chỉ dùng các component/dữ liệu cần thiết ở trên.

## Kiểm tra

Các fixture NUnit nằm tại `Scripts/Tests/EditMode/SkinInventorySessionTests.cs` và `Scripts/Tests/InventoryIntegration/CatSkinIntegrationTests.cs`.

Phần gear có thêm `CatGearSessionTests` và `CatGearIntegrationTests`: độc lập slot, thay thế/tháo, quyền sở hữu, save JSON, bind pose trên base thật, đổi skin giữ gear, lifecycle và bộ lọc UI. Chạy cùng menu kiểm tra ở dưới.

Có thể chạy bằng Unity Test Runner sau khi tự lưu scene. Menu **Tools > Paw Arena > Inventory > Run Local Inventory Checks** chạy trực tiếp các assertion đồng bộ của hai fixture trên main thread, có setup/teardown, không chuyển scene. Báo cáo: `Logs/LocalSkinInventoryChecks.xml`. Cách này không thay thế kiểm thử tương tác Play Mode.

Kiểm tra bao gồm chọn không trang bị, chặn skin chưa sở hữu, lưu/khôi phục, lỗi ghi lưu và thử lại, ID sai, gỡ subscription, đủ 19 cặp material đúng mesh, Avatar/bones không đổi và thời gian Animator không bị reset.
