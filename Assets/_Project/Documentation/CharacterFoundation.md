# Bước 1 — một scene, một player, duyệt animation di chuyển

## Phạm vi hiện tại

Mở `Assets/_Project/Scenes/Main.unity`, bấm Play. Đây là **scene gameplay duy nhất trong Build Settings**. Scene có player, sàn thử, camera và một `CombatTarget` đứng yên để thử toàn bộ combat player. HUD gọn hiển thị máu, trạng thái và hồi chiêu. Không đọc/ghi profile inventory: player Ginger, target Grey, không gear để dễ thấy hình thể và pose.

Scene inventory và SampleScene cũ được giữ làm tài liệu/authoring, không có trong luồng runtime mới. Chưa di chuyển UI inventory vào Main — làm sau khi player được duyệt.

## Dùng thử

- WASD: tự chạy bằng Run (6 đơn vị/giây), không cần Shift; Space: nhảy. Dừng input thì phanh về Idle.
- Lobby: giữ chuột phải và kéo ngang để đổi hướng quay của player; camera luôn bám sau lưng kể cả đang xoay. Kéo dọc thay góc cao/thấp; thả chuột giữ hướng đã chọn và trả con trỏ.
- Party Match (`GameFlowState.Playing`): di chuột tự xoay hướng camera mà không cần giữ nút; player có quán tính xoay theo hướng camera và camera luôn bám sau hướng thực của player. Click trái chỉ phát đòn theo hướng đã tự ngắm; chuột phải vẫn là Grab. Cuộn vẫn zoom gần/xa (2–18 đơn vị).
- Trong scene test, nhấn `L` để chuyển luồng game Lobby ↔ Playing (Party Match); player và camera cùng nhận trạng thái từ `GameFlowController`.
- Đầu và thân tự cúi/ngẩng theo góc cao/thấp của camera; tay vẫn rủ theo hướng xuống. Không dùng R/F. Góc camera mặc định 16 độ là tư thế trung tính; góc xoay giới hạn -35 đến 80 độ, độ cúi/ngẩng nhân vật giới hạn ±30 độ.
- Camera tự rút gần khi bị sàn/vật cản chắn để tránh xuyên mặt sàn (có thể gần hơn giới hạn zoom người dùng). W tiến, S lùi, A/D đi ngang theo hướng nhìn; không tự quay mặt về camera khi đi lùi hoặc đi ngang. Player xoay ngang theo hướng chuột ngay cả khi đứng yên. Bot vẫn quay theo hướng di chuyển, không bị camera người chơi điều khiển. Hiện dùng clip Run có sẵn, chưa thêm bộ clip strafe/backpedal riêng.
- Chỉnh giới hạn camera tại MainCamera → CharacterReviewCamera: Minimum/Maximum Distance (mặc định 2/18) và Minimum/Maximum Pitch (mặc định -35/80). Giá trị được kiểm tra để min không vượt max, khoảng cách luôn dương và góc không lật qua đỉnh.
- Phím 1–3: xem Idle, Run và Jump Air gốc **tại chỗ**, tạm khóa di chuyển và lớp chuyển động mềm. Runtime chỉ còn ba clip này; nhảy lên và rơi xuống cùng dùng một pose trên không để hành động không bị chia thành nhiều pha cứng.
- Trong xem clip: Space tạm dừng/tiếp tục. Ba clip locomotion tối thiểu đều lặp lại.
- 0: trở lại điều khiển thực. Backspace: về điểm bắt đầu, xóa vận tốc và input còn giữ.
- Combat: chuột trái đấm/ném, giữ chuột phải để bắt, Left Shift để nhấc, Q dùng kỹ năng, Z/X đổi kỹ năng. E không dùng. Trong Lobby, RMB đồng thời orbit camera; trong Party Match, mouse-look luôn hoạt động nên RMB chỉ cần cho Grab. Chi tiết ở `Documentation/PlayerCombat.md`.

Các clip gốc không chỉnh sửa. `CatSecondaryMotion` là lớp hình ảnh độc lập với combat: thân chỉ nghiêng trước/sau, không nghiêng ngang hoặc xoắn theo quán tính. Khi đi ngang, lớp này cũng giảm lắc ngang của lưng có sẵn trong clip Run; tay chân vẫn chuyển động. Tay rủ xuống với lò xo giảm chấn thấp để có độ văng và trễ vui nhộn, đầu bù nghiêng, đuôi chuyển động trễ. Không gắn Rigidbody vào từng xương; không làm lệch collider hoặc bàn chân. Trên không tay vẫn được thả lỏng nhẹ nhưng không phá silhouette của Jump Air. Reset/teleport xóa đà hình ảnh, xem clip riêng tắt lớp này để so sánh. Có thể chỉnh Strength, Arm Hang và Run Lean trên prefab.

