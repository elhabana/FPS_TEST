using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PruebaCajas : MonoBehaviour
{
    [SerializeField] private TriggerActions triggers;
    [SerializeField] private CajasSystem primeraPrueba;
    [SerializeField] private TMP_Text contador;
    [SerializeField] private Transform puerta;
    [SerializeField] private Transform spawnpoint;
    [SerializeField] private GameObject prefabCaja;
    [SerializeField] private Shader shaderAparicion;
    [SerializeField] private Shader shaderAcierto;
    [SerializeField] private Shader shaderFallo;
    [SerializeField, Min(1)] private int puntosObjetivo = 100;
    [SerializeField, Min(0.1f)] private float intervalo = 0.9f;
    [SerializeField, Min(0.1f)] private float tiempoParaDisparar = 1f;
    [SerializeField, Min(0.01f)] private float duracionEfecto = 0.25f;
    [SerializeField] private Vector3 amplitudMovimiento = new Vector3(3f, 0f, 1f);
    [SerializeField, Min(0f)] private float velocidadMovimiento = 1.2f;
    [SerializeField] private float anguloPuertaZ = -105.5f;
    [SerializeField, Min(0.1f)] private float duracionApertura = 4f;
    [SerializeField, Min(0.1f)] private float distanciaSuelo = 0.8f;
    [SerializeField] private LayerMask capasSuelo = (1 << 0) | (1 << 6) | (1 << 8) | (1 << 9);

    private readonly List<CajaRapida> cajas = new List<CajaRapida>();
    private Vector3 origenSpawn;
    private Vector3 rotacionPuertaInicial;
    private float tiempoApertura;
    public bool PuertaAbierta => completada && tiempoApertura >= duracionApertura;
    private float inicio;
    private float siguienteCaja;
    private int puntos;
    private bool activa;
    private bool completada;

    private void Awake()
    {
        if (contador != null)
        {
            contador.text = "00";
            contador.gameObject.SetActive(false);
        }
        if (spawnpoint != null) origenSpawn = spawnpoint.localPosition;
        if (puerta != null)
        {
            rotacionPuertaInicial = puerta.localEulerAngles;
            if (puerta.GetComponent<Collider>() == null)
                puerta.gameObject.AddComponent<BoxCollider>();
        }
    }

    private void OnEnable()
    {
        if (triggers != null) triggers.UltimoTriggerCompletado += Activar;
    }

    private void OnDisable()
    {
        if (triggers != null) triggers.UltimoTriggerCompletado -= Activar;
        foreach (CajaRapida caja in cajas)
            if (caja != null) Destroy(caja.gameObject);
        cajas.Clear();
    }

    public void Activar()
    {
        if (activa || completada || spawnpoint == null || prefabCaja == null)
            return;
        activa = true;
        inicio = Time.time;
        siguienteCaja = Time.time + 0.5f;
        if (contador != null) contador.gameObject.SetActive(true);
        if (primeraPrueba != null) primeraPrueba.CerrarPuerta();
    }

    private void Update()
    {
        if (completada && puerta != null)
        {
            tiempoApertura = Mathf.Min(tiempoApertura + Time.deltaTime, duracionApertura);
            float progresoApertura = tiempoApertura / duracionApertura;
            float suave = progresoApertura * progresoApertura * progresoApertura * (progresoApertura * (progresoApertura * 6f - 15f) + 10f);
            float zInicial = Mathf.DeltaAngle(0f, rotacionPuertaInicial.z);
            puerta.localEulerAngles = new Vector3(rotacionPuertaInicial.x,
                rotacionPuertaInicial.y, Mathf.Lerp(zInicial, anguloPuertaZ, suave));
        }
        if (!activa || completada) return;

        float t = (Time.time - inicio) * velocidadMovimiento;
        spawnpoint.localPosition = origenSpawn + new Vector3(
            Mathf.Sin(t) * amplitudMovimiento.x,
            Mathf.Sin(t * 0.7f) * amplitudMovimiento.y,
            Mathf.Sin(t * 1.3f) * amplitudMovimiento.z);
        if (Time.time < siguienteCaja) return;
        siguienteCaja = Time.time + intervalo;
        cajas.RemoveAll(c => c == null);
        GameObject objeto = Instantiate(prefabCaja, spawnpoint.position, spawnpoint.rotation);
        objeto.tag = "Untagged";
        foreach (Transform hijo in objeto.GetComponentsInChildren<Transform>(true))
        {
            hijo.gameObject.layer = 7;
            hijo.gameObject.isStatic = false;
        }
        CajaRapida caja = objeto.AddComponent<CajaRapida>();
        cajas.Add(caja);
        caja.Inicializar(this, shaderAparicion, shaderAcierto, shaderFallo,
            tiempoParaDisparar, duracionEfecto, distanciaSuelo, capasSuelo);
    }

    public void RegistrarAcierto()
    {
        if (!activa || completada) return;
        puntos++;
        if (contador != null) contador.text = puntos.ToString("00");
        if (puntos < puntosObjetivo) return;
        completada = true;
        foreach (CajaRapida caja in cajas)
            if (caja != null) caja.Terminar(false);
    }
}


