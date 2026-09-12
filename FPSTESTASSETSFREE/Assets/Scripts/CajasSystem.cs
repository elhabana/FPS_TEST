using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CajasSystem : MonoBehaviour
{
    [SerializeField] private TMP_Text contador;
    [SerializeField] private Shader shaderDesintegracion;
    [SerializeField] private Shader shaderAparicion;
    [SerializeField, Min(0.01f)] private float duracionAparicion = 0.6f;
    private bool apareciendo;
    [SerializeField, Min(0.05f)] private float duracionDesintegracion = 0.7f;
    [SerializeField, Min(0f)] private float pausaEntreRondas = 1.5f;
    [SerializeField, ColorUsage(false, true)] private Color colorBorde = new Color(0f, 3f, 4f, 1f);

    [Header("Puerta al completar el objetivo")]
    [SerializeField] private Transform puertaObjetivo;
    [SerializeField, Min(1)] private int cajasParaAbrir = 100;
    [SerializeField, Min(0f)] private float alturaPuerta = 2.5f;
    [SerializeField, Min(0f)] private float velocidadPuerta = 1.5f;
    private Vector3 posicionPuertaInicial;
    private bool puertaBloqueada;

    public void CerrarPuerta() => puertaBloqueada = true;

    private class Caja
    {
        public GameObject objeto;
        public Vector3 posicion;
        public Quaternion rotacion;
        public Collider[] colliders;
        public bool[] collidersActivos;
        public Rigidbody cuerpo;
        public bool cinematica;
        public Renderer[] renderers;
        public Material[][] originales;
        public readonly List<Material> efectos = new List<Material>();
        public bool destruida;
    }

    private readonly Dictionary<Transform, Caja> cajas = new Dictionary<Transform, Caja>();
    private int totalDestruidas;
    private int desaparecidas;

    private void Start()
    {
        if (puertaObjetivo != null)
        {
            posicionPuertaInicial = puertaObjetivo.position;
            if (puertaObjetivo.GetComponent<Collider>() == null)
                puertaObjetivo.gameObject.AddComponent<BoxCollider>();
        }

        foreach (GameObject objeto in GameObject.FindGameObjectsWithTag("Cajas"))
        {
            Caja caja = new Caja
            {
                objeto = objeto,
                posicion = objeto.transform.position,
                rotacion = objeto.transform.rotation,
                colliders = objeto.GetComponentsInChildren<Collider>(true),
                cuerpo = objeto.GetComponent<Rigidbody>(),
                renderers = objeto.GetComponentsInChildren<Renderer>(true)
            };
            caja.collidersActivos = new bool[caja.colliders.Length];
            for (int i = 0; i < caja.colliders.Length; i++)
                caja.collidersActivos[i] = caja.colliders[i].enabled;
            caja.cinematica = caja.cuerpo != null && caja.cuerpo.isKinematic;
            caja.originales = new Material[caja.renderers.Length][];
            for (int i = 0; i < caja.renderers.Length; i++)
                caja.originales[i] = caja.renderers[i].sharedMaterials;
            cajas.Add(objeto.transform, caja);
        }
        ActualizarContador();
        StartCoroutine(AparecerTodas());
    }

    private void Update()
    {
        if (puertaObjetivo == null || (!puertaBloqueada && totalDestruidas < cajasParaAbrir))
            return;

        Vector3 destino = posicionPuertaInicial + (puertaBloqueada ? Vector3.zero : Vector3.up * alturaPuerta);
        puertaObjetivo.position = Vector3.MoveTowards(
            puertaObjetivo.position, destino, velocidadPuerta * Time.deltaTime);
    }

    // Resuelve tambien impactos en colliders hijos del prefab.
    public bool RecibirDisparo(Collider collider)
    {
        if (!isActiveAndEnabled)
            return false;
        for (Transform actual = collider.transform; actual != null; actual = actual.parent)
        {
            if (!cajas.TryGetValue(actual, out Caja caja))
                continue;
            if (apareciendo) return true;
            if (!caja.destruida)
            {
                caja.destruida = true;
                totalDestruidas++;
                ActualizarContador();
                StartCoroutine(Desintegrar(caja));
            }
            return true;
        }
        return false;
    }

    private IEnumerator Desintegrar(Caja caja)
    {
        foreach (Collider collider in caja.colliders)
            collider.enabled = false;
        DespertarCajasRestantes();
        if (caja.cuerpo != null)
        {
            if (!caja.cuerpo.isKinematic)
            {
                caja.cuerpo.linearVelocity = Vector3.zero;
                caja.cuerpo.angularVelocity = Vector3.zero;
            }
            caja.cuerpo.isKinematic = true;
        }

        if (shaderDesintegracion != null)
        {
            PrepararMateriales(caja, shaderDesintegracion, colorBorde, 0f);
            float tiempo = 0f;
            while (tiempo < duracionDesintegracion)
            {
                tiempo += Time.deltaTime;
                foreach (Material efecto in caja.efectos)
                    efecto.SetFloat("_Progress", Mathf.Clamp01(tiempo / duracionDesintegracion));
                yield return null;
            }
        }
        caja.objeto.SetActive(false);
        RestaurarMateriales(caja);
        desaparecidas++;
        if (desaparecidas == cajas.Count)
            StartCoroutine(ReaparecerTodas());
    }

    private void DespertarCajasRestantes()
    {
        // Al retirar un apoyo, despierta tambien las cajas superiores de la pila.
        foreach (Caja restante in cajas.Values)
        {
            if (!restante.destruida && restante.objeto.activeInHierarchy &&
                restante.cuerpo != null && !restante.cuerpo.isKinematic)
            {
                restante.cuerpo.WakeUp();
            }
        }
    }

    private IEnumerator ReaparecerTodas()
    {
        yield return new WaitForSeconds(pausaEntreRondas);
        foreach (Caja caja in cajas.Values)
        {
            caja.objeto.transform.SetPositionAndRotation(caja.posicion, caja.rotacion);
            if (caja.cuerpo != null)
            {
                caja.cuerpo.position = caja.posicion;
                caja.cuerpo.rotation = caja.rotacion;
                caja.cuerpo.isKinematic = caja.cinematica;
                if (!caja.cinematica)
                {
                    caja.cuerpo.linearVelocity = Vector3.zero;
                    caja.cuerpo.angularVelocity = Vector3.zero;
                }
            }
            for (int i = 0; i < caja.colliders.Length; i++)
                caja.colliders[i].enabled = caja.collidersActivos[i];
            caja.destruida = false;
            caja.objeto.SetActive(true);
        }
        desaparecidas = 0;
        yield return AparecerTodas();
    }

    private IEnumerator AparecerTodas()
    {
        apareciendo = true;
        foreach (Caja caja in cajas.Values)
        {
            foreach (Collider c in caja.colliders) c.enabled = false;
            if (caja.cuerpo != null) caja.cuerpo.isKinematic = true;
            if (shaderAparicion != null)
                PrepararMateriales(caja, shaderAparicion, new Color(2f, 0.4f, 4f, 1f), 1f);
        }
        if (shaderAparicion != null)
        {
            float tiempo = 0f;
            while (tiempo < duracionAparicion)
            {
                tiempo += Time.deltaTime;
                float progreso = 1f - Mathf.Clamp01(tiempo / duracionAparicion);
                foreach (Caja caja in cajas.Values)
                    foreach (Material efecto in caja.efectos)
                        efecto.SetFloat("_Progress", progreso);
                yield return null;
            }
        }
        foreach (Caja caja in cajas.Values)
        {
            RestaurarMateriales(caja);
            for (int i = 0; i < caja.colliders.Length; i++)
                caja.colliders[i].enabled = caja.collidersActivos[i];
            if (caja.cuerpo != null) caja.cuerpo.isKinematic = caja.cinematica;
        }
        apareciendo = false;
        DespertarCajasRestantes();
    }

    private void ActualizarContador()
    {
        if (contador != null)
            contador.text = totalDestruidas.ToString("00");
    }

    private void PrepararMateriales(Caja caja, Shader shader, Color color, float progreso)
    {
            for (int i = 0; i < caja.renderers.Length; i++)
            {
                Material[] materiales = new Material[caja.originales[i].Length];
                for (int j = 0; j < materiales.Length; j++)
                {
                    Material original = caja.originales[i][j];
                    Material efecto = new Material(shader);
                    if (original != null)
                    {
                        string mapa = original.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                        if (original.HasProperty(mapa))
                        {
                            efecto.SetTexture("_BaseMap", original.GetTexture(mapa));
                            efecto.SetTextureScale("_BaseMap", original.GetTextureScale(mapa));
                            efecto.SetTextureOffset("_BaseMap", original.GetTextureOffset(mapa));
                        }
                        if (original.HasProperty("_BaseColor"))
                            efecto.SetColor("_BaseColor", original.GetColor("_BaseColor"));
                    }
                    efecto.SetColor("_EdgeColor", color);
                    efecto.SetFloat("_Progress", progreso);
                    materiales[j] = efecto;
                    caja.efectos.Add(efecto);
                }
                caja.renderers[i].sharedMaterials = materiales;
            }
    }

    private void RestaurarMateriales(Caja caja)
    {
        for (int i = 0; i < caja.renderers.Length; i++)
            if (caja.renderers[i] != null)
                caja.renderers[i].sharedMaterials = caja.originales[i];
        foreach (Material efecto in caja.efectos)
            Destroy(efecto);
        caja.efectos.Clear();
    }

    private void OnDestroy()
    {
        foreach (Caja caja in cajas.Values)
            RestaurarMateriales(caja);
    }
}

