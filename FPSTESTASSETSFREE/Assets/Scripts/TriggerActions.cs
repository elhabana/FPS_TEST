using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class TriggerActions : MonoBehaviour
{
    public event Action UltimoTriggerCompletado;

    [Serializable]
    private class AccionTrigger
    {
        public GameObject trigger;
        public GameObject texto3D;
        [Min(0f)] public float duracionFade = 0.5f;

        [NonSerialized] public TriggerDetector detector;
        [NonSerialized] public bool completada;
        [NonSerialized] public TMP_Text texto;
        [NonSerialized] public float opacidadVisible;
        [NonSerialized] public Coroutine fade;
    }

    [SerializeField] private AccionTrigger[] acciones = Array.Empty<AccionTrigger>();

    private void Start()
    {
        for (int i = 0; i < acciones.Length; i++)
        {
            AccionTrigger accion = acciones[i];
            if (accion.texto3D != null)
            {
                accion.texto = accion.texto3D.GetComponentInChildren<TMP_Text>(true);
                accion.opacidadVisible = accion.texto != null ? accion.texto.alpha : 1f;
                if (i > 0)
                {
                    if (accion.texto != null)
                        accion.texto.alpha = 0f;
                    accion.texto3D.SetActive(false);
                }
            }

            if (accion.trigger == null)
                continue;

            Collider zona = accion.trigger.GetComponent<Collider>();
            if (zona == null || !zona.isTrigger)
            {
                Debug.LogWarning("El objeto asignado necesita un Collider con Is Trigger activado.", accion.trigger);
                continue;
            }

            if (!accion.trigger.TryGetComponent(out accion.detector))
                accion.detector = accion.trigger.AddComponent<TriggerDetector>();
        }
    }

    private void Update()
    {
        for (int i = 0; i < acciones.Length; i++)
        {
            AccionTrigger accion = acciones[i];
            if (accion.completada)
                continue;

            // Procesa el siguiente paso pendiente, en el orden del Inspector.
            if (accion.detector != null && accion.detector.JugadorDentro)
            {
                accion.completada = true;
                CambiarVisibilidad(accion, false);
                if (i + 1 < acciones.Length)
                    CambiarVisibilidad(acciones[i + 1], true);
                else
                    UltimoTriggerCompletado?.Invoke();
            }
            break;
        }
    }

    private void CambiarVisibilidad(AccionTrigger accion, bool mostrar)
    {
        if (accion.texto3D == null)
            return;
        // Si se alcanza el siguiente trigger durante un fade, continua desde su opacidad actual.
        if (accion.fade != null)
            StopCoroutine(accion.fade);
        if (mostrar)
            accion.texto3D.SetActive(true);
        accion.fade = StartCoroutine(AnimarTexto(accion, mostrar));
    }

    private IEnumerator AnimarTexto(AccionTrigger accion, bool mostrar)
    {
        float destino = mostrar ? accion.opacidadVisible : 0f;
        if (accion.texto != null)
        {
            float origen = accion.texto.alpha;
            float tiempo = 0f;
            while (tiempo < accion.duracionFade && accion.texto != null)
            {
                tiempo += Time.deltaTime;
                accion.texto.alpha = Mathf.Lerp(origen, destino, tiempo / accion.duracionFade);
                yield return null;
            }
            if (accion.texto != null)
                accion.texto.alpha = destino;
        }
        if (!mostrar && accion.texto3D != null)
            accion.texto3D.SetActive(false);
    }
}