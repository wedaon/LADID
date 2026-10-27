using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LADIDSceneBuilder
{
    [MenuItem("LADID/Create InPlay Scene")]
    public static void CreateScene()
    {
        const string path = "Assets/Scenes/InPlay.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("InPlay Game").AddComponent<LADIDGame>();
        EditorSceneManager.SaveScene(scene, path);
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
        foreach (var item in EditorBuildSettings.scenes) if (item.path != path) scenes.Add(item);
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("InPlay scene created. Press Play to start.");
    }
}
