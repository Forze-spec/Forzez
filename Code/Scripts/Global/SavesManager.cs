using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

[Serializable]
public struct SaveContent {
    public string sceneId;
    public string checkpointId;
}

[Serializable]
public struct SaveMetadata {
    public string saveId;
    public string zoneName;
    public DateTime date;
}

[Serializable]
public struct SaveData {
    public SaveMetadata metadata;
    public SaveContent content;
}

public class SavesManager {
    private const string GameSavesFolder = "MisspellSaves";
    private const string MetadataFileName = "meta";
    private const string SavesFormat = "save";

    private static string _getSavePath(string name) {
        return Path.Combine(
            Application.persistentDataPath, GameSavesFolder, $"{name}.{SavesFormat}"
        );
    }

    public Dictionary<string, SaveMetadata> SavesMeta { get; }

    public SavesManager() {
        var savesDirPath = Path.Combine(Application.persistentDataPath, GameSavesFolder);
        if (!Directory.Exists(savesDirPath)) {
            Directory.CreateDirectory(savesDirPath);
            Debug.Log($"Created dir {savesDirPath}");
        }

        var metadataPath = _getSavePath(MetadataFileName);
        if (!File.Exists(metadataPath)) {
            SavesMeta = new Dictionary<string, SaveMetadata>();
            return;
        }

        BinaryFormatter bf = new BinaryFormatter();
        FileStream metadataStream = File.Open(metadataPath, FileMode.Open);
        SavesMeta = (Dictionary<string, SaveMetadata>)bf.Deserialize(metadataStream);
        metadataStream.Close();
    }

    public void Save(SaveData saveData) {
        var saveId = saveData.metadata.saveId;
        SavesMeta[saveId] = saveData.metadata;
        
        BinaryFormatter bf = new BinaryFormatter();

        var savePath = _getSavePath(saveId);
        FileStream fileStream = File.Open(savePath, FileMode.OpenOrCreate);
        bf.Serialize(fileStream, saveData.content);
        fileStream.Close();

        var metadataPath = _getSavePath(MetadataFileName);
        fileStream = File.Open(metadataPath, FileMode.OpenOrCreate);
        bf.Serialize(fileStream, SavesMeta);
        fileStream.Close();
    }

    public SaveData Load(string saveId) {
        var success = SavesMeta.TryGetValue(saveId, out var saveMetadata);
        if (!success) {
            throw new KeyNotFoundException($"Can't find save with id {saveId}");
        }

        var savePath = _getSavePath(saveId);
        if (!File.Exists(savePath)) {
            throw new FileNotFoundException(savePath);
        }
        
        BinaryFormatter bf = new BinaryFormatter();
        FileStream saveStream = File.Open(savePath, FileMode.Open);
        var saveContent = (SaveContent)bf.Deserialize(saveStream);
        saveStream.Close();
        return new SaveData {
            metadata = saveMetadata,
            content = saveContent
        };
    }

    public SaveContent GetInitSaveContent() {
        // TODO add init data (bosses death, shortcuts status, etc.)
        return new SaveContent {
            // first_boss_dead = false,
            // second_boss_dead = false,
            // third_boss_dead = false,
        };
    }
}
