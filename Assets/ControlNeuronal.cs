using UnityEngine;
using System.IO.Ports;
using Unity.InferenceEngine;
using TMPro;
using UnityEngine.UI; // NUEVO: Necesario para controlar el Botón

public class ControlNeuronal : MonoBehaviour
{
    [Header("Configuración IA")]
    public ModelAsset modeloONNX; 
    private Worker motorInferencia;

    [Header("Configuración Serial (Por defecto)")]
    public string puertoNombre = "COM7";
    private SerialPort puertoSerie;

    [Header("UI y Debug")]
    public TMP_InputField inputPuertoUI; // NUEVO: La caja de texto para escribir el COM
    public Button botonConectarUI;       // NUEVO: El botón para iniciar
    public TMP_Text textoLDR1;
    public TMP_Text textoLDR2;
    public TMP_Text textoDecision;

    [Header("Estabilización de Control")]
    public float factorSuavizado = 0.1f;
    public float umbralCambio = 0.03f;

    private float decisionSuavizada = 0f;
    private float ultimaDecisionEnviada = -999f;

    void Start()
    {
        Model modeloCargado = ModelLoader.Load(modeloONNX);
        motorInferencia = new Worker(modeloCargado, BackendType.GPUCompute);

        // 1. Mostrar el puerto del Inspector en la caja de texto visual al iniciar
        if (inputPuertoUI != null)
        {
            inputPuertoUI.text = puertoNombre;
        }

        // 2. Conectar la función de encendido al botón por código
        if (botonConectarUI != null)
        {
            botonConectarUI.onClick.AddListener(ConectarSerial);
        }
        
        // ¡Se eliminó la apertura automática del puerto de aquí!
    }

    // NUEVO: Esta función solo se ejecuta al darle clic al botón
    public void ConectarSerial()
    {
        // Evitar que el usuario presione el botón dos veces por accidente
        if (puertoSerie != null && puertoSerie.IsOpen) return;

        // Leer lo que el usuario escribió en la interfaz
        if (inputPuertoUI != null && !string.IsNullOrEmpty(inputPuertoUI.text))
        {
            // .ToUpper() asegura que si escriben "com7", se corrija a "COM7"
            puertoNombre = inputPuertoUI.text.ToUpper(); 
        }

        puertoSerie = new SerialPort(puertoNombre, 115200);
        puertoSerie.ReadTimeout = 20;

        try 
        { 
            puertoSerie.Open(); 
            Debug.Log($"Conectado exitosamente al puerto: {puertoNombre}");
            
            // UX: Desactivar el botón y el input para indicar que ya se conectó
            botonConectarUI.interactable = false;
            inputPuertoUI.interactable = false;
        } 
        catch (System.Exception e)
        { 
            Debug.LogError($"Error al abrir puerto {puertoNombre}: " + e.Message); 
        }
    }

    void Update()
    {
        // El código interno se protege: Si el puerto no está abierto, ignora el Update
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

                    decisionSuavizada = Mathf.Lerp(decisionSuavizada, decisionBruta, factorSuavizado);
                    textoDecision.text = $"Decisión Neurona: {decisionSuavizada:F2}";

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