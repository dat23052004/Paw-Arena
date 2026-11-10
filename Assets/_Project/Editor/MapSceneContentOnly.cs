using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PawArena.Editor
{
    /// <summary>Removes presentation/gameplay scaffolding from the explicitly scoped map scenes.</summary>
    public static class MapSceneContentOnly
    {
        /// <summary>Batch cleanup with recoverable scene backups; map prefab contents and overrides are preserved.</summary>
        public static void CleanImportedMaps()
        {
            if (!Application.isBatchMode) { throw new InvalidOperationException("Run with the interactive editor closed."); }
            var entries = new[]
            {
                (Scene: "BasePlate", Map: "BasePlate"),
                (Scene: "BoxingRing", Map: "BoxingRing"),
                (Scene: "Cauldron_ScalePreview", Map: "Cauldron"),
                (Scene: "Cauldron_WaterPreview", Map: "Cauldron")
            };
            var backup = "Blender/Map/Backups/BeforeMapOnlyScenes_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
            Directory.CreateDirectory(backup);
            var report = new StringBuilder("MAP_ONLY_SCENES\nBackup: " + backup + "\n");
            foreach (var entry in entries)
            {
                var path = "Assets/_Project/Scenes/Maps/" + entry.Scene + ".unity";
                var prefabPath = $"Assets/_Project/Prefabs/Maps/{entry.Map}/PF_Map_{entry.Map}.prefab";
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var maps = roots.Where(root => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == prefabPath).ToArray();
                if (maps.Length != 1) { throw new InvalidOperationException($"Expected one {prefabPath} in {path}; nothing removed."); }
                var map = maps[0];
                // Do not guess about unexpected content inside the authored map prefab.
                if (map.GetComponentsInChildren<Camera>(true).Length != 0 || map.GetComponentsInChildren<Canvas>(true).Length != 0)
                { throw new InvalidOperationException($"Camera/UI found inside {prefabPath}; inspect the prefab before cleanup."); }
                foreach (var suffix in new[] { "", ".meta" })
                { File.Copy(path + suffix, backup + "/" + entry.Scene + ".unity" + suffix); }
                var removed = roots.Where(root => root != map).Select(root => root.name).ToArray();
                foreach (var root in roots)
                {
                    if (root != map) { Undo.DestroyObjectImmediate(root); }
                }
                RenderSettings.sun = null;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) { throw new IOException("Could not save " + path); }
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var savedRoots = scene.GetRootGameObjects();
                if (savedRoots.Length != 1 || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(savedRoots[0]) != prefabPath)
                { throw new InvalidOperationException("Saved scene is not map-only: " + path); }
                report.AppendLine($"{entry.Scene}: 1 map root; removed [{string.Join(", ", removed)}]; " +
                    $"cameras={savedRoots[0].GetComponentsInChildren<Camera>(true).Length}, " +
                    $"canvases={savedRoots[0].GetComponentsInChildren<Canvas>(true).Length}, " +
                    $"lights={savedRoots[0].GetComponentsInChildren<Light>(true).Length}.");
            }
            report.AppendLine("Map prefab assets untouched. No Main scene changes, tests, Play Mode, commits or pushes.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/MapOnlyScenes.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }
}
