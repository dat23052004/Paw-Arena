# Active Ragdoll — hướng dẫn tinh chỉnh

## Vị trí cấu hình

Player và Bot cùng dùng asset:

`Assets/_Project/ScriptableObjects/Characters/CatActiveRagdollSettings.asset`

Nên chỉnh asset khi đã thoát Play Mode, mỗi lần thay đổi khoảng 10–20%, sau đó thử lại theo thứ tự: Idle → Run → Jump → Hit → Knockout. Các thay đổi trực tiếp trên ScriptableObject trong Play Mode có thể được Unity giữ lại.

Active Ragdoll chỉ điều khiển skeleton hiển thị. Rigidbody/capsule gốc vẫn quyết định di chuyển, damage, grab và ring-out, nên các thông số dưới đây không đổi luật gameplay.

Giữ `Animator > Update Mode` ở `Normal`. Cơ chế phục hồi pose mục tiêu chạy trước Animator mỗi frame; chuyển sang `Animate Physics` sẽ làm sai thứ tự lấy pose và có thể khiến ragdoll xẹp trở lại.

## Preset ổn định hiện tại

- Angular Spring: `180`
- Angular Damping: `28`
- Maximum Angular Acceleration: `520`
- Body Position Spring: `220`
- Body Position Damping: `32`
- Maximum Body Acceleration: `320`
- Pelvis Position Spring: `300`
- Pelvis Position Damping: `38`
- Maximum Pelvis Acceleration: `360`
- Pelvis Linear Limit: `0.08`
- Default Joint Limit: `28`
- Hit Strength: `0.45`
- Held Strength: `0.30`
- Knocked Out Strength: `0.025`
- Airborne Strength: `0.92`
- Impulse Multiplier: `0.12`

Đây là mốc an toàn để nhân vật tự đứng, theo kịp root khi chạy và tự hồi phục sau khi ragdoll.

## Tay theo animation, chỉ nắm tay có độ mềm vật lý (2026-11-05)

- Khi còn tỉnh: UpperArm và LowerArm giữ nguyên local pose của animation, đi theo ngực vật lý. Không có trọng lực hay lực khớp kéo chúng xuống. Proxy vật lý của 4 đoạn tay là kinematic, không collision và thả ràng buộc với ngực.
- Mỗi Hand có Rigidbody riêng nối vào wrist anchor kinematic, khóa dịch chuyển và chỉ cho xoay nhẹ. Khi hiển thị chỉ áp dụng góc lệch; cổ tay giữ vị trí gốc để không kéo giãn mesh.
- Hand Angular Limit: `12°`; Hand Spring: `160`; Hand Damping: `24`; Hand Mass: `0.12`. Thông số khớp nắm tay được tạo lúc vào Play; đổi rồi khởi động lại Play để kiểm tra.
- Khi KnockedOut: 4 đoạn tay trở lại ragdoll cũ, khởi tạo từ pose đang hiển thị. Hồi phục thì trả quyền điều khiển cho animation.
- Thân/chân giữ nguyên tuning nêu trên. Model, 6 clip và logic damage/grab/lift không thay đổi. Collider nắm tay vẫn chỉ thuộc lớp trình diễn, không thay hitbox combat.
- Giữ cơ chế LOD cũ: ngoài khoảng mô phỏng, tất cả về animation. Hiện có 14 joint/nhân vật nhưng chỉ 10 body động khi tỉnh, 14 khi knockout; chưa phải kết quả benchmark lobby 16 người.

Kiểm thử bằng `PawArena.Editor.CatCoreCombatCheck.Run`: giữ Grab 10 giây có chạy/đổi hướng, đo sai lệch vai/khuỷu và khe cổ tay, thử lực xoay nắm tay, Punch/Lift/Jump, knockout/hồi phục. Kết quả ở `Logs/HandOnlyRagdollValidation.json`. Các hướng dẫn lực cơ bên dưới áp dụng cho thân/chân, và cho tay khi knockout, không dùng để chỉnh độ mềm nắm tay.

## Nhóm Muscles

### Angular Spring

Lực xoay body về hướng của animation.

- Tăng: thân và tay chân bám animation chặt hơn, đứng dậy nhanh hơn.
- Giảm: mềm và lắc nhiều hơn.
- Quá cao: dễ rung hoặc giật ở joint.

### Angular Damping

Hãm vận tốc xoay.

- Tăng nếu tay chân rung hoặc xoay qua lại quanh target.
- Giảm nếu chuyển động quá chậm và nặng.
- Khi tăng Angular Spring, thường cũng cần tăng Angular Damping.

### Maximum Angular Acceleration

Giới hạn lực xoay cực đại. Chỉ tăng khi Spring đã đủ cao nhưng nhân vật vẫn hồi hướng quá chậm. Nếu thấy giật mạnh sau va chạm, giảm thông số này trước.

### Hit / Held / Knocked Out Strength

Hệ số nhân sức cơ theo trạng thái.

- Hit Strength thấp: phản ứng đòn đánh mềm và sâu hơn.
- Held Strength thấp: người bị giữ rũ hơn.
- Knocked Out Strength gần `0`: ragdoll gần như hoàn toàn mềm.
- Nếu nhân vật không đứng lại sau khi hết trạng thái, kiểm tra phase có trở về `Free` hay không trước khi tăng các giá trị này.

### Airborne Strength

