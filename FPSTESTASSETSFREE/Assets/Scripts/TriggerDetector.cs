using System.Collections.Generic;
using UnityEngine;

// Se agrega automaticamente durante el juego. La configuracion vive en TriggerSystem.
[AddComponentMenu("")]
public class TriggerDetector : MonoBehaviour
{
    private readonly HashSet<Collider> jugadores = new HashSet<Collider>();

    public bool JugadorDentro
    {
        get
        {
            jugadores.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            return isActiveAndEnabled && jugadores.Count > 0;
        }
    }

    private void OnTriggerEnter(Collider other) => Registrar(other);
    private void OnTriggerStay(Collider other) => Registrar(other);

    private void Registrar(Collider other)
    {
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
            jugadores.Add(other);
    }

    private void OnTriggerExit(Collider other) => jugadores.Remove(other);
    private void OnDisable() => jugadores.Clear();
}
