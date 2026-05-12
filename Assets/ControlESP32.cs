using UnityEngine;
using UnityEngine.UI;
using System.IO.Ports;
using TMPro;

public class ControlESP32 : MonoBehaviour
{
    [Header("Configuración del Puerto")]
    public string puertoNombre = "COM7"; // Asegúrate de que siga siendo tu puerto
    public int baudRate = 115200;

    [Header("Elementos de UI")]
    public Slider sliderServo;
    public TMP_Text textoLDR1;
    public TMP_Text textoLDR2;

    private SerialPort puertoSerie;
    private float valorSliderAnterior = -999f; // Variable para rastrear cambios

    void Start()
    {
        puertoSerie = new SerialPort(puertoNombre, baudRate);
        puertoSerie.ReadTimeout = 20;

        try
        {
            puertoSerie.Open();
            Debug.Log("Puerto Serial Abierto.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("No se pudo abrir el puerto serial: " + e.Message);
        }
    }

    void Update()
    {
        if (puertoSerie != null && puertoSerie.IsOpen)
        {
            // 1. Enviar el valor SOLO si el slider se ha movido
            if (Mathf.Abs(sliderServo.value - valorSliderAnterior) > 0.01f)
            {
                puertoSerie.Write(sliderServo.value.ToString("F2") + "\n");
                valorSliderAnterior = sliderServo.value; // Guardamos el nuevo valor
            }

            // 2. Leer los datos de las fotorresistencias
            try
            {
                string datosEntrantes = puertoSerie.ReadLine();
                string[] valoresLDR = datosEntrantes.Split(',');

                if (valoresLDR.Length == 2)
                {
                    textoLDR1.text = "Fotorresistencia 1: " + valoresLDR[0];
                    textoLDR2.text = "Fotorresistencia 2: " + valoresLDR[1];
                }
            }
            catch (System.TimeoutException)
            {
                // Ignoramos el timeout silenciosamente
            }
        }
    }

    void OnApplicationQuit()
    {
        if (puertoSerie != null && puertoSerie.IsOpen)
        {
            puertoSerie.Close();
        }
    }
}