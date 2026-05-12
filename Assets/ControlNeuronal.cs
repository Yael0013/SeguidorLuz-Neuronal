using UnityEngine;
using System.IO.Ports;
using Unity.InferenceEngine;
using TMPro;

public class ControlNeuronal : MonoBehaviour
{
    [Header("Configuración IA")]
    public ModelAsset modeloONNX; 
    private Worker motorInferencia;

    [Header("Configuración Serial")]
    public string puertoNombre = "COM7";
    private SerialPort puertoSerie;

    [Header("UI y Debug")]
    public TMP_Text textoLDR1;
    public TMP_Text textoLDR2;
    public TMP_Text textoDecision;

    [Header("Estabilización de Control (NUEVO)")]
    [Tooltip("Qué tan suave se mueve (0.01 muy lento, 1.0 sin filtro)")]
    public float factorSuavizado = 0.1f;
    [Tooltip("Cambio mínimo para mover el motor (Evita micro-saltos)")]
    public float umbralCambio = 0.03f;

    // Variables internas para la memoria del filtro
    private float decisionSuavizada = 0f;
    private float ultimaDecisionEnviada = -999f;

    void Start()
    {
        Model modeloCargado = ModelLoader.Load(modeloONNX);
        motorInferencia = new Worker(modeloCargado, BackendType.GPUCompute);

        puertoSerie = new SerialPort(puertoNombre, 115200);
        puertoSerie.ReadTimeout = 20;
        try { puertoSerie.Open(); } catch { }
    }

    void Update()
    {
        if (puertoSerie != null && puertoSerie.IsOpen)
        {
            try
            {
                string datos = puertoSerie.ReadLine();
                string[] valores = datos.Split(',');

                if (valores.Length == 2)
                {
                    float ldr1 = float.Parse(valores[0]) / 4095.0f;
                    float ldr2 = float.Parse(valores[1]) / 4095.0f;

                    textoLDR1.text = $"LDR 1: {ldr1:F2}";
                    textoLDR2.text = $"LDR 2: {ldr2:F2}";

                    using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 2), new float[] { ldr1, ldr2 });
                    motorInferencia.Schedule(inputTensor);

                    Tensor<float> outputTensor = motorInferencia.PeekOutput() as Tensor<float>;
                    using var cpuOutput = outputTensor.ReadbackAndClone();
                    float decisionBruta = cpuOutput[0];

                    // --- 1. FILTRO PASA BAJAS (Interpolación Lineal) ---
                    // Esto promedia la lectura nueva con las lecturas anteriores. 
                    // Elimina los picos erráticos de ruido eléctrico.
                    decisionSuavizada = Mathf.Lerp(decisionSuavizada, decisionBruta, factorSuavizado);

                    textoDecision.text = $"Decisión Neurona: {decisionSuavizada:F2}";

                    // --- 2. BANDA MUERTA (Deadband) ---
                    // Solo "despertamos" al puerto serial si el cambio matemático 
                    // es lo suficientemente grande como para justificar un movimiento físico.
                    if (Mathf.Abs(decisionSuavizada - ultimaDecisionEnviada) > umbralCambio)
                    {
                        puertoSerie.Write(decisionSuavizada.ToString("F2") + "\n");
                        ultimaDecisionEnviada = decisionSuavizada;
                    }
                }
            }
            catch (System.TimeoutException) { }
        }
    }

    void OnDisable()
    {
        motorInferencia?.Dispose(); 
        if (puertoSerie != null && puertoSerie.IsOpen) puertoSerie.Close();
    }
}