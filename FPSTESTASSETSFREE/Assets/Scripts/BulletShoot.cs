using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

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
    [SerializeField] private Shader laserShader;
    [SerializeField] private LayerMask capasImpacto = Physics.DefaultRaycastLayers;

    [SerializeField] private WeaponEffects efectos = new WeaponEffects();

    private class Bala
    {
        public LineRenderer visual;
        public Vector3 posicion;
        public Vector3 direccion;
        public float recorrido;
    }

    private readonly List<Bala> balas = new List<Bala>();
    private CrateChallenge cajasSystem;
    private AudioSource fuenteAudio;
    private Material materialLaser;


    private void Awake()
    {
        if (camaraApuntado == null)
            camaraApuntado = Camera.main;

        efectos.Inicializar(transform, camaraApuntado);

        cajasSystem = FindFirstObjectByType<CrateChallenge>();
        fuenteAudio = GetComponent<AudioSource>();
        fuenteAudio.playOnAwake = false;

        // La referencia de la escena permite incluir el shader en la build.
        if (laserShader == null)
        {
            Debug.LogError("BulletShoot: falta asignar Laser Shader.", this);
            enabled = false;
            return;
        }
        materialLaser = new Material(laserShader);
        materialLaser.SetColor("_BaseColor", colorLaser);

    }

    private void Update()
    {
        efectos.ActualizarApuntado();
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            Disparar();
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
                FallingCrate cajaRapida = impacto.collider.GetComponentInParent<FallingCrate>();
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

    private void LateUpdate() => efectos.ActualizarRetroceso();

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
        efectos.Disparar();

        if (audioDisparo != null)
            fuenteAudio.PlayOneShot(audioDisparo);
    }

    private void OnDisable()
    {
        efectos.Restablecer();
        foreach (Bala bala in balas)
            Destroy(bala.visual.gameObject);
        balas.Clear();
    }

    private void OnDestroy()
    {
        efectos.Liberar();
        if (materialLaser != null)
            Destroy(materialLaser);
    }
}
