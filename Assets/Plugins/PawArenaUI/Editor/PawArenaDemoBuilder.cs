#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PawArenaUI.Editor
{
    public static class PawArenaDemoBuilder
    {
        const string Base = "Assets/Plugins/PawArenaUI/Sprites/";
        const string SceneFolder = "Assets/_Project/Scenes/UI";
        const string PrefabFolder = "Assets/_Project/Prefabs/UI/PawArenaUI";
        static Font font;
        static Sprite Load(string folder, string id) => AssetDatabase.LoadAssetAtPath<Sprite>(Base + folder + "/" + id + ".png");
        static Color Hex(string s) { ColorUtility.TryParseHtmlString(s, out Color c); return c; }
        static Vector2 V(float x, float y) => new Vector2(x,y);
        static RectTransform R(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();
            rt.anchorMin=anchor; rt.anchorMax=anchor; rt.pivot=V(.5f,.5f);
            rt.sizeDelta=size; rt.anchoredPosition=pos;
            return rt;
        }
        static RectTransform Stretch(Transform parent, string name)
        {
            var rt=R(parent,name,V(.5f,.5f),V(0,0),V(0,0));
            rt.anchorMin=V(0,0);rt.anchorMax=V(1,1);rt.offsetMin=V(0,0);rt.offsetMax=V(0,0);
            return rt;
        }
        static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if(!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent,name);
        }
        static Image Image(Transform parent, string name, string folder, string id, Vector2 anchor, Vector2 size, Vector2 pos, bool sliced=false)
        {
            var rt=R(parent,name,anchor,size,pos);
            var image=rt.gameObject.AddComponent<Image>();
            image.sprite=Load(folder,id);
            image.type=sliced?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect=!sliced;
            return image;
        }
        static Image ImageStretch(Transform parent,string name,string folder,string id,bool sliced=true)
        {
            var rt=Stretch(parent,name); var img=rt.gameObject.AddComponent<Image>();
            img.sprite=Load(folder,id); img.type=sliced?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            return img;
        }
        static Text Text(Transform parent,string name,string content,Vector2 anchor,Vector2 size,Vector2 pos,int pts=28,string color="#F5FBFF",TextAnchor align=TextAnchor.MiddleCenter,bool outline=false)
        {
            var rt=R(parent,name,anchor,size,pos); var tx=rt.gameObject.AddComponent<Text>();
            tx.font=font;tx.text=content;tx.fontSize=pts;tx.fontStyle=FontStyle.Bold;tx.color=Hex(color);
            tx.alignment=align;tx.horizontalOverflow=HorizontalWrapMode.Wrap;tx.verticalOverflow=VerticalWrapMode.Truncate;
            tx.resizeTextForBestFit=true;tx.resizeTextMinSize=Mathf.Max(10,pts/2);tx.resizeTextMaxSize=pts;
            tx.raycastTarget=false;
            if(outline) {var ol=rt.gameObject.AddComponent<UnityEngine.UI.Outline>();ol.effectColor=new Color(.05f,.1f,.22f,.95f);ol.effectDistance=V(2,-2);}
            return tx;
        }
        static Button Button(Transform parent,string name,string caption,string color,Vector2 anchor,Vector2 size,Vector2 pos,int pts=26)
        {
            var img=Image(parent,name,"Buttons","button_"+color+"_normal",anchor,size,pos,true);
            var b=img.gameObject.AddComponent<Button>();
            b.targetGraphic=img;b.transition=Selectable.Transition.SpriteSwap;
            var st=new SpriteState { highlightedSprite=Load("Buttons","button_"+color+"_hover"),pressedSprite=Load("Buttons","button_"+color+"_pressed"),selectedSprite=Load("Buttons","button_"+color+"_hover"),disabledSprite=Load("Buttons","button_disabled")};
            b.spriteState=st;
            Text(img.transform,name+"Label",caption,V(.5f,.5f),size-V(16,8),V(0,0),pts,"#FFFFFF",TextAnchor.MiddleCenter,true);
            return b;
        }
        static Button IconButton(Transform parent,string name,string background,string icon,Vector2 anchor,Vector2 size,Vector2 pos)
        {
            var img=Image(parent,name,"Buttons",background,anchor,size,pos,true);
            var b=img.gameObject.AddComponent<Button>();b.targetGraphic=img;
            Image(img.transform,name+"Icon","Icons",icon,V(.5f,.5f),size*.74f,V(0,0));
            return b;
        }
        static Image Icon(Transform parent,string name,string id,Vector2 anchor,Vector2 size,Vector2 pos)
            => Image(parent,name,"Icons",id,anchor,size,pos);
        static RectTransform Panel(Transform parent,string name,string sprite,Vector2 anchor,Vector2 size,Vector2 pos)
            => Image(parent,name,"Panels",sprite,anchor,size,pos,true).rectTransform;
        static RectTransform Modal(Transform root, string name, Vector2 size)
        {
            var p=Panel(root,"Window_"+name,"window_panel",V(.5f,.5f),size,V(0,0));
            Text(p,"Title_"+name,name=="Updates"?"Update log":name=="Rewards"?"Daily rewards":name,V(.0f,1f),V(440,60),V(250,-36),36,"#FFFFFF",TextAnchor.MiddleLeft,true);
            IconButton(p,"Close_"+name,"close_red","icon_close",V(1,1),V(75,75),V(-28,-27));
            return p;
        }
        static void Tab(RectTransform p,int index,string caption,float x)
        {
            var bg=Image(p,"TabBackground_"+index,"Buttons",index==0?"tab_active":"tab_inactive",V(.5f,.5f),V(167,62),V(x,0),true);
            bg.gameObject.name="TabBackground_"+index;
            bg.gameObject.AddComponent<Button>();
            bg.gameObject.name="InvTab_"+index; // Rename wrapper and add distinct background child for tint.
            var child=Image(bg.transform,"TabBackground_"+index,"Buttons",index==0?"tab_active":"tab_inactive",V(.5f,.5f),V(167,62),V(0,0),true);
            child.raycastTarget=false;
            Text(bg.transform,"InvTabLabel_"+index,caption,V(.5f,.5f),V(156,50),V(0,0),25,"#FFFFFF",TextAnchor.MiddleCenter,true);
        }
        static void HUD(Transform canvas)
        {
            var hud=Stretch(canvas,"HUDRoot");
            Image(hud,"HUD_CoinBG","Badges","coin_counter",V(0,1),V(255,78),V(152,-100),true);
            Icon(hud,"HUD_CoinIcon","icon_coin",V(0,1),V(58,58),V(60,-100));
            Text(hud,"HUD_CoinText","1,657",V(0,1),V(138,59),V(166,-101),41,"#FFD342",TextAnchor.MiddleCenter,true);
            IconButton(hud,"Nav_Settings","arrow_blue","icon_settings",V(0,1),V(65,65),V(283,-43));
            IconButton(hud,"Nav_Stats","arrow_blue","icon_trophy",V(0,1),V(65,65),V(208,-43));
            var nav=Panel(hud,"HUD_LeftNav","sidebar_panel",V(0,1),V(235,220),V(135,-290));
            IconButton(nav,"Nav_Updates","arrow_blue","icon_shop",V(.5f,.5f),V(92,86),V(-53,47));
            IconButton(nav,"Nav_Inventory","arrow_blue","icon_bag",V(.5f,.5f),V(92,86),V(53,47));
            IconButton(nav,"Nav_Emotes","arrow_blue","icon_paw",V(.5f,.5f),V(92,86),V(-53,-47));
            IconButton(nav,"Nav_Rewards","arrow_blue","icon_gift",V(.5f,.5f),V(92,86),V(53,-47));
            Text(hud,"HUD_ShopLabel","Shop",V(0,1),V(112,36),V(74,-449),22,"#FFFFFF",TextAnchor.MiddleCenter,true);
            Text(hud,"HUD_InvLabel","Inventory",V(0,1),V(112,36),V(184,-449),22,"#FFFFFF",TextAnchor.MiddleCenter,true);
            Text(hud,"HUD_Afk","AFK  ● On",V(0,1),V(165,44),V(107,-495),23,"#B6FFE0",TextAnchor.MiddleCenter,true);
            Image(hud,"HUD_CandyBG","Badges","coin_counter",V(0,0),V(195,65),V(126,97),true);
            Icon(hud,"HUD_CandyIcon","icon_candy",V(0,0),V(58,58),V(61,97));
            Text(hud,"HUD_CandyText","60",V(0,0),V(80,48),V(140,97),35,"#FFAB68",TextAnchor.MiddleCenter,true);
            var q=Panel(hud,"HUD_Quests","content_panel",V(1,.70f),V(355,230),V(-207,0));
            Text(q,"QuestTitle","Daily Quests",V(.5f,1),V(320,50),V(0,-30),30,"#98CCFF",TextAnchor.MiddleCenter,true);
            string[] lines={"Play 15 matches       1/15   +150","Win 5 matches         0/5     +175","Play with friends     0/3     +150"};
            for(int i=0;i<3;i++)
            {
                var c=Image(q,"QuestRow_"+i,"Panels","quest_row",V(.5f,.5f),V(320,48),V(0,44-i*52),true);
                Text(c.transform,"QuestText_"+i,lines[i],V(.5f,.5f),V(300,38),V(0,0),20,"#FFDF72",TextAnchor.MiddleCenter);
            }
            Text(q,"QuestTimer","Resets in 2h 49m",V(.5f,0),V(300,34),V(0,13),21,"#F78CAB");
            IconButton(hud,"Nav_Event","close_red","icon_gift",V(1,0),V(110,110),V(-104,120));
            Text(hud,"EventLabel","Event",V(1,0),V(115,34),V(-104,52),27,"#FFFFFF",TextAnchor.MiddleCenter,true);
            Text(hud,"Controls","Punch  ·  Grab  ·  Shift Lift  ·  Space Jump",V(1,0),V(530,65),V(-285,30),23,"#D6F4FF",TextAnchor.MiddleRight,true);
            Text(hud,"EmoteSelected","",V(.5f,0),V(450,55),V(0,55),25,"#D6F4FF");
        }
        static void ItemCard(RectTransform page, int category, int index, PawItem item,Vector2 pos)
        {
            bool owned=item.owned;
            string cardSprite=owned?"item_owned":"item_locked";
            var bg=Image(page,"Item_"+category+"_"+index,"Cards",cardSprite,V(.5f,.5f),V(159,201),pos,true);
            bg.gameObject.AddComponent<Button>();
            Icon(bg.transform,"ItemIcon_"+category+"_"+index,item.icon,V(.5f,.5f),V(127,127),V(0,30));
            Text(bg.transform,"ItemName_"+category+"_"+index,item.name,V(.5f,.5f),V(148,30),V(0,-82),22,item.rarity=="Epic"?"#F2A0FF":"#F7FBFF",TextAnchor.MiddleCenter,true);
            Icon(bg.transform,"ItemLock_"+category+"_"+index,"icon_lock",V(.5f,.5f),V(46,46),V(0,-17));
            Icon(bg.transform,"ItemCheck_"+category+"_"+index,"icon_check",V(1,1),V(42,42),V(-17,-15));
            Text(bg.transform,"ItemPrice_"+category+"_"+index,owned?"":item.price.ToString("N0"),V(.5f,.5f),V(130,30),V(0,-57),22,"#FFD24E",TextAnchor.MiddleCenter,true);
        }
        static void Inventory(Transform root)
        {
            var wnd=Modal(root,"Inventory",V(1530,850));
            var tabbar=Panel(wnd,"InventoryTabBar","tabbar_panel",V(.5f,.5f),V(900,85),V(205,355));
            for(int i=0;i<PawCatalog.Categories.Length;i++) Tab(tabbar,i,PawCatalog.Categories[i],-352+i*177);
            var left=Panel(wnd,"InventoryList","content_panel",V(.5f,.5f),V(960,700),V(-265,-55));
            var detail=Panel(wnd,"InventoryDetail","content_panel",V(.5f,.5f),V(475,700),V(504,-55));
            for (int c=0;c<PawCatalog.Categories.Length;c++)
            {
                var page=R(left,"InvPage_"+c,V(.5f,.5f),V(935,680),V(0,0));
                Text(page,"OwnedLabel_"+c,"OWNED",V(.5f,.5f),V(860,40),V(0,302),32,"#FFFFFF",TextAnchor.MiddleCenter,true);
                Text(page,"UnownedLabel_"+c,"────────   UNOWNED   ────────",V(.5f,.5f),V(900,50),V(0,-12),33,"#FFFFFF",TextAnchor.MiddleCenter,true);
                var items=PawCatalog.CreateItems()[c];
                for(int i=0;i<items.Length;i++)
                {
                    bool owned=items[i].owned;
                    // First row = owned, second row = locked. Maximum five across.
                    int ownedCount=0;
                    foreach (var candidate in items) if(candidate.owned) ownedCount++;
                    int col=i<ownedCount ? i : i-ownedCount;
                    float x=-356+col*178;
                    float y=i<ownedCount ? 163 : -170;
                    ItemCard(page,c,i,items[i],V(x,y));
                }
                page.gameObject.SetActive(c==0);
            }
            Icon(detail,"Inventory_Icon","fur_black_widow",V(.5f,1),V(225,225),V(0,-138));
            Text(detail,"Inventory_Name","Black Widow",V(.5f,1),V(410,60),V(0,-271),41,"#FFFFFF",TextAnchor.MiddleCenter,true);
            Text(detail,"Inventory_Rarity","Epic",V(.5f,1),V(310,35),V(0,-317),25,"#DB7BFF",TextAnchor.MiddleCenter,true);
            Text(detail,"Inventory_Description","Eight legs. Maybe four.",V(.5f,.5f),V(405,145),V(0,-42),30,"#D6E9FF",TextAnchor.MiddleCenter);
            Button(detail,"Btn_Equip","Equipped","green",V(.5f,0),V(400,78),V(0,58),27);
            // Runtime uses this exact label name for live updates.
            var label=detail.Find("Btn_Equip").Find("Btn_EquipLabel");
            if(label!=null)label.name="EquipButtonLabel";
            Text(wnd,"InventorySearchLabel","Search item  ·  Optional extension",V(0,0),V(420,40),V(248,16),19,"#9FD1FF");
        }
        static void Stats(Transform root)
        {
            var p=Modal(root,"Stats",V(1000,630));
            var av=Panel(p,"StatsAvatar","content_panel",V(.5f,.5f),V(282,430),V(-313,-18));
            Icon(av,"StatsFace","fur_ginger",V(.5f,.5f),V(232,235),V(0,55));
            Text(av,"StatsUser","PLAYER",V(.5f,0),V(270,75),V(0,51),35,"#FFFFFF",TextAnchor.MiddleCenter,true);
            string[] names={"Wins","KOs","Best streak","Current streak"}; string[] vals={"1","8","🔥 1","0"};
            for(int i=0;i<4;i++)
            {
                int col=i%2,row=i/2;
                var c=Panel(p,"StatCell_"+i,"stat_panel",V(.5f,.5f),V(272,192),V(4+col*285,93-row*211));
                Text(c,"StatTitle_"+i,names[i],V(.5f,1),V(250,60),V(0,-40),31,i==2?"#FFA53B":"#FFFFFF",TextAnchor.MiddleCenter,true);
                Text(c,"StatValue_"+i,vals[i],V(.5f,.5f),V(235,105),V(0,-18),64,"#FFFFFF",TextAnchor.MiddleCenter,true);
            }
        }
        static void Rewards(Transform root)
        {
            var p=Modal(root,"Rewards",V(1530,710));
            Icon(p,"RewardFire","icon_flame",V(0,1),V(72,72),V(94,-90));
            Text(p,"RewardCount","4 Days",V(0,1),V(190,55),V(192,-94),33,"#FFCC4F",TextAnchor.MiddleCenter,true);
            Text(p,"RewardHeading","Return every day for a bigger reward!",V(.5f,1),V(835,55),V(20,-94),34,"#FFFFFF",TextAnchor.MiddleCenter,true);
            string[] things={"250 coins","Fur Egg Tier 1","1,500 coins","Emote Egg","Fur Egg Tier 2"};
            string[] icons={"icon_coin","icon_egg","icon_coin","icon_paw","icon_egg"};
            for(int i=0;i<5;i++)
            {
                var c=Image(p,"RewardCard_"+i,"Cards",i==4?"reward_current":"reward_claimed",V(.5f,.5f),V(i==4?265:232,423),V(-577+i*289,-55),true);
                Text(c.transform,"RewardDay_"+i,"Day "+(i+1),V(.5f,1),V(214,56),V(0,-42),31,i==4?"#FFD45B":"#A5C7F3",TextAnchor.MiddleCenter,true);
                Text(c.transform,"RewardName_"+i,things[i],V(.5f,1),V(215,54),V(0,-93),23,"#FFFFFF",TextAnchor.MiddleCenter,true);
                Icon(c.transform,"RewardIcon_"+i,icons[i],V(.5f,.5f),V(165,165),V(0,-20));
                if(i<4)
                {
                    Icon(c.transform,"RewardClaimCheck_"+i,"icon_check",V(.5f,.5f),V(78,78),V(0,-20));
                    Text(c.transform,"RewardLabel_"+i,"Claimed",V(.5f,0),V(200,55),V(0,40),25,"#C6DBFB",TextAnchor.MiddleCenter,true);
                }
                else
                {
                    Button(c.transform,"Btn_ClaimRewards","in 14h 49m","gold",V(.5f,0),V(220,72),V(0,52),25);
                }
            }
            Text(p,"RewardStatus","Day 5 unlocks after the countdown.",V(.5f,0),V(800,35),V(0,24),19,"#DBEBFF");
        }
        static Slider SliderControl(Transform p,string id,string name,int y)
        {
            Text(p,"SettingsLabel_"+id,name,V(.5f,.5f),V(230,50),V(-127,y),26,"#FFFFFF",TextAnchor.MiddleLeft,true);
            var track=Image(p,"Slider_"+id,"Badges","slider_track",V(.5f,.5f),V(240,26),V(88,y),true);
            var slider=track.gameObject.AddComponent<Slider>();
            var fill=Image(track.transform,"Fill_"+id,"Badges","slider_fill",V(.5f,.5f),V(240,26),V(0,0),true);
            var handle=Image(track.transform,"Knob_"+id,"Icons","icon_coin",V(.5f,.5f),V(34,34),V(0,0));
            slider.fillRect=fill.rectTransform;
            slider.handleRect=handle.rectTransform;
            slider.direction=Slider.Direction.LeftToRight;
            slider.minValue=0;slider.maxValue=1;slider.value=0;
            Text(p,"SliderPercent_"+id,"0%",V(.5f,.5f),V(75,50),V(229,y),22,"#FFFFFF");
            return slider;
        }
        static void Settings(Transform root)
        {
            var p=Modal(root,"Settings",V(700,825));
            string[] names={"Music","Sounds","Interface"};
            for(int i=0;i<3;i++)
            {
                var row=Panel(p,"SettingsRow_"+i,"content_panel",V(.5f,.5f),V(600,75),V(0,230-i*91));
                SliderControl(row,""+i,names[i],0);
            }
            var rowS=Panel(p,"SettingsShakeRow","content_panel",V(.5f,.5f),V(600,75),V(0,-65));
            Text(rowS,"ShakeLabel","Screen shake",V(.5f,.5f),V(380,60),V(-105,0),27,"#FFFFFF",TextAnchor.MiddleLeft,true);
            Button(rowS,"Btn_ShakeToggle","Off","gray",V(.5f,.5f),V(118,56),V(222,0),24);
            var s= rowS.Find("Btn_ShakeToggle").Find("Btn_ShakeToggleLabel");if(s!=null)s.name="ShakeToggleLabel";
            var rowM=Panel(p,"SettingsMusicRow","content_panel",V(.5f,.5f),V(600,75),V(0,-153));
            Text(rowM,"MusicLabel","Copyrighted music",V(.5f,.5f),V(385,60),V(-103,0),25,"#FFFFFF",TextAnchor.MiddleLeft,true);
            Button(rowM,"Btn_MusicToggle","Off","gray",V(.5f,.5f),V(118,56),V(222,0),24);
            var m=rowM.Find("Btn_MusicToggle").Find("Btn_MusicToggleLabel");if(m!=null)m.name="MusicToggleLabel";
            var rowC=Panel(p,"SettingsGiftsRow","content_panel",V(.5f,.5f),V(600,75),V(0,-244));
            Text(rowC,"GiftLabel","Receive gifts from",V(.5f,.5f),V(350,60),V(-124,0),25,"#FFFFFF",TextAnchor.MiddleLeft,true);
            Button(rowC,"Btn_GiftSetting","Everyone","blue",V(.5f,.5f),V(206,56),V(185,0),22);
            var rowCode=Panel(p,"SettingsCodeRow","content_panel",V(.5f,.5f),V(600,75),V(0,-329));
            Text(rowCode,"CodeHint","Enter a code",V(.5f,.5f),V(300,54),V(-116,0),25,"#A2BADC",TextAnchor.MiddleLeft);
            Button(rowCode,"Btn_Redeem","Redeem","blue",V(.5f,.5f),V(168,55),V(193,0),23);
            Text(p,"SettingsCodeStatus","",V(.5f,0),V(600,25),V(0,14),17,"#FFD667");
        }
        static void Updates(Transform root)
        {
            var p=Modal(root,"Updates",V(1380,825));
            var side=Panel(p,"UpdateSidebar","sidebar_panel",V(.5f,.5f),V(247,625),V(-515,-40));
            string[] vers={"Update 2","Update 1","Update 0.5"};
            for(int i=0;i<vers.Length;i++)Button(side,"VersionButton_"+i,vers[i],i==0?"green":"blue",V(.5f,1),V(205,79),V(0,-76-i*100),25);
            var body=Panel(p,"UpdateContent","content_panel",V(.5f,.5f),V(1000,625),V(128,-40));
            var banner=Panel(body,"UpdateBanner","header_panel",V(.5f,1),V(942,215),V(0,-126));
            Icon(banner,"BannerIcon","icon_skull",V(1,.5f),V(160,160),V(-100,10));
            Text(banner,"BannerText","HALLOWEEN 2\nTHE GRAVEYARD",V(.5f,.5f),V(735,145),V(-84,0),46,"#FFFFFF",TextAnchor.MiddleCenter,true);
            string[] title={"The Graveyard","Soul Reaper","Nevermore","Reaper Wheel","Hallow Egg","Pirate Ships"};
            string[] ic={"icon_skull","icon_flame","icon_egg","icon_crown","icon_pumpkin","icon_gift"};
            string[] desc={"Explore the new island","Win and unlock skins","Special seasonal rewards","Spin for limited loot","Collect rare companions","A new arena to visit"};
            for(int i=0;i<6;i++)
            {
                int col=i%3,row=i/3;
                var c=Panel(body,"UpdateCard_"+i,"stat_panel",V(.5f,.5f),V(294,155),V(-311+col*312,28-row*171));
                Icon(c,"UpdateCardIcon_"+i,ic[i]=="icon_pumpkin"?"fur_pumpkin":ic[i],V(0,1),V(70,70),V(48,-48));
                Text(c,"UpdateTitle_"+i,title[i],V(.5f,1),V(205,53),V(43,-43),23,"#FFFFFF",TextAnchor.MiddleCenter,true);
                Text(c,"UpdateDesc_"+i,desc[i],V(.5f,0),V(270,57),V(0,35),20,"#CCE1FF",TextAnchor.MiddleCenter);
            }
            Button(p,"Btn_OkayUpdates","Okay","green",V(.5f,0),V(270,68),V(0,48),30);
        }
        static void Emotes(Transform root)
        {
            var container=R(root,"Window_Emotes",V(.5f,.5f),V(680,680),V(0,0));
            var wheel=Image(container,"EmoteCircle","Panels","emote_wheel_base",V(.5f,.5f),V(620,620),V(0,0),false);
            Text(container,"EmotesTitle","EMOTES",V(.5f,.5f),V(330,100),V(0,0),44,"#FFFFFF",TextAnchor.MiddleCenter,true);
            string[] ids={"icon_wave","icon_stretch","icon_beam"};
            for (int i=0;i<8;i++)
            {
                float angle=(90-i*45)*Mathf.Deg2Rad;
                float x=233*Mathf.Cos(angle),y=233*Mathf.Sin(angle);
                var a=Image(container,"EmoteSlot_"+i,"Cards","item_owned",V(.5f,.5f),V(110,111),V(x,y),true);
                if(i<3){a.gameObject.name="Emote_"+i;a.gameObject.AddComponent<Button>();Icon(a.transform,"EmoteIcon_"+i,ids[i],V(.5f,.5f),V(94,94),V(0,0));}
                else a.color=new Color(.3f,.5f,.8f,.6f);
            }
            IconButton(container,"Close_Emotes","close_red","icon_close",V(1,1),V(74,74),V(-15,-15));
        }
        [MenuItem("Tools/Paw Arena UI/Generate Demo Scene")]
        public static void Generate()
        {
            AssetDatabase.Refresh();
            try { font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { font=null; }
            if(font==null) { try { font=Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { font=null; } }
            if(font==null)font=Font.CreateDynamicFontFromOSFont("Arial",28);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var go=new GameObject("PawArenaCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(PawUIController));
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=0;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=V(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;
            var backdrop=ImageStretch(go.transform,"DemoBackground","Panels","content_panel",true);
            backdrop.color=Hex("#5586B9");
            // Background is illustrative; replace this Image with your gameplay camera output.
            Image(go.transform,"Decoration_1","Icons","icon_crown",V(.25f,.7f),V(380,380),V(0,0)).color=new Color(1,1,1,.23f);
            Image(go.transform,"Decoration_2","Icons","fur_pumpkin",V(.75f,.3f),V(460,460),V(0,0)).color=new Color(1,1,1,.15f);
            HUD(go.transform);
            var dim=Stretch(go.transform,"ModalDimmer");var im=dim.gameObject.AddComponent<Image>();im.color=new Color(.02f,.06f,.12f,.70f);
            var layer=Stretch(go.transform,"ModalLayer");
            Inventory(layer);Settings(layer);Rewards(layer);Stats(layer);Updates(layer);Emotes(layer);
            var eventSystem = new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            EnsureFolder("Assets/_Project/Scenes","UI");
            EnsureFolder("Assets/_Project/Prefabs","UI");
            EnsureFolder("Assets/_Project/Prefabs/UI","PawArenaUI");
            EditorSceneManager.SaveScene(scene,SceneFolder+"/PawArenaUIShowcase.unity");
            // Reusable prefabs, exported by Unity after import (not stored as handwritten YAML).
            PrefabUtility.SaveAsPrefabAsset(go,PrefabFolder+"/PawArenaCanvas.prefab");
            PrefabUtility.SaveAsPrefabAsset(eventSystem,PrefabFolder+"/EventSystem.prefab");
            PrefabUtility.SaveAsPrefabAsset(go.transform.Find("HUDRoot").gameObject,PrefabFolder+"/HUDRoot.prefab");
            foreach(string type in new [] {"Inventory","Settings","Stats","Rewards","Updates","Emotes"})
            {
                var source=layer.Find("Window_"+type);
                if(source!=null) PrefabUtility.SaveAsPrefabAsset(source.gameObject,PrefabFolder+"/Window_"+type+".prefab");
            }
            AssetDatabase.SaveAssets();
            Selection.activeGameObject=go;
            Debug.Log("Paw Arena UI Kit: demo scene generated. Press Play and click HUD buttons.");
        }
    }
}
#endif
