# Paw Arena — Animation Pack 01

10 animation riêng trên base mèo và rig đã duyệt. Không sửa file base/rig gốc, không thêm code gameplay hoặc Animator Controller vào project chính.

## Mở và xem

- `PawArena_Cat_Animations_01.blend`: bản gốc có rig điều khiển, texture đóng gói và 10 Action chỉnh sửa được.
- `00_All_10_Animations.mp4`: video tổng hợp, mỗi clip được lặp lại để dễ duyệt. Các đoạn Jump Start, Land, Punch và Hit React vẫn là **one-shot**, không phải animation loop trong game.
- `00_Animation_ContactSheet.png`: ảnh các tư thế đại diện, không thay thế video chuyển động.
- Mỗi `AN_Cat_*.fbx` chỉ chứa skeleton và một clip, không nhân bản mesh. `SK_PawArena_Cat.fbx` là model tham chiếu T-pose chung.
- `PawArena_Cat_Animations01.unitypackage`: bản nhập sẵn cho Unity sau khi kiểm tra; vị trí asset là `Assets/_Project/Animations/Characters/Cat/Pack01`.

## Danh sách clip — 30 fps

1. **Cat_Idle** — frame 1–73, 2,4 giây, lặp. Thở nhẹ, chuyển trọng tâm, tai và đuôi chuyển động.
2. **Cat_Walk** — frame 1–31, 1 giây, lặp. Bước ngắn, tay đối nhịp chân.
3. **Cat_Run** — frame 1–21, khoảng 0,667 giây, lặp. Nhún thân, chân nhấc cao hơn và tay gập.
4. **Cat_Jump_Start** — frame 1–13, 0,4 giây. Hạ người lấy đà rồi mở thân, nối sang Jump Air.
5. **Cat_Jump_Air** — frame 1–25, 0,8 giây, lặp. Tư thế bay lên, chân thu nhẹ.
6. **Cat_Fall** — frame 1–25, 0,8 giây, lặp. Tay mở giữ thăng bằng, chuẩn bị tiếp đất.
7. **Cat_Land** — frame 1–15, khoảng 0,467 giây. Nhún hấp thụ lực rồi trở lại tư thế nghỉ.
8. **Cat_Punch_Left** — frame 1–19, 0,6 giây. Lấy đà, đấm trái và thu tay.
9. **Cat_Punch_Right** — frame 1–19, 0,6 giây. Phiên bản đối xứng đấm phải.
10. **Cat_Hit_React** — frame 1–17, khoảng 0,533 giây. Phản ứng trúng đòn phía trước và hồi phục.

Clip lặp có frame cuối trùng frame đầu để nối vòng. Một chu kỳ Idle là 72 khoảng frame, không phải 73 khoảng.

## Chỉnh trong Blender

Chọn `RIG_Cat`, dùng Dope Sheet → Action Editor và chọn một Action `Cat_...`. Đặt phạm vi timeline theo danh sách trên rồi phát. Cả 10 Action được giữ bằng Fake User và đánh dấu Asset; file mở ở Idle.

- Chân dùng IK để giữ điểm đặt chân. Tay dùng FK đã xử lý hướng xoắn vai; cổ tay nối hướng cẳng tay cho dáng bàn chân trước liền mạch.
- Các switch tay/chân giữ nguyên trong toàn bộ từng clip; không chuyển IK/FK giữa chừng.
- `CTRL_Root` cố định. Không kéo Root nếu muốn giữ tính chất in-place của bộ này.
- Tai và năm xương đuôi đã có chuyển động phụ. Mũ/tóc gắn vào `HeadGearSocket` sẽ theo đầu; không cần bộ animation riêng cho skin màu.
- Rig hiện chưa có biểu cảm mặt/ngón tay: bộ này không bao gồm chớp mắt, đổi nét mặt hay nắm ngón tay.
- Các key điều khiển được lấy mẫu mỗi frame để giữ đúng tư thế khi xuất; vẫn có thể chỉnh sửa trong Blender.

## Khi đưa vào game sau này

Đây là bộ **in-place**: animation diễn cơ thể, còn hệ thống di chuyển quyết định quãng đường và độ cao nhảy. Các clip bay/rơi không tự đưa nhân vật lên cao hoặc xuống thấp trong thế giới.

- Unity: Humanoid, cùng Avatar của model tham chiếu; giữ hierarchy để còn xương tai, đuôi và điểm gắn đồ đầu.
- Animation → Mask: dùng `Cat_AllBones.mask` đi kèm để giữ các đường chuyển động xương phụ; không chỉ nhập phần cơ thể Humanoid. Thiết lập này được lưu sẵn trong gói Unity. API cấu hình mask được mô tả trong [tài liệu Unity](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/modelimporterclipanimation).
- Dùng 5 clip loop: Idle, Walk, Run, Jump Air, Fall. Các clip còn lại phát một lần.
- Hướng dẫn điểm tiếp xúc của hai cú đấm: **frame 7 / 0,20 giây** tính từ đầu clip. Đây là mốc cho người tích hợp, chưa tạo hitbox hay Animation Event.
- Tốc độ Walk/Run cần được cân với tốc độ di chuyển thực tế khi nối gameplay; kiểm tra foot sliding ở bước đó. Chưa kiểm chứng với player controller/multiplayer.
- Cần blend ngắn giữa các trạng thái; Fall → Land và di chuyển → đấm chưa có Animator Controller tự nối. Jump Start kết thúc cùng tư thế bắt đầu Jump Air.
- Gói Unity giữ thiết lập clip và Avatar đã kiểm tra. Material có thể cần đổi sang URP/Lit khi đưa vào project URP.
- Mesh vẫn là bản đã duyệt, 73.564 tam giác và atlas 4K; không phải đợt tối ưu LOD/mobile.

## Kiểm tra

Xem `Blender_Validation.json`, `FBX_Animation_Validation.json` và `Unity_Animation_Validation.json` để biết các kiểm tra thực tế của bản xuất: phạm vi frame, điểm nối loop, Root cố định, quỹ đạo xương sau xuất/import và lấy mẫu clip Humanoid.

Video là bản render trực tiếp từ animation 3D, không phải ảnh minh họa AI hoặc mô phỏng chuyển động trên ảnh tĩnh.