Hệ số cơ khi đang trên không. Khoảng `0.8–0.9` giữ silhouette nhảy rõ. Giảm về `0.5–0.7` nếu muốn tay chân văng nhiều hơn khi nhảy.

## Nhóm Pose Position Tracking

Đây là nhóm quan trọng nhất để rig mèo không nằm xẹp. Các lực này kéo spine và limbs về vị trí animation bằng `AddForce`, không teleport bone.

### Body Position Spring

- Tăng nếu đầu/ngực vẫn tụt xuống hoặc body không theo kịp khi chạy.
- Giảm nếu muốn chuyển động mềm, có độ trễ kiểu Party Animals.
- Không nên đặt `0` với rig hiện tại; angular-only không đủ nâng chuỗi spine/chest.

### Body Position Damping

Hãm body khi đi qua vị trí target. Tăng nếu ngực, đầu hoặc tay rung quanh animation. Nếu body theo target quá chậm, giảm nhẹ Damping trước khi tăng Spring quá cao.

### Maximum Body Acceleration

Giới hạn lực kéo vị trí. Tăng nếu chạy nhanh làm đầu/tay bị bỏ lại quá xa. Giảm nếu va chạm bị triệt tiêu quá nhanh và ragdoll trông quá cứng.

## Nhóm Balance

### Pelvis Position Spring

Lực chính kéo hông theo gameplay capsule. Nếu cả cơ thể bị kéo lê phía sau root, tăng thông số này.

### Pelvis Position Damping

Hãm độ nảy của hông. Tăng nếu toàn thân nhún hoặc rung lên xuống. Giá trị quá cao làm hông phản ứng chậm.

### Maximum Pelvis Acceleration

Giới hạn lực hồi phục của hông. Tăng khi dash/chạy nhanh làm puppet không bắt kịp capsule.

### Pelvis Linear Limit

Khoảng cách tối đa giữa hông và gameplay anchor.

- `0.10–0.16`: ổn định, dễ đứng.
- `0.17–0.24`: mềm và lắc nhiều hơn.
- Trên `0.30`: dễ nằm xẹp hoặc bị kéo lê với rig hiện tại.

## Nhóm Bodies

### Default Joint Limit

Độ tự do xoay chung của joint.

- `30–40`: chân và thân ổn định.
- `41–55`: mềm hơn, dễ cuộn người hơn.
- Quá thấp: animation chạy bị cứng.
- Quá cao: đầu gối/spine dễ gập sai hướng.

### Body Mass

Khối lượng cơ sở trước multiplier theo bone. Muscle dùng `ForceMode.Acceleration`, nên Mass chủ yếu thay đổi cảm giác va chạm và tương tác với môi trường.

### Linear / Body Angular Damping

Drag vật lý nền. Chỉ tăng nhẹ nếu sau va chạm body còn trượt hoặc xoay quá lâu. Trước tiên nên chỉnh các Damping của muscle vì chúng kiểm soát target rõ hơn.

### Collider Radius Scale

Độ dày collider theo chiều dài bone. Thay đổi nhỏ khoảng `0.02`. Collider quá lớn làm tay/chân kê nhân vật khỏi sàn; quá nhỏ làm limb xuyên sàn.

### Impulse Multiplier

Tỉ lệ knockback gameplay được sao chép sang ragdoll hiển thị.

- `0.08–0.14`: phản ứng nhẹ.
- `0.15–0.22`: phản ứng rõ, phù hợp party game.
- Trên `0.25`: dễ làm body lật mạnh; cần tăng damping hoặc lực hồi phục.

## Quy trình chỉnh nhanh

### Nhân vật vẫn nằm hoặc ngực tụt

1. Tăng Body Position Spring mỗi lần `15–20`.
2. Tăng Maximum Body Acceleration mỗi lần `20–30` nếu body không bắt kịp.
3. Giữ Pelvis Linear Limit trong `0.10–0.16`.
4. Nếu có rung, tăng Body Position Damping mỗi lần `2–3`.

### Nhân vật quá cứng

1. Giảm Body Position Spring `10–15%`.
2. Giảm Angular Spring `10–15%`.
3. Tăng Default Joint Limit khoảng `3–5` độ.
4. Không giảm Pelvis Spring trước; hông cần ổn định để root gameplay và puppet không tách nhau.

### Nhân vật rung hoặc giật

1. Tăng Angular Damping và Body Position Damping `10–15%`.
2. Nếu vẫn rung, giảm Maximum Angular/Body Acceleration.
3. Sau cùng mới giảm Spring.

### Muốn phản ứng đòn đánh mềm hơn

1. Giảm Hit Strength về khoảng `0.30–0.40`.
2. Tăng Impulse Multiplier từng bước `0.02`.
3. Giữ Free strength mặc định để nhân vật tự đứng lại sau HitStun.

## Checklist sau mỗi lần chỉnh

- Idle ít nhất 3 giây: đầu cao hơn hông, hông không rung.
- Run và đổi hướng: puppet theo capsule, không bị kéo lê.
- Jump: silhouette còn đọc được, tay chân được phép trễ.
- Nhận hit: có phản ứng nhưng không xuyên sàn.
- Knockout rồi hồi phục: body mềm khi ngã và tự đứng lại khi phase trở về `Free`.
- Player và Bot: cả hai dùng chung asset nên phải kiểm tra cả hai nếu thay đổi lớn.
