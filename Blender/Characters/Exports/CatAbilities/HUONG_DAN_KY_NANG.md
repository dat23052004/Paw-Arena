# Paw Arena — 16 clip kỹ năng

Phạm vi đợt này: chỉ bổ sung 16 clip chuyển động cơ thể cho 11 kỹ năng trong mục 5 của GDD v1.1 người dùng gửi. Không bàn giao thêm các clip túm, nhấc, ném, thắng/thua hoặc đối kháng khác. Các tư thế thi triển là bản diễn hoạt mới dựa trên mô tả GDD, không phải sao chép animation của Cat Battles.

## File sử dụng

- `PawArena_Cat_Abilities.blend`: base và rig đã duyệt; 26 Action gồm 10 clip đầu giữ nguyên và 16 clip kỹ năng mới. Texture được đóng gói trong file.
- `Exports/`: 16 FBX animation riêng, model tham chiếu T-pose, texture, thiết lập Humanoid và báo cáo kiểm tra. Không nhân bản mesh trong từng FBX animation.
- `PawArena_Cat_Abilities.unitypackage`: các asset đã cấu hình trong thư mục riêng `Assets/_Project/Animations/Characters/Cat/Abilities`. Không có gameplay hoặc Animator Controller.
- `00_All_16_Abilities.mp4`: video render từ Blender, mỗi clip lặp lại để duyệt. Chỉ Pounce Air và Anchor Hold là loop thực sự.
- File gốc của base, rig và Animation Pack 01 không bị ghi đè. Bản nháp `Animations_Complete`/`Animations02` trước khi đổi phạm vi không thuộc bộ bàn giao này.

## Danh sách — 30 fps

Tên Action có tiền tố `Cat_`; tên FBX có tiền tố `AN_Cat_`.

1. `Ability_Dash`: frame 1–19, 0,60 giây. Hạ người, nghiêng thân và đưa tay ra sau để lướt.
2. `Ability_NineLives_FaceUp`: 1–37, 1,20 giây. Hồi phục nhanh từ nằm ngửa, chống tay rồi đứng lên.
3. `Ability_NineLives_FaceDown`: 1–37, 1,20 giây. Hồi phục nhanh từ nằm sấp.
4. `Ability_Hiss`: 1–19, 0,60 giây. Lấy đà ngắn, hất thân/đầu ra trước, mở hai tay, ép tai.
5. `Ability_Pounce_Start`: 1–13, 0,40 giây. Thu người lấy đà, chuyển sang tư thế vồ.
6. `Ability_Pounce_Air`: 1–19, 0,60 giây, loop. Hai tay vươn trước, chân thu, tai/đuôi chuyển động nhẹ.
7. `Ability_Pounce_Land`: 1–16, 0,50 giây. Tiếp đất và hấp thụ lực, trở lại tư thế nghỉ.
8. `Ability_Anchor_Start`: 1–19, 0,60 giây. Mở chân và hạ trọng tâm.
9. `Ability_Anchor_Hold`: 1–49, 1,60 giây, loop. Giữ thế trụ, thở nhẹ và chuyển động đuôi.
10. `Ability_Anchor_End`: 1–19, 0,60 giây. Thoát thế trụ.
11. `Ability_Stomp`: 1–25, 0,80 giây. Nhấc chân trái, giậm xuống và hồi tư thế.
12. `Ability_Catnip`: 1–25, 0,80 giây. Nhún người rồi bật tư thế tỉnh táo, sẵn sàng tăng tốc. Không có động tác ăn vật phẩm.
13. `Ability_FluffUp`: 1–28, 0,90 giây. Thu hai tay rồi mở vai, ưỡn ngực. Không đổi kích thước hoặc hình dạng mesh.
14. `Ability_Hairball`: 1–25, 0,80 giây. Ngẩng đầu lấy đà rồi cúi thân/đầu phun về trước. Không tạo projectile hoặc vùng trơn trong file.
15. `Ability_SharkAttack`: 1–34, 1,10 giây. Đưa tay lên rồi ra hiệu triệu hồi về trước. Không có model/animation cá mập.
16. `Ability_WindShurikens`: 1–34, 1,10 giây. Hai lượt vung tay trái/phải để tung hai quạt phi tiêu gió. Không có phi tiêu hoặc VFX.

