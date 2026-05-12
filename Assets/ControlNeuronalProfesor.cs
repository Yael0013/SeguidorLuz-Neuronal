using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO.Ports;
using TMPro;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

public class ControlNeuronalProfesor : MonoBehaviour
{
    [Header("Configuración IA (Método Microsoft)")]
    [Tooltip("Escribe el nombre de tu archivo con la extensión, ej: neurona_seguidor.onnx")]
    public string nombreArchivoModelo = "neurona_seguidor.onnx";
    private InferenceSession sesionONNX;
    private int[] formaTensor = new int[] { 1, 2 }; // 1 fila, 2 columnas (LDR1 y LDR2)

    [Header("Configuración Serial")]
    public string puertoNombre = "COM7";
    private SerialPort puertoSerie;

    [Header("UI y Debug")]
    public TMP_Text textoLDR1;
    public TMP_Text textoLDR2;
    public TMP_Text textoDecision;

    void Start()
    {
        // 1. Cargar el modelo usando la librería de Microsoft
        // En este método, Unity necesita saber la ruta exacta del archivo en tu disco duro
        string rutaCompleta = Application.dataPath + "/" + nombreArchivoModelo;
        
        try
        {
            sesionONNX = new InferenceSession(rutaCompleta, new SessionOptions());
            Debug.Log("Modelo ONNX de Microsoft cargado correctamente.");
        }
        catch (Exception e)
        {
            Debug.LogError("Error al cargar ONNX: " + e.Message);
        }

        // 2. Abrir Puerto Serial
        puertoSerie = new SerialPort(puertoNombre, 115200);
        puertoSerie.ReadTimeout = 20;
        try { puertoSerie.Open(); } catch { }
    }

    void Update()
    {
        if (puertoSerie != null && puertoSerie.IsOpen && sesionONNX != null)
        {
            try
            {
                string datos = puertoSerie.ReadLine();
                string[] valores = datos.Split(',');

                if (valores.Length == 2)
                {
                    // 3. Normalizar
                    float ldr1 = float.Parse(valores[0]) / 4095.0f;
                    float ldr2 = float.Parse(valores[1]) / 4095.0f;

                    textoLDR1.text = $"LDR 1: {ldr1:F2}";
                    textoLDR2.text = $"LDR 2: {ldr2:F2}";

                    // 4. Inferencia con Microsoft ONNX Runtime
                    float decision = CalculaUsandoONNX(ldr1, ldr2);

                    textoDecision.text = $"Decisión Neurona: {decision:F2}";

                    // 5. Enviar a ESP32
                    puertoSerie.Write(decision.ToString("F2") + "\n");
                }
            }
            catch (TimeoutException) { }
        }
    }

    // Esta es la función adaptada del script "BoyDebug" de tu profesor
    private float CalculaUsandoONNX(float ldr1, float ldr2)
    {
        // Preparamos los datos
        float[] entradas = new float[] { ldr1, ldr2 };
        var inputTensor = new DenseTensor<float>(entradas, formaTensor);
        
        // Empaquetamos para Microsoft ML
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor<float>(sesionONNX.InputMetadata.Keys.First(), inputTensor)
        };

        // Ejecutamos la neurona
        using var resultados = sesionONNX.Run(inputs);

        // Extraemos el resultado matemático
        var output = resultados.FirstOrDefault();
        if (output != null)
        {
            var outputTensor = output.AsTensor<float>();
            float[] salidas = outputTensor.ToArray();
            return salidas[0]; // Retorna el valor entre -1.0 y 1.0 para el servo
        }
        
        return 0f; // Si hay error, se queda quieto
    }

    void OnDisable()
    {
        sesionONNX?.Dispose();
        if (puertoSerie != null && puertoSerie.IsOpen) puertoSerie.Close();
    }
}