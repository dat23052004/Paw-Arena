# Cat — base và 26 animation

Bộ asset này gồm base mèo đã duyệt, 10 clip cơ bản và 16 clip kỹ năng cho 11 chiêu trong GDD v1.1. Không gồm 18 clip đối kháng đang để sau.

## Vị trí

- Prefab hình ảnh: `Assets/_Project/Prefabs/Characters/Cat/PF_Cat_Base.prefab`.
- Model và Avatar Humanoid chung: `Assets/_Project/Models/Characters/Cat/SK_PawArena_Cat.fbx`.
- 10 animation cơ bản: thư mục `Base` bên cạnh tài liệu này.
- 16 animation kỹ năng: thư mục `Abilities` bên cạnh tài liệu này.
- Material URP: `Assets/_Project/Materials/Characters/Cat`.
- Atlas màu 4K: `Assets/_Project/Textures/Characters/Cat/Cat_BaseColor.png`.
- `Cat_AllBones.mask`: giữ các kênh tai, đuôi và điểm gắn đồ đầu khi nhập Humanoid.

## Dùng trong Unity

Kéo `PF_Cat_Base` vào scene khi cần sử dụng. Prefab có Animator và Avatar hợp lệ, nhưng chưa gán Animator Controller, Rigidbody, collider hoặc script gameplay. Không thay thế player hay nhân vật trong scene hiện tại.

Mở từng FBX animation trong Project để lấy clip con `Cat_...`. Có thể xem ở Inspector → Animation Preview; nếu Unity yêu cầu model xem trước, kéo prefab mèo vào vùng Preview.

Tất cả clip dùng chung Avatar từ model tham chiếu, chạy 30 fps và giữ Root tại chỗ. Chuyển động trong thế giới, tốc độ chạy/lướt, độ cao nhảy và hiệu ứng được nối khi làm gameplay.

Loop: Idle, Walk, Run, Jump Air, Fall, Ability Pounce Air và Ability Anchor Hold. Các clip còn lại phát một lần. Chỉ bật loop cho các pha cần giữ, không lặp toàn bộ động tác thi triển.

Pounce: Start → Air → Land. Anchor: Start → Hold → End. Nine Lives chọn FaceUp hoặc FaceDown theo tư thế nằm. Các clip kỹ năng không chứa VFX hoặc điều khiển miệng.

Rig giữ nguyên tai, năm xương đuôi và `HeadGearSocket`. Không bật Optimize Game Objects khi chưa cấu hình expose các xương này. Bounds trên prefab bao phủ các tư thế đã kiểm tra để giảm việc nhân vật biến mất khi đứng dậy/nhảy.

## Nhập và kiểm tra

Menu `Tools → Paw Arena → Import Cat Base and 26 Animations` cấu hình asset mới và kiểm tra chuyển động trong preview scene riêng. Công cụ không lưu hoặc đổi scene đang mở, và không ghi đè prefab đã tồn tại. Lần nhập ban đầu có thể được yêu cầu bằng marker một lần trong Library; marker được đánh dấu hoàn tất và không chạy lại khi mở project.

Kết quả lần nhập được ghi vào `Blender/Validation/MainProjectCatImport.json` ở gốc project. File Blender gốc và các gói animation đã bàn giao vẫn được giữ nguyên.
