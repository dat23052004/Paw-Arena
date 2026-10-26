# Bộ animation mèo hoàn chỉnh — 44 clip

Nguồn mới: `Blender/Characters/PawArena_Cat_Animations_Full.blend`.
Được tạo từ bản gear đã duyệt, giữ nguyên mesh, topology, UV, weight, rest rig và toàn bộ 26 action cũ. Không ghi đè các file Blender nguồn cũ.

## 18 clip bổ sung

Tất cả được author/bake ở 30 fps, Root đứng yên; di chuyển trong thế giới do gameplay điều khiển.

- Bắt/giữ: `Grab_Start` (0,60 s), `Grab_Hold` (1,20 s, loop), `Grab_Release` (0,50 s).
- Nhấc/mang/ném: `Lift` (1,00 s), `Carry_Idle` (1,33 s, loop), `Carry_Move` (1,07 s, loop), `Throw` (0,90 s).
- Vùng vẫy: `Struggle_Grab` (1,00 s, loop), `Struggle_Carried` (0,80 s, loop).
- Đứng dậy: `GetUp_FaceUp` (1,80 s), `GetUp_FaceDown` (1,60 s).
- Rơi khỏi sàn: `RingOut_Fall` (1,00 s, loop).
- Đầu/cuối trận: `Spawn` (1,00 s), `Ready` (1,20 s, loop), `Victory` (2,00 s), `Defeat` (1,60 s).
- Phản ứng: `Hit_Back` (0,60 s), `Stunned` (1,60 s, loop).

Thư viện hiện gồm 10 clip cơ bản + 16 skill có sẵn + 18 clip trên. Tên action/clip có tiền tố `Cat_`.

## Dùng trong Blender

Mở file Full, chọn `RIG_Cat`, chọn action trong Action Editor; bật Pose Position để xem biến dạng. Action có frame range riêng và được giữ bằng Fake User. Gear là các collection riêng, có thể bật để kiểm tra cùng animation. Bản render tổng hợp ẩn gear để thấy rõ động tác.

Các pha `Grab_Start → Grab_Hold → Lift → Carry_Idle → Throw`, `Grab_Hold → Grab_Release` và `Spawn → Ready` được kiểm tra tư thế nối nhau. Với loop, endpoint trùng đầu chu kỳ; không kéo dài thêm một frame dừng ở cuối.

Marker tham khảo, **không phải gameplay event**:

- `Grab_Start`: frame 15, khoảng 0,467 s tính từ frame 1 — vị trí tiếp xúc tay.
- `Throw`: frame 13, 0,400 s — thời điểm gợi ý thả mục tiêu.

## Unity

Clip mới ở `Assets/_Project/Animations/Characters/Cat/Completion/AN_Cat_*.fbx`.
Importer dùng Humanoid Avatar của base đã duyệt, mask giữ animation tai/đuôi, tắt compression và bake root rotation/position vào pose. Mỗi FBX chỉ có rig animation, không thêm một bản mesh mèo.

`PF_Cat_Base`, 26 FBX cũ, inventory, skin/gear và scene đang mở không bị thay đổi bởi bộ import. Menu chạy lại: **Tools > Paw Arena > Animations > Import Remaining 18 Clips**. Menu từ chối ghi đè nội dung FBX khác nguồn; chỉ cấu hình lại 18 asset mới này.

Bộ combat thử nghiệm đã được lưu trữ ngoài Assets tại `Archive/CombatPrototype_20261026` theo yêu cầu làm lại từng phần. Các clip Completion vẫn được giữ nguyên để tích hợp lại sau; scene `Main` hiện chỉ duyệt locomotion. Xem `CharacterFoundation.md`. Marker Blender không phải event Unity đã import. Chưa có ragdoll từng xương hoặc chọn hướng đứng dậy từ ragdoll; quạt chưa có động tác cầm riêng.

## Kiểm tra và preview

- `Blender/Characters/Exports/CatCompletion/Completion_Audit.json`: bất biến nguồn, mọi frame, loop, điểm nối và gear trong Blender.
- `FBX_Animation_Validation.json` cùng thư mục: xuất–nhập lại FBX, 30 bone xuất, khoảng frame và sai số pose.
- `Unity_Validation.json` cùng thư mục: kết quả mới nhất khi chạy bộ import; chỉ coi đạt khi `passed` là `true`.
- `Blender/Previews/Characters/Completion/00_Completion_ContactSheet.png`: 18 tư thế đại diện.
- `Blender/Previews/Characters/Completion/00_All_18_Completion.mp4`: render thật, 15 fps để duyệt nhanh; dữ liệu animation gốc vẫn là 30 fps. Video lặp lại cả clip one-shot để dễ xem, không có nghĩa clip đó được bật Loop Time.

Pipeline: `CompleteCatAnimations.py`, `ValidateCatAnimations.py --completion`, `EncodeCatCompletionPreviews.ps1`. Script tạo nguồn Full là công cụ tái tạo: sao lưu trước nếu đã sửa tay file đầu ra hoặc clip mới.
