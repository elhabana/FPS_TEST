using System.Collections;
using UnityEngine;

public class FallingCrate : MonoBehaviour
{
    private TimedCrateChallenge prueba;
    private Shader shaderAcierto;
    private Shader shaderFallo;
    private CrateEffect efecto;
    private Collider[] colliders;
    private Rigidbody cuerpo;
    private float duracion;
    private float caduca;
    private float margenSuelo;
    private LayerMask suelo;
    private bool vulnerable;
    private bool terminada;

    public void Inicializar(TimedCrateChallenge propietario, Shader aparicion, Shader acierto,
        Shader fallo, float vida, float tiempoEfecto, float margen, LayerMask capas)
    {
        prueba = propietario;
        shaderAcierto = acierto;
        shaderFallo = fallo;
        duracion = tiempoEfecto;
        margenSuelo = margen;
        suelo = capas;
        efecto = new CrateEffect(gameObject);
        colliders = GetComponentsInChildren<Collider>(true);
        cuerpo = GetComponent<Rigidbody>();
        if (cuerpo == null) cuerpo = gameObject.AddComponent<Rigidbody>();
        cuerpo.isKinematic = true;
        cuerpo.useGravity = true;
        cuerpo.constraints = RigidbodyConstraints.None;
        cuerpo.interpolation = RigidbodyInterpolation.Interpolate;
        cuerpo.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        foreach (Collider c in colliders) c.enabled = false;
        StartCoroutine(Aparecer(aparicion, vida));
    }

    private IEnumerator Aparecer(Shader shader, float vida)
    {
        efecto.Preparar(shader, new Color(2f, 0.4f, 4f, 1f), 1f);
        yield return efecto.Animar(1f, 0f, duracion);
        efecto.Restaurar();
        if (terminada) yield break;
        foreach (Collider c in colliders) c.enabled = true;
        cuerpo.isKinematic = false;
        cuerpo.WakeUp();
        caduca = Time.time + vida;
        vulnerable = true;
    }

    private void FixedUpdate()
    {
        if (!vulnerable || terminada) return;
        if (Time.time >= caduca)
        {
            Terminar(false);
            return;
        }
        // Se congela y desaparece antes de tocar el suelo, incluso a alta velocidad.
        foreach (Collider c in colliders)
        {
            if (!c.enabled) continue;
            Bounds b = c.bounds;
            float distancia = b.extents.y + margenSuelo + Mathf.Max(0f, -cuerpo.linearVelocity.y) * Time.fixedDeltaTime;
            foreach (RaycastHit hit in Physics.RaycastAll(b.center, Vector3.down, distancia,
                suelo, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform))
                {
                    Terminar(false);
                    return;
                }
            }
        }
    }

    public void RecibirDisparo()
    {
        if (vulnerable) Terminar(true);
    }

    public void Terminar(bool acierto)
    {
        if (terminada) return;
        terminada = true;
        vulnerable = false;
        StopAllCoroutines();
        foreach (Collider c in colliders) c.enabled = false;
        cuerpo.isKinematic = true;
        if (acierto && prueba != null) prueba.RegistrarAcierto();
        StartCoroutine(Desaparecer(acierto));
    }

    private IEnumerator Desaparecer(bool acierto)
    {
        efecto.Preparar(acierto ? shaderAcierto : shaderFallo,
            acierto ? new Color(0f, 4f, 3f, 1f) : new Color(4f, 0.1f, 0.05f, 1f), 0f);
        yield return efecto.Animar(0f, 1f, duracion);
        Destroy(gameObject);
    }

    private void OnDestroy() => efecto?.Restaurar();
}
