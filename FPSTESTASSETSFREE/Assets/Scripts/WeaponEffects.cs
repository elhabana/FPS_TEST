using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Object;

// Solo controla el aspecto del arma y la camara; no decide impactos ni puntos.
[Serializable]
public class WeaponEffects
{
    private Camera camaraApuntado;
    [Header("Apuntado - mantener boton derecho")]
    [SerializeField] private Vector3 desplazamientoApuntado = new Vector3(-0.26f, 0.17f, 0.05f);
    [SerializeField, Range(0.2f, 1f)] private float multiplicadorFovApuntado = 0.75f;
    [SerializeField, Min(0.1f)] private float velocidadApuntado = 12f;
    [SerializeField, Range(0f, 1f)] private float retrocesoApuntando = 0.5f;

    private float mezclaApuntado;
    private float fovInicial;
    private bool fovGuardado;
    private CinemachineCamera camaraVirtual;
    private CinemachineBrain cerebroCamara;

    [Header("Retroceso visual")]
    [SerializeField] private Transform armaRetroceso;
    [SerializeField, Min(0f)] private float distanciaRetroceso = 0.045f;
    [SerializeField, Min(0f)] private float anguloRetroceso = 4f;
    [SerializeField, Min(0.02f)] private float duracionRetroceso = 0.18f;

    [Header("Efecto de disparo")]
    [SerializeField, Range(0f, 1f)] private float aberracionDisparo = 0.18f;
    [SerializeField, Min(0.01f)] private float duracionAberracion = 0.12f;

    private Vector3 posicionArmaInicial;
    private Quaternion rotacionArmaInicial;
    private float tiempoDisparo = float.NegativeInfinity;
    private Volume volumenDisparo;
    private VolumeProfile perfilDisparo;
    private ChromaticAberration aberracion;

    public void Inicializar(Transform transform, Camera camara)
    {
        camaraApuntado = camara;
        if (camaraApuntado != null)
            cerebroCamara = camaraApuntado.GetComponent<CinemachineBrain>();

        if (armaRetroceso == null)
            armaRetroceso = transform;

        posicionArmaInicial = armaRetroceso.localPosition;
        rotacionArmaInicial = armaRetroceso.localRotation;

        GameObject objetoVolumen = new GameObject("Efecto de disparo");
        objetoVolumen.transform.SetParent(transform, false);
        // Usa una capa que la camara incluya en su mascara de volumenes.
        if (camaraApuntado != null &&
            camaraApuntado.TryGetComponent(out UniversalAdditionalCameraData datosCamara))
        {
            for (int capa = 0; capa < 32; capa++)
            {
                if ((datosCamara.volumeLayerMask.value & (1 << capa)) == 0)
                    continue;
                objetoVolumen.layer = capa;
                break;
            }
        }

        volumenDisparo = objetoVolumen.AddComponent<Volume>();
        volumenDisparo.isGlobal = true;
        volumenDisparo.priority = 100f;
        volumenDisparo.weight = 0f;
        perfilDisparo = ScriptableObject.CreateInstance<VolumeProfile>();
        aberracion = perfilDisparo.Add<ChromaticAberration>(true);
        volumenDisparo.sharedProfile = perfilDisparo;

    }

    public void Disparar() => tiempoDisparo = Time.time;

    public void ActualizarApuntado()
    {
        bool apuntando = Application.isFocused && Mouse.current != null && Mouse.current.rightButton.isPressed;
        float suavizado = 1f - Mathf.Exp(-velocidadApuntado * Time.deltaTime);
        mezclaApuntado = Mathf.Lerp(mezclaApuntado, apuntando ? 1f : 0f, suavizado);

        if (camaraApuntado == null)
            return;

        // Cinemachine controla la lente; espera a que su camara este activa.
        if (!fovGuardado)
        {
            if (cerebroCamara != null && cerebroCamara.enabled)
            {
                camaraVirtual = cerebroCamara.ActiveVirtualCamera as CinemachineCamera;
                if (camaraVirtual == null)
                    return;
                fovInicial = camaraVirtual.Lens.FieldOfView;
            }
            else
                fovInicial = camaraApuntado.fieldOfView;
            fovGuardado = true;
        }

        float fov = Mathf.Lerp(fovInicial, fovInicial * multiplicadorFovApuntado, mezclaApuntado);
        if (camaraVirtual != null)
            camaraVirtual.Lens.FieldOfView = fov;
        else
            camaraApuntado.fieldOfView = fov;
    }

    public void ActualizarRetroceso()
    {
        float transcurrido = Time.time - tiempoDisparo;
        float progreso = Mathf.Clamp01(transcurrido / duracionRetroceso);
        // Golpe rapido y vuelta suave, sin acumular desplazamiento entre disparos.
        float fuerza = progreso < 0.2f
            ? Mathf.SmoothStep(0f, 1f, progreso / 0.2f)
            : 1f - Mathf.SmoothStep(0f, 1f, (progreso - 0.2f) / 0.8f);

        if (armaRetroceso != null)
        {
            fuerza *= Mathf.Lerp(1f, retrocesoApuntando, mezclaApuntado);
            armaRetroceso.localPosition = posicionArmaInicial + desplazamientoApuntado * mezclaApuntado +
                rotacionArmaInicial * Vector3.back * (distanciaRetroceso * fuerza);
            armaRetroceso.localRotation = rotacionArmaInicial *
                Quaternion.Euler(-anguloRetroceso * fuerza, 0f, 0f);
        }

        volumenDisparo.weight = 1f - Mathf.Clamp01(transcurrido / duracionAberracion);
        aberracion.intensity.value = aberracionDisparo;
    }

    public void Restablecer()
    {
        mezclaApuntado = 0f;
        if (fovGuardado)
        {
            if (camaraVirtual != null)
                camaraVirtual.Lens.FieldOfView = fovInicial;
            else if (camaraApuntado != null)
                camaraApuntado.fieldOfView = fovInicial;
        }
        fovGuardado = false;
        tiempoDisparo = float.NegativeInfinity;
        if (volumenDisparo != null)
            volumenDisparo.weight = 0f;
        if (armaRetroceso != null)
        {
            armaRetroceso.localPosition = posicionArmaInicial;
            armaRetroceso.localRotation = rotacionArmaInicial;
        }
    }

    public void Liberar()
    {
        if (volumenDisparo != null)
            Destroy(volumenDisparo.gameObject);
        if (perfilDisparo != null)
        {
            foreach (VolumeComponent componente in perfilDisparo.components)
                Destroy(componente);
            Destroy(perfilDisparo);
        }
    }
}