Hướng chuột là hướng đích; player tăng/giảm vận tốc góc thay vì bám cứng nên có quán tính khi bắt đầu, dừng và đảo chiều. Khi kéo nhanh vượt Turn Speed, player chuyển sang pha đuổi hướng mới bằng đường quay ngắn nhất với trần Catch Up Turn Speed; camera vẫn bám sau hướng thực tế của player. WASD theo hướng camera đang hiển thị, không nhảy ngay sang hướng đích. Chỉnh độ nặng tại `ScriptableObjects/Characters/CatLocomotion.asset` bằng Turn Acceleration, Turn Braking và Turn Smooth Time. Reset, mất focus và xem clip xóa đà quay. Tốc độ cuộn zoom chỉnh trên MainCamera → Character Review Camera → Zoom Sensitivity (mặc định 0.004).

Gia tốc và tốc độ quay lấy mẫu ở FixedUpdate sau motor; lò xo hình ảnh cập nhật ở LateUpdate. Không lấy đạo hàm vận tốc vật lý theo render delta vì sẽ làm mất quán tính ở FPS cao. Khôi phục pose gốc trước Animator mỗi frame để tránh cộng dồn độ xoay.

## Prefab và trách nhiệm

- `Prefabs/Player/PF_Player.prefab`: input, locomotion, combat player, effect pool, procedural combat pose và HUD.
- `Prefabs/Bots/PF_Bot.prefab`: `CatBotController` và `CatCombatant`, không có input người chơi. Mặc định đứng yên làm target; chưa có AI tấn công.
- Cả hai dùng `CatLocomotion` cho Rigidbody, gia tốc, quay, probe sàn và nhảy; `CatAnimationController` chọn clip. Các script này không cần CombatWorld hoặc manager trận.
- `Prefabs/Characters/Cat/PF_Cat_Base.prefab`: model/rig nguồn được tái sử dụng, không sửa.
- `ScriptableObjects/Characters/CatLocomotion.asset`: tốc độ chạy, tăng tốc, phanh, quán tính quay và thông số nhảy.
- `CatLocomotion.controller`: locomotion thường chỉ có Idle, Run, Jump Air; ngoài ra giữ 16 state kỹ năng và hai stance combat tối thiểu `Grab_Hold`/`Carry_Move`. Giữ chuột phải dùng stance riêng ổn định, còn tay được chỉnh procedural về phía mục tiêu; không chạy chuỗi clip combat dài.
- `Prefabs/Maps/PF_Map_TestFloor.prefab`: mặt sàn và 8 vị trí spawn (`MapSpawnPoints`), không chứa player/bot/camera.
- `CharacterReviewSession`: chỉ nối player–camera và trả player về nếu rơi khỏi sàn.

Player/bot là prefab độc lập: kéo vào sàn bất kỳ là có thể test; không có scene reference bắt buộc trong prefab. Camera player tự lấy MainCamera một lần khi bắt đầu nếu không được nối trước.

## Quy ước một scene xuyên suốt

`Main` giữ `Systems`, `Actors`, camera và **MapAnchor cố định tại (0,0,0)**. Nội dung bản đồ là prefab dưới MapAnchor, không dùng LoadScene khi đổi map. Các actor ở `Actors`, không làm con của map để tránh bị hủy khi gỡ map.

Ở bước sau, khi chốt khoảng chờ N giây: bộ điều phối map đếm thời gian → tạo map prefab tại MapAnchor → đợi map sẵn sàng → đặt các actor vào spawn point của map. `CatLocomotion.Warp` đã là điểm vào cho việc đổi vị trí actor và xóa đà/input cũ. Cần chốt thời gian chờ, thời lượng map, cách quay về lobby và xử lý map trước đó trước khi làm vòng lặp. **Chưa bật timer hoặc tự thay map ở bước duyệt này.**

## Undo bản combat cũ

Scene OfflineCombat, prefab arena, tuning/controller combat, script combat, UI và bài kiểm tra cũ đã chuyển sang `Archive/CombatPrototype_20261026/Assets/...` ngoài Assets. Không xóa vĩnh viễn; có thể khôi phục theo README trong thư mục lưu trữ. Không reset Git toàn project. Model, rig, 44 clip, skin, gear, inventory và thay đổi người dùng trước đó được giữ.

## Kiểm tra

`Tools → Paw Arena → Characters → Run Foundation Checks`: kiểm tra prefab độc lập, ground probe không nhận nhầm chính collider, tốc độ đi/chạy/đường chéo, phanh, nhảy, khóa di chuyển khi xem clip, pause/scrub/speed/reset và chỉ một scene trong build. Kết quả ở `Logs/CharacterFoundationChecks.xml`.

`CharacterFoundationPlayCheck.Run` chỉ chạy batch trong bản sao kiểm thử `Library/CombatValidationProject`. Kiểm tra auto Run không Shift, pose Jump Air tối thiểu, solo player, một scene/map, preview 3 clip, không HUD, tay rủ khi cúi/ngẩng, giới hạn zoom và góc dọc camera. Không tự chuyển Play Mode trên Unity làm việc của người dùng.

Bài kiểm tra bổ sung xác nhận thân nghiêng khi chạy, tay trễ khi đổi hướng và ổn định sau khi dừng. Kết quả kỹ thuật không thay cho duyệt feel bằng tay; đây là mốc locomotion để tiếp tục cân chỉnh, chưa phải bản combat hoàn chỉnh.
