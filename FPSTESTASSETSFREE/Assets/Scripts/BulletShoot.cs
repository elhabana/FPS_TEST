using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(AudioSource))]
public class BulletShoot : MonoBehaviour
{
    [SerializeField] private Transform pointShoot;
    [SerializeField] private Camera camaraApuntado;
    [SerializeField] private AudioClip audioDisparo;
    [SerializeField, Min(0f)] private float alcance = 100f;
    [SerializeField, Min(0.1f)] private float velocidadBala = 45f;
    [SerializeField, Min(0.01f)] private float longitudBala = 0.45f;
    [SerializeField, Min(0f)] private float fuerzaEmpuje = 8f;
    [SerializeField] private LayerMask capasEmpujables = 1 << 7;
    [SerializeField, Min(0.001f)] private float grosorLaser = 0.02f;
    [SerializeField] private Color colorLaser = Color.red;
    [SerializeField] private LayerMask capasImpacto = Physics.DefaultRaycastLayers;

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

    private class Bala
    {
        public LineRenderer visual;
        public Vector3 posicion;
        public Vector3 direccion;
        public float recorrido;
    }

    private readonly List<Bala> balas = new List<Bala>();
    private CajasSystem cajasSystem;
    private AudioSource fuenteAudio;
    private Material materialLaser;


    private void Awake()
    {
        if (camaraApuntado == null)
            camaraApuntado = Camera.main;

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

        // Oculta el LineRenderer de la version anterior del script.
        if (TryGetComponent(out LineRenderer laserAnterior))
            laserAnterior.enabled = false;
        cajasSystem = FindFirstObjectByType<CajasSystem>();
        fuenteAudio = GetComponent<AudioSource>();
        fuenteAudio.playOnAwake = false;

        materialLaser = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        materialLaser.SetColor("_BaseColor", colorLaser);

    }

    private void Update()
    {
        ActualizarApuntado();
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            Disparar();
    }

    private void ActualizarApuntado()
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

    private void FixedUpdate()
    {
        for (int i = balas.Count - 1; i >= 0; i--)
        {
            Bala bala = balas[i];
            float paso = Mathf.Min(velocidadBala * Time.fixedDeltaTime, alcance - bala.recorrido);

            // Barrido del recorrido completo para no saltarse colliders a alta velocidad.
            if (Physics.Raycast(bala.posicion, bala.direccion, out RaycastHit impacto,
                paso, capasImpacto, QueryTriggerInteraction.Ignore))
            {
                CajaRapida cajaRapida = impacto.collider.GetComponentInParent<CajaRapida>();
                if (cajaRapida != null) cajaRapida.RecibirDisparo();
                bool esCaja = cajaRapida != null || (cajasSystem != null && cajasSystem.RecibirDisparo(impacto.collider));
                Rigidbody cuerpo = impacto.rigidbody;
                if (!esCaja && cuerpo != null && !cuerpo.isKinematic &&
                    (capasEmpujables.value & (1 << cuerpo.gameObject.layer)) != 0)
                {
                    cuerpo.AddForceAtPosition(bala.direccion * fuerzaEmpuje,
                        impacto.point, ForceMode.Impulse);
                }

                Destroy(bala.visual.gameObject);
                balas.RemoveAt(i);
                continue;
            }

            bala.posicion += bala.direccion * paso;
            bala.recorrido += paso;
            bala.visual.SetPosition(0, bala.posicion - bala.direccion *
                Mathf.Min(longitudBala, bala.recorrido));
            bala.visual.SetPosition(1, bala.posicion);

            if (bala.recorrido >= alcance)
            {
                Destroy(bala.visual.gameObject);
                balas.RemoveAt(i);
            }
        }
    }

    private void LateUpdate()
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

    public void Disparar()
    {
        if (pointShoot == null || camaraApuntado == null)
            return;

        // La mira determina el objetivo desde el centro de la camara.
        Ray rayoMira = camaraApuntado.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 destino = rayoMira.GetPoint(alcance);

        if (Physics.Raycast(rayoMira, out RaycastHit objetivo, alcance,
            capasImpacto, QueryTriggerInteraction.Ignore))
        {
            destino = objetivo.point;
        }

        Vector3 origen = pointShoot.position;
        Vector3 direccion = (destino - origen).normalized;
        if (direccion.sqrMagnitude < 0.001f || alcance <= 0f)
            return;

        GameObject objetoBala = new GameObject("Bala laser");
        LineRenderer visual = objetoBala.AddComponent<LineRenderer>();
        visual.useWorldSpace = true;
        visual.positionCount = 2;
        visual.startWidth = grosorLaser;
        visual.endWidth = grosorLaser;
        visual.numCapVertices = 3;
        visual.shadowCastingMode = ShadowCastingMode.Off;
        visual.receiveShadows = false;
        visual.sharedMaterial = materialLaser;
        visual.SetPosition(0, origen);
        visual.SetPosition(1, origen);
        balas.Add(new Bala { visual = visual, posicion = origen, direccion = direccion });
        tiempoDisparo = Time.time;

        if (audioDisparo != null)
            fuenteAudio.PlayOneShot(audioDisparo);
    }

    private void OnDisable()
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
        foreach (Bala bala in balas)
            Destroy(bala.visual.gameObject);
        balas.Clear();
    }

    private void OnDestroy()
    {
        if (volumenDisparo != null)
            Destroy(volumenDisparo.gameObject);
        if (perfilDisparo != null)
        {
            foreach (VolumeComponent componente in perfilDisparo.components)
                Destroy(componente);
            Destroy(perfilDisparo);
        }
        if (materialLaser != null)
            Destroy(materialLaser);
    }
}
