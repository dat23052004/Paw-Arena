# Bộ 19 skin mèo — chỉ đổi bề mặt

Mở `Blender/Characters/PawArena_Cat_Skins.blend`.

## Chọn skin

Trong thanh trên cùng của Blender, mở danh sách **Scene** và chọn một scene có số từ `01 | Ginger` đến `19 | Frankencat`. Mỗi scene hiển thị một skin với cùng ánh sáng. Bật Material Preview hoặc Rendered để xem texture; Solid không thể hiện đầy đủ màu.

19 skin: Ginger, Grey, Black, Tuxedo, White, Calico, Pumpkin, Mummy, Russian Blue, Siamese, Tiger, Ninja, Black Widow, Crystal, Gold, Hologram, Skeleton, Snow Leopard, Frankencat.

## Những gì được giữ nguyên

- Tất cả nhân vật dùng chung một mesh và UV `Cat_Atlas` của base đã duyệt.
- Không chỉnh vị trí đỉnh, topology, tỷ lệ, weight hoặc cấu trúc xương.
- Giữ nguyên 26 animation từ `PawArena_Cat_Abilities.blend`.
- Material được gán riêng ở cấp Object nên đổi skin không đổi material của các scene khác.
- File base/rig/animation nguồn và asset Unity không bị ghi đè.

## Xem animation

File mở ở **Rest Position** để duyệt skin trong T-pose. Chọn `RIG_Cat`, vào Armature Data Properties, đổi sang **Pose Position**. Sau đó chọn action có sẵn trong Action Editor để xem animation. Rig được dùng chung nên lựa chọn tư thế/action áp dụng cho các scene skin; đây không phải các rig riêng.

## Sửa màu

Chọn mesh `SK_Cat...`; hai slot material là Coat và Eyes. Mỗi skin có một ảnh BaseColor riêng, dùng chung cho hai slot. Texture được đóng gói trong file Blender và cũng xuất thành PNG ở thư mục này. Ginger giữ nguyên atlas 4096 gốc, các skin mới dùng atlas 2048.

Không sửa geometry khi đổi skin. Muốn tạo skin tiếp theo, sao chép material **và ảnh texture**, tránh vẽ vào ảnh đang được skin khác sử dụng. Lưu ảnh sau khi Texture Paint.

## Giới hạn theo yêu cầu giữ nguyên kết cấu

- Ninja, Mummy, Frankencat: khăn/áo/băng quấn/đường khâu là hình vẽ trên bề mặt, không phải trang phục có độ dày.
- Pumpkin: rãnh bí và chi tiết xanh là họa tiết, không thêm cuống/lá.
- Crystal: họa tiết các mảng pha lê và bề mặt bóng; không đổi mesh thành mặt cắt pha lê.
- Hologram: màu cầu vồng, vạch quét tĩnh và phát sáng nhẹ; không trong suốt hoặc có hiệu ứng động.
- Skeleton: hình vẽ xương trên thân mèo, không phải bộ xương rỗng.
- Gold dùng metallic; Crystal/Hologram/Black Widow có thiết lập emission. PNG chỉ chứa màu: khi đưa sang engine khác phải thiết lập lại các thuộc tính material theo `Skin_Manifest.json`.

## Kiểm tra

`Skin_Manifest.json` lưu danh mục, thông số material và dấu vân tay dữ liệu bảo vệ. Ảnh duyệt được render từ chính model trong file, ở thư mục `Blender/Previews/Characters/Skins`.
