using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public static class TFJSPluginUtils// : MonoBehaviour
{

    ///// <summary>
    ///// Get the names and paths of the available TensorFlow.js models
    ///// </summary>
    ///// <param name="json"></param>
    //void GetTFJSModels(string json)
    //{
    //    ModelList modelList = JsonUtility.FromJson<ModelList>(json);
    //    foreach (ModelData model in modelList.models)
    //    {
    //        //Debug.Log($"{model.name}: {model.path}");
    //        modelNames.Add(model.name);
    //        string path = $"{Application.streamingAssetsPath}{model.path}";
    //        modelPaths.Add(path);
    //    }
    //    // Remove default dropdown options
    //    modelDropdown.ClearOptions();
    //    // Add TFJS model names to menu
    //    modelDropdown.AddOptions(modelNames);
    //    // Select the first option in the dropdown
    //    modelDropdown.SetValueWithoutNotify(0);
    //}

    /// <summary>
    /// Download the JSON file with the available TFJS model information
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="onSuccess"></param>
    /// <returns></returns>
    public static IEnumerator GetRequest(string uri, Action<string> onSuccess)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            string[] pages = uri.Split('/');
            int page = pages.Length - 1;

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError(pages[page] + ": Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError(pages[page] + ": HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    Debug.Log(pages[page] + ":\nReceived: " + webRequest.downloadHandler.text);

                    onSuccess?.Invoke(webRequest.downloadHandler.text);
                    break;
            }
        }
    }

    //void Start() // Just an example usage, you can use it differently
    //{
    //    StartCoroutine(GetRequest("yourURLhere", (resultText) =>
    //    {
    //        // Do something with resultText
    //        Debug.Log("Received Text: " + resultText);
    //    }));
    //}
}
