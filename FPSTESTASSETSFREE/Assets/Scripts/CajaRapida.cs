using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CajaRapida : MonoBehaviour
{
    private PruebaCajas prueba;
    private Shader shaderAcierto;
    private Shader shaderFallo;
    private Renderer[] renderers;
    private Material[][] originales;
    private Collider[] colliders;
    private Rigidbody cuerpo;
    private readonly List<Material> materiales = new List<Material>();
    private float duracion;
    private float caduca;
    private float margenSuelo;
    private LayerMask suelo;
    private bool vulnerable;
    private bool terminada;

    public void Inicializar(PruebaCajas propietario, Shader aparicion, Shader acierto,
        Shader fallo, float vida, float tiempoEfecto, float margen, LayerMask capas)
    {
        prueba = propietario;
        shaderAcierto = acierto;
        shaderFallo = fallo;
        duracion = tiempoEfecto;
        margenSuelo = margen;
        suelo = capas;
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        originales = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++) originales[i] = renderers[i].sharedMaterials;
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
        CambiarMateriales(shader, new Color(2f, 0.4f, 4f, 1f));
        yield return Animar(1f, 0f);
        RestaurarMateriales();
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
        CambiarMateriales(acierto ? shaderAcierto : shaderFallo,
            acierto ? new Color(0f, 4f, 3f, 1f) : new Color(4f, 0.1f, 0.05f, 1f));
        yield return Animar(0f, 1f);
        Destroy(gameObject);
    }

    private IEnumerator Animar(float desde, float hasta)
    {
        float tiempo = 0f;
        foreach (Material m in materiales) m.SetFloat("_Progress", desde);
        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            foreach (Material m in materiales)
                m.SetFloat("_Progress", Mathf.Lerp(desde, hasta, tiempo / duracion));
            yield return null;
        }
    }

    private void CambiarMateriales(Shader shader, Color color)
    {
        RestaurarMateriales();
        if (shader == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] nuevos = new Material[originales[i].Length];
            for (int j = 0; j < nuevos.Length; j++)
            {
                Material original = originales[i][j];
                Material m = new Material(shader);
                if (original != null)
                {
                    string mapa = original.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                    if (original.HasProperty(mapa))
                    {
                        m.SetTexture("_BaseMap", original.GetTexture(mapa));
                        m.SetTextureScale("_BaseMap", original.GetTextureScale(mapa));
                        m.SetTextureOffset("_BaseMap", original.GetTextureOffset(mapa));
                    }
                    if (original.HasProperty("_BaseColor")) m.SetColor("_BaseColor", original.GetColor("_BaseColor"));
                }
                m.SetColor("_EdgeColor", color);
                nuevos[j] = m;
                materiales.Add(m);
            }
            renderers[i].sharedMaterials = nuevos;
        }
    }

    private void RestaurarMateriales()
    {
        if (renderers != null)
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sharedMaterials = originales[i];
        foreach (Material m in materiales) Destroy(m);
        materiales.Clear();
    }

    private void OnDestroy() => RestaurarMateriales();
}
