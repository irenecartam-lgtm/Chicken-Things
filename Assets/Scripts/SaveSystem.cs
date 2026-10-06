using UnityEngine;
using System.IO;

public static class SaveSystem
{
    private static string SavePath {
        get { return Path.Combine(Application.persistentDataPath, "savegame.json"); }
    
    }

    public static void Save(SaveData data) {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log("Gamefile saved in:" + SavePath);
    }

    public static SaveData Load() {
        if (!File.Exists(SavePath)) {
            Debug.Log("No saved files");
            return null;
        }

        string json = File.ReadAllText(SavePath);
        return JsonUtility.FromJson<SaveData>(json);
    }

    public static bool HasSave() {
        return File.Exists(SavePath);
    }

    public static void Delete() {
        if (File.Exists(SavePath)) {
            File.Delete(SavePath);
        
        }
    
    }

}
