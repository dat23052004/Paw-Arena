using System;
using System.IO;
using System.Linq;
using PawArena.Core.Inventory;
using PawArena.Gameplay.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PawArena.Editor.Inventory
{
    /// <summary>Refreshes generated meshes, fits the room camera and validates the actual saved room and equipment.</summary>
    public static class CatGearVerification
    {
        private const string Request = "Library/CatGearVerify.request";
        [InitializeOnLoadMethod]
        private static void Schedule() => EditorApplication.delayCall += Run;
        private static void Run()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "verify") { return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += Run; return; }
            File.WriteAllText(Request, "running");
            try
            {
                var library = AssetDatabase.LoadAssetAtPath<CatGearLibrary>(CatGearInventoryBuilder.Folder + "/CatGearLibrary.asset");
                foreach (var item in library.Gear)
                {
                    var slug = item.ItemId.Substring("gear_".Length);
                    var path = AssetDatabase.GetAssetPath(item.Mesh);
                    var fbxPath = Path.GetDirectoryName(path).Replace('\\', '/') + "/SK_Gear_" + item.Mesh.name.Substring("MSH_Gear_".Length) + ".fbx";
                    AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceSynchronousImport);
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath).GetComponentInChildren<SkinnedMeshRenderer>();
                    var copy = UnityEngine.Object.Instantiate(source.sharedMesh);
                    var triangles = copy.triangles;
                    copy.subMeshCount = 1; copy.SetTriangles(triangles, 0); copy.name = item.Mesh.name;
                    Undo.RecordObject(item.Mesh, "Update gear weight corrections");
                    EditorUtility.CopySerialized(copy, item.Mesh);
                    EditorUtility.SetDirty(item.Mesh);
                    UnityEngine.Object.DestroyImmediate(copy);
                    if (!item.IsValid) { throw new InvalidOperationException("Invalid corrected gear: " + slug); }
                }
                CorrectRoomCamera();
                AssetDatabase.SaveAssets();
                LocalSkinInventoryTestRunner.Run();
                File.WriteAllText(Request, File.ReadAllText("Library/LocalSkinInventoryChecks.request"));
            }
            catch (Exception exception) { File.WriteAllText(Request, "failed: " + exception); Debug.LogException(exception); }
        }

        private static void CorrectRoomCamera()
        {
            const string roomPath = "Assets/_Project/Prefabs/UI/PF_LocalSkinInventoryRoom.prefab";
            const string backup = "Blender/Backups/BeforeGearCameraCorrection.prefab";
            if (!File.Exists(backup)) { File.Copy(roomPath, backup); }
            var room = PrefabUtility.LoadPrefabContents(roomPath);
            try
            {
                var camera = room.GetComponentInChildren<Camera>();
                Undo.RecordObject(camera.transform, "Show cat face in inventory preview");
                camera.transform.position = new Vector3(.8f, 1.4f, 5);
                camera.transform.LookAt(new Vector3(0, 1.2f, 0));
                foreach (var light in room.GetComponentsInChildren<Light>())
                {
                    Undo.RecordObject(light, "Balance inventory preview lighting");
                    Undo.RecordObject(light.transform, "Light the front of the cat");
                    var key = light.name.Contains("Key");
                    light.intensity = key ? 1.15f : .65f;
                    light.transform.rotation = Quaternion.Euler(key ? 35 : 15, key ? 145 : -35, 0);
                }
                PrefabUtility.SaveAsPrefabAsset(room, roomPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }
    }
}
