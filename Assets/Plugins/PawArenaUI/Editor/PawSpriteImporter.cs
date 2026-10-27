#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace PawArenaUI.Editor
{
    public class PawSpriteImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/PawArenaUI/Sprites/") || !assetPath.EndsWith(".png")) return;
            var t = (TextureImporter) assetImporter;
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            t.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            t.SetTextureSettings(settings);
            t.alphaIsTransparency = true;
            t.mipmapEnabled = false;
            t.filterMode = FilterMode.Bilinear;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.spritePixelsPerUnit = 100;
            string filename = Path.GetFileNameWithoutExtension(assetPath);
            int border = 0;
            if(assetPath.Contains("/Panels/"))
            {
                if(filename == "emote_wheel_base") border=0;
                else if(filename == "window_panel") border=34;
                else if(filename == "content_panel") border=26;
                else if(filename == "sidebar_panel" || filename == "toast_panel") border=22;
                else if(filename == "stat_panel") border=24;
                else if(filename == "quest_row") border=23;
                else border=18;
            }
            else if(assetPath.Contains("/Buttons/"))
                border=filename=="close_red" ? 23 : filename=="arrow_blue" ? 20 : filename.StartsWith("tab_") ? 18 : 22;
            else if(assetPath.Contains("/Cards/"))
                border=filename.StartsWith("reward_") ? 25 : 22;
            else if(assetPath.Contains("/Badges/"))
                border=filename.StartsWith("toggle_") ? 32 : filename.StartsWith("slider_") ? 20 : filename=="coin_counter" ? 21 : 15;
            t.spriteBorder = new Vector4(border,border,border,border);
        }
    }
}
#endif
