using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LADIDSceneBuilder
{
    [MenuItem("LADID/Create InPlay Scene")]
    public static void CreateInPlayScene()
    {
        const string path="Assets/Scenes/InPlay.unity";
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("InPlay Game").AddComponent<LADIDGame>();
        EditorSceneManager.SaveScene(scene,path);
        EnsureBuildScene(path,false);
        Debug.Log("InPlay scene created. Press Play to start.");
    }

    [MenuItem("LADID/Create Main Menu Scene")]
    public static void CreateMainMenuScene()
    {
        const string path="Assets/Scenes/MainMenu.unity";
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Main Menu").AddComponent<LADIDMainMenu>();
        EditorSceneManager.SaveScene(scene,path);
        EnsureBuildScene(path,true);
        Debug.Log("MainMenu scene created. Start the game from this scene.");
    }

    static void EnsureBuildScene(string path,bool first)
    {
        var scenes=new List<EditorBuildSettingsScene>();
        if(first)scenes.Add(new EditorBuildSettingsScene(path,true));
        bool found=first;
        foreach(var item in EditorBuildSettings.scenes)
        {
            if(item.path==path){if(!found)scenes.Add(new EditorBuildSettingsScene(path,true));found=true;continue;}
            scenes.Add(item);
        }
        if(!found)scenes.Add(new EditorBuildSettingsScene(path,true));
        EditorBuildSettings.scenes=scenes.ToArray();
    }
}
