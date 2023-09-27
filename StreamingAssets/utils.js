// Perform inference with the provided model and input data
async function PerformInferenceAsync(model, float32Data, shape) {

    let outputData = tf.tidy(() => {
        let input_tensor = tf.tensor(float32Data, shape, 'float32');
        // Make a prediction.
        return model.predict(input_tensor);
    });
    return await outputData.data();
}