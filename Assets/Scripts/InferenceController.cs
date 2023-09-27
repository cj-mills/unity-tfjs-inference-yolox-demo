using System.Linq;
using UnityEngine;
using CJM.BBox2DToolkit;
using CJM.DeepLearningImageProcessor;
using System.Collections.Generic;
using CJM.YOLOXUtils;
using System;

public class InferenceController : MonoBehaviour
{
    #region Fields

    // Components
    [Header("Components")]
    [SerializeField, Tooltip("Responsible for image preprocessing")]
    private ImageProcessor imageProcessor;
    [SerializeField, Tooltip("Manages user interface updates")]
    private UIController uiController;
    [SerializeField, Tooltip("Visualizes detected object bounding boxes")]
    private BoundingBox2DVisualizer boundingBoxVisualizer;
    [SerializeField, Tooltip("Renders the input image on a screen")]
    private MeshRenderer screenRenderer;
    // JSON file containing the color map for bounding boxes
    [SerializeField, Tooltip("JSON file with bounding box colormaps")]
    private TextAsset colormapFile;

    [Header("Data Processing")]
    [Tooltip("The target dimensions for the processed image")]
    [SerializeField] private int targetDim = 224;
    [Tooltip("Flag to use compute shaders for processing input images.")]
    [SerializeField] private bool useComputeShaders = false;
    [Tooltip("Flag to normalize input images before passing them to the model.")]
    [SerializeField] private bool normalizeInput = false;

    // Output processing settings
    [Header("Output Processing")]
    [SerializeField, Tooltip("Minimum confidence score for an object proposal to be considered"), Range(0, 1)]
    private float confidenceThreshold = 0.5f;
    [SerializeField, Tooltip("Threshold for Non-Maximum Suppression (NMS)"), Range(0, 1)]
    private float nmsThreshold = 0.45f;

    // Runtime variables
    private BBox2DInfo[] bboxInfoArray; // Array to store bounding box information
    private bool mirrorScreen = false; // Flag to check if the screen is mirrored
    private Vector2Int offset; // Offset used when cropping the input image
        
    // List to store label and color pairs for each class
    private List<(string, Color)> colormapList = new List<(string, Color)>();

    // List to store grid and stride information for the YOLOX model
    private List<GridCoordinateAndStride> gridCoordsAndStrides = new List<GridCoordinateAndStride>();

    // Length of the proposal array for YOLOX output
    private int proposalLength;

    // A helper class to store the name and file path of a TensorFlow.js model
    [System.Serializable]
    class ModelData { public string name; public string path; }
    // A helper class to store a read a list of available TensorFlow.js models from a JSON file
    [System.Serializable]
    class ModelList { public List<ModelData> models; }

    // Names of the available TFJS backends
    List<string> tfjsBackends = new List<string> { "webgl" };


    ModelList modelList;

    // The model CPU input texture
    Texture2D inputTextureCPU;


    float[] outputArray;

    // Stores whether the TensorFlow.js model is ready for inference
    bool modelInitialized;


    #endregion

    #region MonoBehaviour Methods

    // Awake is called when the script instance is being loaded
    void Awake()
    {
        WebGLPlugin.GetExternalJS();
    }

    private void Start()
    {
        colormapList = ColormapUtility.LoadColorMapList(colormapFile); // Load colormap information from JSON file
        proposalLength = colormapList.Count + YOLOXConstants.NumBBoxFields; // Calculate proposal length
        
        // Get the available TensorFlow.js models
        string modelListPath = $"{Application.streamingAssetsPath}/models.json";
        StartCoroutine(TFJSPluginUtils.GetRequest(modelListPath, (json) =>
        {
            modelList = JsonUtility.FromJson<ModelList>(json);
            UpdateTFJSModel();
        }));

        // Update the current TensorFlow.js compute backend
        WebGLPlugin.SetTFJSBackend(tfjsBackends[0]);
    }


