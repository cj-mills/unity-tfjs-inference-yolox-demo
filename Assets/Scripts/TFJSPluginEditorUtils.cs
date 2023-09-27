using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;

[InitializeOnLoad]
public class Startup
{
    // A helper class that stores the name and file path for a TensorFlow.js model
    [System.Serializable]
    class ModelData
    {
        public string name;
        public string path;

        public ModelData(string name, string path)
        {
            this.name = name;
            this.path = path;
        }
    }

    // A helper class that stores a list of TensorFlow.js model names and file paths
    [System.Serializable]
    class ModelList
    {
        public List<ModelData> models;

        public ModelList(List<ModelData> models)
        {
            this.models = models;
        }
    }

    static Startup()
    {
        string tfjsModelsDir = "TFJSModels";
        List<ModelData> models = new List<ModelData>();

        Debug.Log("Available models");
        // Get the paths for each model folder
        foreach (string dir in Directory.GetDirectories($"{Application.streamingAssetsPath}/{tfjsModelsDir}"))
        {
            string dirStr = dir.Replace("\\", "/");
            // Extract the model folder name
            string[] splits = dirStr.Split('/');
            string modelName = splits[splits.Length - 1];

            // Get the paths for the model.json file for each model
            foreach (string file in Directory.GetFiles(dirStr))
            {
                if (file.EndsWith("model.json"))
                {
                    string fileStr = file.Replace("\\", "/").Replace(Application.streamingAssetsPath, "");
                    models.Add(new ModelData(modelName, fileStr));
                }
            }
        }

        ModelList modelList = new ModelList(models);
        // Format the list of available models as a string in JSON format
        string json = JsonUtility.ToJson(modelList);
        Debug.Log($"Model List JSON: {json}");
        // Write the list of available TensorFlow.js models to a JSON file
        using StreamWriter writer = new StreamWriter($"{Application.streamingAssetsPath}/models.json");
        writer.Write(json);
    }
}
#endif
