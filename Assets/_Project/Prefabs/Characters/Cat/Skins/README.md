# 19 skin mèo trong Unity

Các prefab `PF_Cat_<Skin>.prefab` kế thừa `PF_Cat_Base`, không phải 19 model/rig riêng.

## Xem thử

Trong Project, mở `Assets/_Project/Prefabs/Characters/Cat/Skins`. Chọn prefab để xem Preview, mở Prefab Mode hoặc kéo vào scene khi cần. Việc import không tự thay nhân vật trong scene đang mở.

## Đổi skin thì thay gì?

Chỉ thay hai material trên `SkinnedMeshRenderer`:

- Element 0 / Coat: màu lông, mảng trắng, sọc/đốm, họa tiết trang phục, màu tai/mũi/đệm chân.
- Element 1 / Eyes: màu và bề mặt mắt.

Hai material của cùng skin dùng chung một texture BaseColor. Mỗi material có thông số độ bóng, kim loại, lớp phủ bóng và phát sáng phù hợp với skin. Material nằm ở `Assets/_Project/Materials/Characters/Cat/Skins`; texture nằm ở `Assets/_Project/Textures/Characters/Cat/Skins`.

Không đổi mesh, tọa độ UV, hình dáng/tỷ lệ, weight, xương, Avatar, điểm gắn đồ đầu, Animator Controller hoặc clip animation. Không thay đổi collider/hitbox/chỉ số gameplay. Prefab hình ảnh gốc hiện chưa có collider hoặc logic gameplay.

## Khi nối inventory sau này

Giữ nguyên nhân vật đang tồn tại và chỉ gán cặp material đã chọn vào hai slot của renderer. Không cần hủy/tạo lại nhân vật hoặc reset Animator. Các prefab skin là bản chuẩn để xem, đặt nhân vật và tra cặp material; không bắt buộc spawn prefab mới mỗi lần đổi skin.

Chưa thêm code inventory, quyền sở hữu skin, lưu lựa chọn hoặc đồng bộ multiplayer trong đợt import này.

## Hiển thị bề mặt

- Tất cả dùng URP Complex Lit, opaque, có shadow. Không có shader/package mới.
- Roughness từ Blender được chuyển thành Smoothness = 1 - Roughness; metallic và clear coat được đặt theo manifest. Mắt dùng Smoothness 0.75.
- Gold có Metallic 0.92. Vẻ kim loại phụ thuộc ánh sáng và reflection của scene.
- Crystal, Hologram, Black Widow có emission. Emission không tự tạo đèn hay bật Bloom; không thay post-processing của scene.
- Crystal/Hologram là bề mặt cách điệu tĩnh, không phải vật thể trong suốt hoặc shader chuyển động.
- Băng quấn/khăn/đường khâu là texture, không có mesh trang phục bổ sung.
- Ánh sáng và tone mapping khác nhau có thể khiến Blender và Unity không giống hệt từng pixel.

## Kiểm tra

`Blender/Validation/UnityCatSkinsImport.json` lưu kết quả kiểm tra prefab variant, material, dùng chung mesh/Avatar, phát thử Run trên từng skin và xác nhận base/animation/scene đang mở không bị sửa.
