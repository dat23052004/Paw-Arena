using System;

namespace PawArenaUI
{
    [Serializable]
    public class PawItem
    {
        public string name, icon, rarity, description;
        public int price;
        public bool owned;
        public PawItem(string name, string icon, string rarity, string description, int price, bool owned)
        { this.name=name; this.icon=icon; this.rarity=rarity; this.description=description; this.price=price; this.owned=owned; }
    }

    public static class PawCatalog
    {
        public static readonly string[] Categories = { "Furs", "Abilities", "Gear", "Effects", "Emotes" };
        public static PawItem[][] CreateItems()
        {
            return new [] {
                new [] {
                    new PawItem("Ginger", "fur_ginger", "Common", "A sunny little arena fighter.",0,true),
                    new PawItem("Pumpkin", "fur_pumpkin", "Rare", "A playful seasonal costume.",0,true),
                    new PawItem("Black Widow", "fur_black_widow", "Epic", "Eight legs. Maybe four.",0,true),
                    new PawItem("Grey", "fur_grey", "Common", "Classic soft grey fur.",250,false),
                    new PawItem("White", "fur_white", "Common", "Bright fur for bold cats.",750,false),
                    new PawItem("Calico", "fur_calico", "Uncommon", "A lucky patchwork friend.",2000,false),
                    new PawItem("Tuxedo", "fur_black_widow", "Rare", "Dress for the winning ring.",2000,false),
                    new PawItem("Snow", "fur_white", "Rare", "Cool-headed, even in combat.",5000,false),
                },
                new [] {
                    new PawItem("Dash", "icon_dash", "Common", "Dodge away. Briefly evade hits.",0,true),
                    new PawItem("Nine Lives", "icon_ninelives", "Rare", "A chance to survive the impossible.",0,true),
                    new PawItem("Pounce", "icon_pounce", "Common", "Leap toward your opponent.",750,false),
                    new PawItem("Anchor", "icon_anchor", "Uncommon", "Stay grounded for a moment.",2000,false),
                    new PawItem("Stomp", "icon_stomp", "Uncommon", "Make a tiny shockwave.",2000,false),
                    new PawItem("Fury", "icon_flame", "Epic", "Power up for a short burst.",5000,false),
                },
                new [] {
                    new PawItem("Hachimaki", "icon_headband", "Common", "A headband for focus.",0,true),
                    new PawItem("Champion", "icon_crown", "Rare", "Wear the winner's crown.",750,false),
                    new PawItem("Spark Wand", "icon_star_wand", "Uncommon", "Add a little magic.",2000,false),
                    new PawItem("Shield", "icon_shield", "Rare", "Look ready for battle.",2000,false),
                    new PawItem("Gift Box", "icon_present", "Epic", "Surprise your friends.",5000,false),
                },
                new [] {
                    new PawItem("Confetti", "icon_confetti", "Common", "Confetti for the winner.",0,true),
                    new PawItem("Dizzy Stars", "icon_star", "Common", "Stars over your head.",0,true),
                    new PawItem("Dust Puffs", "icon_poof", "Common", "Soft puffs when you move.",0,true),
                    new PawItem("Poof", "icon_poof", "Common", "Poof! There it goes.",0,true),
                    new PawItem("Sparks", "icon_sparks", "Rare", "Bright sparks on victory.",0,true),
                    new PawItem("Paw Prints", "icon_paw", "Common", "Leave a colorful trail.",200,false),
                    new PawItem("Fireworks", "icon_firework", "Epic", "Celebrate in the sky.",2000,false),
                    new PawItem("Fish Bones", "icon_fishbone", "Uncommon", "A fishy little trail.",300,false),
                },
                new [] {
                    new PawItem("Wave", "icon_wave", "Common", "Say hi to the ring.",0,true),
                    new PawItem("Stretch", "icon_stretch", "Common", "Warm up those paws.",0,true),
                    new PawItem("Beam Me Up", "icon_beam", "Epic", "Teleport in style.",0,true),
                    new PawItem("Lick Paw", "icon_paw", "Common", "Clean those paws.",750,false),
                    new PawItem("Nap Attack", "icon_heart", "Uncommon", "A well-earned rest.",2000,false),
                    new PawItem("Tea Ceremony", "icon_star_wand", "Rare", "Take a quiet moment.",2000,false),
                },
            };
        }
    }
}