    /// <summary>
    /// Update the InferenceController every frame, processing the input image and updating the UI and bounding boxes.
    /// </summary>
    private void Update()
    {
        // Check if all required components are valid
        if (!AreComponentsValid()) return;

        // Get the input image and dimensions
        var imageTexture = screenRenderer.material.mainTexture;
        var imageDims = new Vector2Int(imageTexture.width, imageTexture.height);
        var inputDims = imageProcessor.CalculateInputDims(imageDims, targetDim);
                
        // Calculate source and input dimensions for model input
        var sourceDims = inputDims;
        inputDims = new Vector2Int(targetDim, targetDim);
        inputDims = YOLOXUtility.CropInputDims(inputDims);
        
        // Prepare and process the input texture
        RenderTexture inputTexture = PrepareInputTexture(inputDims);
        ProcessInputImage(inputTexture, imageTexture, sourceDims, inputDims);


        if (!inputTextureCPU || inputTextureCPU.width != inputTexture.width || inputTextureCPU.height != inputTexture.height)
        {
            inputTextureCPU = new Texture2D(inputDims.x, inputDims.y, TextureFormat.RGB24, false);
            gridCoordsAndStrides = YOLOXUtility.GenerateGridCoordinatesWithStrides(YOLOXConstants.Strides, inputTexture.height, inputTexture.width);

            int output_size = gridCoordsAndStrides.Count * proposalLength;
            outputArray = new float[output_size];
            WebGLPlugin.UpdateOutputArray(outputArray, output_size);
            Debug.Log($"Updating output array to {output_size}");
            Debug.Log($"Input Dims: {inputTextureCPU.width}x{inputTextureCPU.height}");
        }

        // Download pixel data from GPU to CPU
        RenderTexture.active = inputTexture;
        inputTextureCPU.ReadPixels(new Rect(0, 0, inputTexture.width, inputTexture.height), 0, 0);
        inputTextureCPU.Apply();

        // Get the current input dimensions
        int width = inputTextureCPU.width;
        int height = inputTextureCPU.height;
        int size = width * height * 3;

        // Pass the input data to the plugin to perform inference
        modelInitialized = WebGLPlugin.PerformInference(inputTextureCPU.GetRawTextureData(), size, width, height);


        if (modelInitialized == false) return;

        RenderTexture.ReleaseTemporary(inputTexture);
        
        // Generate bounding box proposals from the output array
        List<BBox2D> proposals = YOLOXUtility.GenerateBoundingBoxProposals(outputArray,
                                                                           gridCoordsAndStrides,
                                                                           colormapList.Count,
                                                                           YOLOXConstants.NumBBoxFields,
                                                                           confidenceThreshold);
        // Apply Non-Maximum Suppression (NMS) to the proposals
        List<int> proposalIndices = BBox2DUtility.NMSSortedBoxes(proposals, nmsThreshold);
        // Create an array of BBox2DInfo objects containing the filtered bounding boxes, class labels, and colors
        bboxInfoArray = YOLOXUtility.GetBBox2DInfos(proposals, proposalIndices, colormapList);

        // Update bounding boxes and user interface
        UpdateBoundingBoxes(inputDims);
        uiController.UpdateUI(bboxInfoArray.Length);
        boundingBoxVisualizer.UpdateBoundingBoxVisualizations(bboxInfoArray);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Check if all required components are assigned and valid.
    /// </summary>
    /// <returns>True if all components are valid, false otherwise</returns>
    private bool AreComponentsValid()
    {
        if (imageProcessor == null || uiController == null || boundingBoxVisualizer == null)
        {
            Debug.LogError("InferenceController requires ImageProcessor, ModelRunner, and InferenceUI components.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Prepare a temporary RenderTexture with the given input dimensions.
    /// </summary>
    /// <param name="inputDims">The input dimensions for the RenderTexture</param>
    /// <returns>A temporary RenderTexture with the specified input dimensions</returns>
    private RenderTexture PrepareInputTexture(Vector2Int inputDims)
    {
        return RenderTexture.GetTemporary(inputDims.x, inputDims.y, 0, RenderTextureFormat.ARGBHalf);
    }

    /// <summary>
    /// Process the input image and apply necessary transformations.
    /// </summary>
    /// <param name="inputTexture">The input RenderTexture to process</param>
    /// <param name="imageTexture">The source image texture</param>
    /// <param name="sourceDims">The source image dimensions</param>
    /// <param name="inputDims">The input dimensions for processing</param>
    private void ProcessInputImage(RenderTexture inputTexture, Texture imageTexture, Vector2Int sourceDims, Vector2Int inputDims)
    {
        // Calculate the offset for cropping the input image
        offset = (sourceDims - inputDims) / 2;

        // Create a temporary render texture to store the cropped image
        RenderTexture sourceTexture = RenderTexture.GetTemporary(sourceDims.x, sourceDims.y, 0, RenderTextureFormat.ARGBHalf);
        Graphics.Blit(imageTexture, sourceTexture);

        // Crop and normalize the input image using Compute Shaders or fallback to Shader processing
        if (SystemInfo.supportsComputeShaders && useComputeShaders)
        {
            imageProcessor.CropImageComputeShader(sourceTexture, inputTexture, offset, inputDims);
            if (normalizeInput) imageProcessor.ProcessImageComputeShader(inputTexture, "NormalizeImage");
        }
        else
        {
            ProcessImageShader(sourceTexture, inputTexture, sourceDims, inputDims);
        }

        // Release the temporary render texture
        RenderTexture.ReleaseTemporary(sourceTexture);
    }

    /// <summary>
    /// Process the input image using Shaders when Compute Shaders are not supported.
    /// </summary>
    /// <param name="sourceTexture">The source image RenderTexture</param>
    /// <param name="inputTexture">The input RenderTexture to process</param>
    /// <param name="sourceDims">The source image dimensions</param>
    /// <param name="inputDims">The input dimensions for processing</param>
    private void ProcessImageShader(RenderTexture sourceTexture, RenderTexture inputTexture, Vector2Int sourceDims, Vector2Int inputDims)
    {
        // Calculate the scaled offset and size for cropping the input image
        Vector2 scaledOffset = offset / (Vector2)sourceDims;
        Vector2 scaledSize = inputDims / (Vector2)sourceDims;

        // Create offset and size arrays for the Shader
        float[] offsetArray = new float[] { scaledOffset.x, scaledOffset.y };
        float[] sizeArray = new float[] { scaledSize.x, scaledSize.y };

        // Crop and normalize the input image using Shaders
        imageProcessor.CropImageShader(sourceTexture, inputTexture, offsetArray, sizeArray);
        if (normalizeInput) imageProcessor.ProcessImageShader(inputTexture);
    }

    /// <summary>
    /// Update the bounding boxes based on the input dimensions and screen dimensions.
    /// </summary>
    /// <param name="inputDims">The input dimensions for processing</param>
    private void UpdateBoundingBoxes(Vector2Int inputDims)
    {
        // Check if the screen is mirrored
        mirrorScreen = screenRenderer.transform.localScale.z == -1;

        // Get the screen dimensions
        Vector2 screenDims = new Vector2(screenRenderer.transform.localScale.x, screenRenderer.transform.localScale.y);

        // Scale and position the bounding boxes based on the input and screen dimensions
        for (int i = 0; i < bboxInfoArray.Length; i++)
        {
            bboxInfoArray[i].bbox = BBox2DUtility.ScaleBoundingBox(bboxInfoArray[i].bbox, inputDims, screenDims, offset, mirrorScreen);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Load a TensorFlow.js model
    /// </summary>
    public void UpdateTFJSModel()
    {
        string modelPath = $"{Application.streamingAssetsPath}{modelList.models[0].path}";

        // Load TensorFlow.js model in JavaScript plugin
        WebGLPlugin.InitTFJSModel(modelPath);
    }


    /// <summary>
    /// Update the confidence threshold for object detection.
    /// </summary>
    /// <param name="value">The new confidence threshold value</param>
    public void UpdateConfidenceThreshold(float value)
    {
        confidenceThreshold = value;
    }

    #endregion
}
