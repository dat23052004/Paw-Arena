using UnityEngine;

namespace PawArena.Gameplay.Combat
{
    /// <summary>Compact runtime combat readout for the playable vertical slice.</summary>
    [DisallowMultipleComponent]
    public sealed class CatCombatHud : MonoBehaviour
    {
        [SerializeField] private CatCombatant _actor;
        [SerializeField] private CatPlayerCombat _combat;
        [SerializeField] private bool _visible = true;
        private GUIStyle _label;
        private GUIStyle _title;

        private void OnGUI()
        {
            if (!_visible || _actor == null || _combat == null) { return; }
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
            _title ??= new GUIStyle(_label) { fontSize = 20, fontStyle = FontStyle.Bold };
            var area = new Rect(20, 20, 470, 150);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(34, 28, 445, 135));
            GUILayout.Label($"HP  {Mathf.CeilToInt(_actor.Health)} / {Mathf.CeilToInt(_actor.Settings.MaxHealth)}   •   {_actor.Phase}", _title);
            GUILayout.Label($"Skill: {_combat.SelectedAbility}   •   Cooldown: {_combat.SelectedCooldown:0.0}s", _label);
            GUILayout.Label("LMB đấm/ném  •  giữ RMB bắt  •  Shift nhấc  •  Q dùng skill", _label);
            GUILayout.Label("Z/X đổi skill  •  RMB vẫn xoay camera  •  Space nhảy", _label);
            GUILayout.EndArea();
        }
    }
}
