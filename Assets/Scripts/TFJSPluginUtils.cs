using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public static class TFJSPluginUtils// : MonoBehaviour
{

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
}
