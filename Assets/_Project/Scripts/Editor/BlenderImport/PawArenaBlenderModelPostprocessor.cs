using System;
using UnityEditor;

namespace PawArena.Editor.BlenderImport
{
    /// <summary>
    /// Keeps the Unity importer from applying a second axis conversion.
    /// Blender already bakes Z-up to Unity Y-up into these static FBX meshes on export.
    /// </summary>
    internal sealed class PawArenaBlenderModelPostprocessor : AssetPostprocessor
    {
        private const string ModelFolderPrefix = "Assets/_Game/Models/Environment/BlenderExports/";

        public override uint GetVersion()
        {
            return 2;
        }

        private void OnPreprocessModel()
        {
            if (!IsPawArenaModel(assetPath))
            {
                return;
            }

            ApplySettings((ModelImporter)assetImporter);
        }

        internal static bool ApplySettings(ModelImporter importer)
        {
            var changed = false;
            changed |= SetIfDifferent(importer.bakeAxisConversion, false, value => importer.bakeAxisConversion = value);
            changed |= SetIfDifferent(importer.globalScale, 1f, value => importer.globalScale = value);
            changed |= SetIfDifferent(importer.useFileUnits, true, value => importer.useFileUnits = value);
            changed |= SetIfDifferent(importer.useFileScale, true, value => importer.useFileScale = value);
            changed |= SetIfDifferent(importer.importAnimation, false, value => importer.importAnimation = value);
            changed |= SetIfDifferent(importer.importCameras, false, value => importer.importCameras = value);
            changed |= SetIfDifferent(importer.importLights, false, value => importer.importLights = value);
            return changed;
        }

        internal static bool IsPawArenaModel(string path)
        {
            return path.StartsWith(ModelFolderPrefix, StringComparison.OrdinalIgnoreCase) &&
                   path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SetIfDifferent(bool current, bool desired, Action<bool> setter)
        {
            if (current == desired)
            {
                return false;
            }

            setter(desired);
            return true;
        }

        private static bool SetIfDifferent(float current, float desired, Action<float> setter)
        {
            if (Math.Abs(current - desired) <= 0.0001f)
            {
                return false;
            }

            setter(desired);
            return true;
        }
    }
}
