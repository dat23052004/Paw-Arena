# Paw Arena — Cat Rig

Rig được làm từ `PawArena_BaseCat_Review.blend` đã duyệt. File base gốc được giữ nguyên.

## File sử dụng

- `PawArena_Cat_Rigged.blend`: model, texture đã đóng gói, bộ điều khiển và các tư thế kiểm tra.
- `SK_PawArena_Cat.fbx`: mesh và 30 xương xuất sang engine; không chứa xương điều khiển Blender.
- `Cat_BaseColor.png`: atlas màu 4096 × 4096, dùng chung cho hai material thân và mắt.
- `Cat_Humanoid.preset`: thiết lập import và mapping Humanoid đã kiểm tra trong Unity 6000.4.2f1.
- `Unity_Validation.json`: kết quả kiểm tra Avatar và biến dạng trong Unity.

## Điều khiển trong Blender

Chọn `RIG_Cat`, vào Pose Mode. Điều khiển các xương có tiền tố `CTRL_`; không chỉnh trực tiếp các xương xuất game.

- `CTRL_Root`: di chuyển/xoay cả nhân vật.
- `CTRL_Hips`: điều khiển hông; các vòng ở thân, cổ và đầu điều khiển từng vùng tương ứng.
- FK: xoay `CTRL_LeftUpperArm`, `CTRL_LeftLowerArm`… tương tự bên phải và chân.
- IK: trong Object Properties → Custom Properties của `RIG_Cat`, đặt `Arm_IK_Left`, `Arm_IK_Right`, `Leg_IK_Left` hoặc `Leg_IK_Right` thành **1**. Sau đó kéo điều khiển `…Hand_IK` hoặc `…Foot_IK`. Giá trị **0** dùng FK.
- Các điều khiển `…Pole` chọn hướng gập khuỷu tay/đầu gối. Không kéo mục tiêu vượt chiều dài tay/chân; IK không tự kéo giãn chi.
- `CTRL_LeftEar`, `CTRL_RightEar`, `CTRL_Tail_01` đến `CTRL_Tail_05`: chuyển động phụ của tai và đuôi.
- `HeadGearSocket`: điểm gắn mũ/tóc, đi theo xương đầu; không phải bộ điều khiển để tạo dáng.

Các nhóm xương trong Armature Data → Bone Collections giúp ẩn/hiện điều khiển thân, FK, IK và tai/đuôi. Nhóm `Deform / export` được ẩn mặc định để tránh chỉnh nhầm.

**Lưu ý chuyển IK/FK:** đây là chuyển ảnh hưởng giữa hai cách điều khiển, chưa có nút tự căn khớp hai tư thế. Muốn đổi giữa một animation đang chạy, cần căn tay/chân ở hai chế độ trước khi đổi để tránh nhảy tư thế.

## Các mốc kiểm tra

- Frame 1: T-pose gốc, toàn bộ switch IK = 0.
- Frame 20: thả tay bằng FK.
- Frame 40: gập khuỷu tay 90°.
- Frame 60: hạ hông, giữ bàn chân bằng IK.
- Frame 80: bước chân và nhấc bàn chân.
- Frame 100: quay đầu, tai và đuôi.
- Frame 120: kéo hai bàn tay bằng IK.

Action `Cat_Rig_Validation_Poses_NOT_GAME_ANIMATION` chỉ là các mốc kiểm tra, **không phải bộ idle/run/attack hoàn chỉnh**. Khi làm animation mới, tạo action riêng; nếu sửa điều khiển khi action kiểm tra còn hoạt động, thay đổi thời gian có thể đưa điều khiển về keyframe đã lưu.

## Dùng trong Unity

1. Import FBX, PNG và preset vào cùng một thư mục trong project đích.
2. Chọn FBX, dùng nút chọn Preset ở đầu Inspector và áp dụng `Cat_Humanoid`, sau đó Apply.
3. Kiểm tra tab Rig: Animation Type = Humanoid, Avatar Definition = Create From This Model. Preset gán rõ 21 khớp, bao gồm Chest. Không chỉ dựa vào Auto Mapping: lần kiểm tra tự động đầu tiên đã bỏ sót Chest.
4. Giữ hierarchy nếu cần truy cập `HeadGearSocket`, tai và đuôi. Chúng là xương phụ, không nằm trong 21 khớp Humanoid.
5. Material trong URP có thể cần chọn shader tương ứng và gán `Cat_BaseColor` vào Base Map. Rig không phụ thuộc shader.

Preset hiện bật Read/Write để phục vụ kiểm tra mesh; có thể tắt khi tích hợp game nếu không cần đọc mesh trên CPU. Chưa thay asset hay scene nào trong project gameplay chính.

## Đã kiểm tra và giới hạn

- T-pose của rig khớp hình học base đã duyệt; sai lệch tối đa sau vòng xuất/import FBX khoảng 0,00000043 m.
- Mỗi đỉnh có tối đa 4 trọng số xương, được chuẩn hóa. Dùng linear skinning, không dựa vào Preserve Volume riêng của Blender.
- Đã render kiểm tra bảy tư thế trong Blender, kiểm tra mục tiêu IK và phát hiện các cạnh bị kéo dài bất thường.
- FBX import lại có đủ 30 xương, không lẫn 37 xương điều khiển Blender, còn texture và UV.
- Unity 6000.4.2f1: `Avatar.isValid = true`, `Avatar.isHuman = true`; kiểm tra đủ 21 khớp và thử HumanPoseHandler + BakeMesh thành công.
- Đây là rig cơ thể, tai và đuôi; chưa có rig biểu cảm mặt/ngón tay, bộ animation gameplay hay cơ chế tự chuyển IK/FK không nhảy tư thế.
- Bản giữ bề mặt đã duyệt có **73.564 tam giác**, hai material và texture 4K. Chưa phải bản LOD/tối ưu nhiều nhân vật cho mobile; tối ưu mesh/texture là một bước riêng trước khi chốt ngân sách hiệu năng.

Kiểm tra Unity được thực hiện trong project riêng `Blender/Validation/UnityRigCheck`, theo hướng dẫn editor-only; không bổ sung code runtime vào Paw Arena.
