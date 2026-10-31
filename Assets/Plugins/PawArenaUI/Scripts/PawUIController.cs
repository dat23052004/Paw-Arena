using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PawArenaUI
{
    // Demonstration UI only. Replace these local state values with your save/inventory system.
    public class PawUIController : MonoBehaviour
    {
        private PawItem[][] items;
        private readonly int[] equipped = { 2, 1, 0, 4, 0 };
        private int category = 0, itemIndex = 2, coins = 1657;
        private bool music = false, screenShake = false, giftsEveryone = true;
        private string currentModal = "";

        Transform Find(string name)
        {
            var ts = GetComponentsInChildren<Transform>(true);
            foreach (var t in ts) if (t.name == name) return t;
            return null;
        }
        T Comp<T>(string name) where T : Component
        { var t = Find(name); return t != null ? t.GetComponent<T>() : null; }
        void SetText(string name, string value)
        { var t = Comp<TMP_Text>(name); if (t != null) t.text = value; }
        void SetActive(string name, bool state)
        { var t = Find(name); if (t != null) t.gameObject.SetActive(state); }
        void Bind(string name, Action action)
        { var b = Comp<Button>(name); if (b != null) b.onClick.AddListener(() => action()); }
        void Awake()
        {
            items = PawCatalog.CreateItems();
            CloseAll();
            Bind("Nav_Inventory", () => Open("Inventory"));
            Bind("Nav_Settings", () => Open("Settings"));
            Bind("Nav_Stats", () => Open("Stats"));
            Bind("Nav_Rewards", () => Open("Rewards"));
            Bind("Nav_Updates", () => Open("Updates"));
            Bind("Nav_Notifications", () => Open("Updates"));
            Bind("Nav_Emotes", () => Open("Emotes"));
            Bind("Nav_Event", () => Open("Event"));
            Bind("Close_Inventory", CloseAll);
            Bind("Close_Settings", CloseAll);
            Bind("Close_Stats", CloseAll);
            Bind("Close_Rewards", CloseAll);
            Bind("Close_Updates", CloseAll);
            Bind("Close_Emotes", CloseAll);
            Bind("Close_Event", CloseAll);
            Bind("Btn_OkayUpdates", CloseAll);
            Bind("Btn_ClaimRewards", () => SetText("RewardStatus", "Come back tomorrow for more!"));
            Bind("Btn_MusicToggle", () => { music = !music; SetText("MusicToggleLabel", music ? "On" : "Off"); });
            Bind("Btn_ShakeToggle", () => { screenShake = !screenShake; SetText("ShakeToggleLabel", screenShake ? "On" : "Off"); });
            Bind("Btn_Redeem", () => SetText("SettingsCodeStatus", "Sample UI - connect your code service."));
            Bind("Btn_GiftSetting", () => { giftsEveryone = !giftsEveryone; SetText("Btn_GiftSettingLabel", giftsEveryone ? "Everyone" : "Friends"); });
            for(int i=0;i<3;i++)
            {
                int n=i;
                var slider=Comp<Slider>("Slider_"+i);
                if(slider!=null)slider.onValueChanged.AddListener(value=>SetText("SliderPercent_"+n, Mathf.RoundToInt(value*100f)+"%"));
            }
            for(int i=0;i<3;i++)
            {
                int version=i;
                Bind("VersionButton_"+i,()=>ShowUpdateVersion(version));
            }
            Bind("Btn_Equip", EquipOrBuy);
            Bind("Emote_0", () => SelectEmote(0));
            Bind("Emote_1", () => SelectEmote(1));
            Bind("Emote_2", () => SelectEmote(2));
            for (int c = 0; c < PawCatalog.Categories.Length; c++)
            {
                int captured = c;
                Bind("InvTab_" + c, () => SwitchCategory(captured));
                for (int j = 0; j < items[c].Length; j++)
                {
                    int ci = c, ji = j;
                    Bind("Item_" + c + "_" + j, () => SelectItem(ci, ji));
                }
            }
            SwitchCategory(0);
            UpdateCoins();
        }
        private void UpdateCoins()
        { SetText("HUD_CoinText", coins.ToString("N0")); }
        private void CloseAll()
        {
            foreach (string type in new [] {"Inventory","Settings","Stats","Rewards","Updates","Emotes","Event"})
                SetActive("Window_" + type, false);
            SetActive("ModalDimmer", false);
            currentModal = "";
        }
        private void Open(string name)
        {
            CloseAll();
            SetActive("ModalDimmer", true);
            SetActive("Window_" + name, true);
            currentModal = name;
            if (name == "Inventory") SwitchCategory(category);
        }
        private void SwitchCategory(int newCategory)
        {
            category = newCategory;
            for (int c = 0; c < PawCatalog.Categories.Length; c++)
            {
                SetActive("InvPage_" + c, c == category);
                var tab = Comp<Image>("TabBackground_" + c);
                if (tab != null) tab.color = (c == category) ? Color.white : new Color(.67f,.81f,1f,1f);
            }
            SelectItem(category, Mathf.Min(itemIndex, items[category].Length-1));
        }
        private void SelectItem(int cat, int index)
        {
            if (cat != category) SwitchCategory(cat);
            itemIndex = index;
            PawItem data = items[category][itemIndex];
            SetText("Inventory_Name", data.name);
            SetText("Inventory_Rarity", data.rarity);
            SetText("Inventory_Description", data.description);
            var detailIcon = Comp<Image>("Inventory_Icon");
            var thumb = Comp<Image>("ItemIcon_" + category + "_" + itemIndex);
            if (detailIcon != null && thumb != null) detailIcon.sprite = thumb.sprite;
            Color rarityColor = Color.white;
            switch (data.rarity)
            {
                case "Uncommon": rarityColor = new Color(.45f,.95f,.42f); break;
                case "Rare": rarityColor = new Color(.28f,.83f,1f); break;
                case "Epic": rarityColor = new Color(.79f,.39f,1f); break;
                case "Legendary": rarityColor = new Color(1f,.79f,.25f); break;
            }
            var rarityText = Comp<TMP_Text>("Inventory_Rarity");
            if (rarityText != null) rarityText.color = rarityColor;
            for (int j = 0; j < items[category].Length; j++)
            {
                var bg = Comp<Image>("Item_" + category + "_" + j);
                if (bg != null) bg.color = j == index ? Color.white : new Color(.69f,.8f,.95f,1f);
            }
            UpdateItemUI();
        }
        private void UpdateItemUI()
        {
            var data = items[category][itemIndex];
            var btnImage = Comp<Image>("Btn_Equip");
            var btnLabel = Comp<TMP_Text>("EquipButtonLabel");
            bool isEquipped = equipped[category] == itemIndex;
            if (btnLabel != null) btnLabel.text = !data.owned ? (data.price.ToString("N0") + " coins - Unlock") : (isEquipped ? "Equipped" : "Equip");
            if (btnImage != null) btnImage.color = !data.owned ? new Color(1f,.85f,.55f) : isEquipped ? new Color(.8f,.89f,1f) : Color.white;
            for (int c = 0; c < items.Length; c++)
              for (int i = 0; i < items[c].Length; i++)
              {
                  SetActive("ItemLock_" + c + "_" + i, !items[c][i].owned);
                  SetActive("ItemCheck_" + c + "_" + i, items[c][i].owned && equipped[c] == i);
                  SetText("ItemPrice_" + c + "_" + i, items[c][i].owned ? "" : items[c][i].price.ToString("N0"));
              }
        }
        private void EquipOrBuy()
        {
            PawItem current = items[category][itemIndex];
            if (!current.owned)
            {
                if (current.price > coins)
                {
                    SetText("Inventory_Description", "Not enough coins to unlock " + current.name + ".");
                    return;
                }
                coins -= current.price;
                current.owned = true;
                UpdateCoins();
            }
            equipped[category] = itemIndex;
            UpdateItemUI();
        }
        private void ShowUpdateVersion(int index)
        {
            string[] banners={"HALLOWEEN 2\nTHE GRAVEYARD","SEASON 1\nARENA CHALLENGE","WELCOME TO\nPAW ARENA"};
            SetText("BannerText",banners[index]);
        }
        private void SelectEmote(int index)
        {
            string[] names = { "Wave", "Stretch", "Beam Me Up" };
            SetText("EmoteSelected", "Selected: " + names[index]);
            // Hook your animator / character controller here.
            CloseAll();
        }
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame && currentModal != "") CloseAll();
            if (keyboard.iKey.wasPressedThisFrame) { if(currentModal == "Inventory") CloseAll(); else Open("Inventory"); }
            if (keyboard.gKey.wasPressedThisFrame) { if(currentModal == "Emotes") CloseAll(); else Open("Emotes"); }
        }
    }
}