Frame đầu là 1; thời lượng bằng `(frame cuối − frame đầu) / 30`. Frame cuối của clip loop trùng tư thế frame đầu.

## Mốc tham khảo cho tích hợp sau

Các mốc được ghi trong Action Blender, chưa phải Animation Event hoặc logic gây tác dụng:

- Dash: frame 5 / 0,133 giây.
- Hiss: frame 8 / 0,233 giây.
- Pounce Start: cất người frame 10 / 0,300 giây; Pounce Land: tiếp đất frame 4 / 0,100 giây.
- Stomp: giậm frame 14 / 0,433 giây.
- Catnip: frame 12 / 0,367 giây; Fluff Up: frame 13 / 0,400 giây.
- Hairball: frame 11 / 0,333 giây.
- Shark Attack: frame 17 / 0,533 giây.
- Wind Shurikens: lượt một frame 10 / 0,300 giây; lượt hai frame 21 / 0,667 giây.

Cooldown và “Lasts” trong GDD không được dùng làm độ dài clip. Khi tích hợp cần quyết định tác dụng kỹ năng xảy ra ngay hay theo mốc động tác, rồi chỉnh tốc độ animation nếu cần. Hiss/Hairball có “Lasts 0,1s” không có nghĩa toàn bộ lấy đà–thi triển–hồi tư thế phải dài 0,1s.

## Chỉnh và phát trong Blender

Chọn `RIG_Cat` → Dope Sheet → Action Editor → Action `Cat_Ability_...`. Đặt timeline theo khoảng frame ở trên. File mở sẵn ở Fluff Up; các Action có Fake User và được đánh dấu Asset.

Rig điều khiển vẫn chỉnh sửa được. Key được lấy mẫu mỗi frame, chuyển động tay tránh xoắn vai; những pha xoay cả thân được giải tư thế rồi lưu các xương điều khiển. Bản FBX được bake lên skeleton 30 xương chung, không xuất các control.

## Giới hạn có chủ ý

- Chỉ animation cơ thể. Rig hiện chưa có điều khiển miệng/mặt, nên Hiss và Hairball chưa há miệng; chưa có âm thanh, particle, material effect, cá mập hoặc phi tiêu.
- Không thêm fur/gear, không sửa mesh, cân nặng skinning hay cấu trúc rig đã duyệt.
- Root đứng yên. Dash/Pounce không tự dịch chuyển nhân vật; gameplay quyết định vận tốc, quãng đường, mục tiêu và quỹ đạo. Nine Lives có chuyển động Hips và xoay thân để đứng lên nhưng không di chuyển Root.
- Không tạo hitbox, lực đẩy, vùng trơn, stamina, cooldown, ragdoll, multiplayer, input hoặc Animator Controller.
- Pounce Start → Air → Land và Anchor Start → Hold → End đã kiểm tra tư thế tại điểm nối. Thời gian giữ pha Air/Hold do gameplay quyết định.
- Chọn Nine Lives FaceUp/FaceDown theo tư thế kết thúc ragdoll; cần căn Root và blend từ tư thế vật lý khi tích hợp sau. Bộ này chưa kiểm chứng với ragdoll thực tế.
- Unity dùng Humanoid và mask xương phụ đi kèm để giữ tai/đuôi. Không bật Optimize Game Objects nếu chưa cấu hình expose các xương phụ và HeadGearSocket.
- Gói Unity giữ một model tham chiếu riêng để tự chứa Avatar; có thể dùng chung Avatar chuẩn với Pack 01 khi tích hợp. Material có thể cần chuyển sang shader URP của game.
- Base vẫn là bản đã duyệt (73.564 tam giác, texture 4K); chưa phải đợt tối ưu mobile/LOD.

## Kiểm tra

Các báo cáo đi kèm ghi kết quả thực tế: `Blender_Validation.json` (Root, loop, biến dạng), `FBX_Animation_Validation.json` (xuất/nhập lại và so quỹ đạo xương), `Unity_Animation_Validation.json` (Humanoid, thời lượng, tai/đuôi, mesh sau skinning và loop), `Ability_Final_Audit.json` (26 Action, bảo toàn 10 clip cũ, base/rig, điểm thấp nhất mỗi frame).
