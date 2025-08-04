using UnityEngine;
using Microsoft.ML.OnnxRuntime;

public class OnnxRuntimeTest : MonoBehaviour
{
    void Start()
    {
        try
        {
            var session = new InferenceSession("Assets/Models/ppo_durak.onnx");
            Debug.Log("ONNX Runtime loaded successfully.");
            session.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ONNX Runtime load failed: {e.Message}");
        }
    }
}