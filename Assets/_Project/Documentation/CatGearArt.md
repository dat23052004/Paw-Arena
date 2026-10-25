# Bộ gear sumo — nguồn 3D và tích hợp

Nguồn Blender: `Blender/Characters/PawArena_Cat_Gear.blend`.
Script dựng: `Blender/Characters/BuildCatGear.py`.
Script kiểm tra nguồn/biến dạng: `Blender/Characters/ValidateCatGear.py`.
Ảnh render thật: `Blender/Previews/Characters/Gear`.
Manifest: `Blender/Characters/Exports/Gear_Manifest.json`.

## Thiết kế

Tám món được dựng theo bảng tham khảo sumo và fit trên đúng `SK_Cat` đã duyệt. Bản này là hình học stylized, không phải mô phỏng vải: băng đầu có biểu tượng đỏ và nút buộc; đai có nếp vải/nút buộc; quạt có mặt đen, viền vàng và tua đỏ; tóc có búi và lọn; tạp dề đỏ có họa tiết mặt trời/sóng, tua vàng; dây Yokozuna gồm ba sợi xoắn liên tục và giấy trang trí.

Base, UV, weight, 67 xương authoring và các action gốc được giữ nguyên. Gear nằm trong các collection `GEAR | ...` riêng, dùng chung `RIG_Cat`. Trong file mở mặc định, Hachimaki + Crimson Mawashi + Gyoji Fan được bật để duyệt; tắt/bật các gear khác qua Outliner. Bỏ ẩn cả trong viewport và render khi chuyển món thủ công. Đổi `Armature > Skeleton > Pose Position` từ Rest sang Pose để xem action gốc chạy với gear.

Hachimaki/Topknot có weight cứng theo Head; quạt theo RightHand. Gear eo có weight theo thân/hông/đùi. Không thêm bone mới và không bật cloth physics. Fan vẫn theo tay trong animation đấm; chưa có bộ động tác cầm quạt riêng.

## Unity

FBX nguồn: `Models/Characters/Cat/Gear/SK_Gear_*.fbx`.
Mesh runtime: `Models/Characters/Cat/Gear/MSH_Gear_*.asset`.
Material: `Materials/Characters/Cat/Gear/M_Gear_*.mat`.
Texture palette BaseColor/MetallicSmoothness và thumbnail: `Textures/Characters/Cat/Gear`.

Mỗi gear có một submesh và một URP Lit material. UV gear độc lập, không sửa UV mèo. Khi trang bị, `CatGearController` tạo renderer cho món đó và ánh xạ tên bone sang skeleton mèo đang có. Không clone FBX kèm skeleton/Animator vào nhân vật. Không thay material của skin đang mặc.

Menu import một lần: **Tools > Paw Arena > Inventory > Import Sumo Gear**. Bộ tạo từ chối ghi đè library đã tồn tại; sau khi đã import, chỉnh các asset hiện có trực tiếp. Bộ dựng Blender có thể tái tạo các FBX/ảnh do script quản lý; nên sao lưu nếu đã sửa thủ công.

## Giới hạn cần biết

- Chưa có cloth simulation hoặc động tác quạt riêng; không tăng chỉ số gameplay.
- Kiểm tra biến dạng số học không chứng minh không xuyên mesh ở mọi tư thế. Cần duyệt thêm các pha chiến đấu khi chốt animation/gameplay.
- Đây là mesh chất lượng duyệt hình; chưa tạo LOD hay chốt ngân sách polygon cho thiết bị di động.
- Không thay collider/hitbox theo độ lớn của gear.

Hướng dẫn trang bị và lưu nằm trong `LocalSkinInventory.md`.

## Kết quả kiểm tra 25/10/2026

- Kết quả kiểm tra NUnit đồng bộ trên main thread của Unity Editor nằm trong `Logs/LocalSkinInventoryChecks.xml`. Bao gồm cả scene phòng thử đã lưu, camera mặt trước và khôi phục profile qua composition root. Đây không phải xác nhận tương tác Play Mode thủ công.
- Blender đã lấy 5 thời điểm mỗi action trong 26 action, trên cả 8 gear. Không có tọa độ không hữu hạn; trọng số hợp lệ, tối đa 4 ảnh hưởng/vertex. Sau khi sửa tua tạp dề, tỷ lệ giãn cạnh lớn nhất quan sát được khoảng 1.64, thay vì lỗi kéo dài trước đó.
- So sánh fingerprint xác nhận geometry, topology, UV, weight, rest rig và toàn bộ đường cong animation của base không đổi. Báo cáo: `Blender/Characters/Exports/Gear_Validation.json`.
- Ảnh `Pose_Cat_Idle`, `Pose_Cat_Run`, `Pose_Cat_Punch_Right` là render thật để duyệt dáng, không phải ảnh concept.
- Ảnh trong thư mục `Blender/Previews/Characters/Gear/Unity` được render trực tiếp bằng URP từ mesh/material đã import. Camera phòng inventory đã chỉnh sang mặt trước; base prefab và scene chưa lưu của người dùng không bị thay thế.
