using UnityEngine;

public class GameEnding : MonoBehaviour
{
    [SerializeField] private GameObject triggerFinal;
    [SerializeField] private GameObject panelGameOver;
    [SerializeField] private AudioSource sonido;
    [SerializeField] private TimedCrateChallenge prueba;
    private TriggerDetector detector;
    private bool mostrado;

    private void Awake()
    {
        if (sonido != null)
        {
            sonido.playOnAwake = false;
            sonido.loop = false;
        }
        if (panelGameOver != null) panelGameOver.SetActive(false);
        if (triggerFinal != null && !triggerFinal.TryGetComponent(out detector))
            detector = triggerFinal.AddComponent<TriggerDetector>();
    }

    private void Update()
    {
        if (mostrado || detector == null || !detector.JugadorDentro ||
            (prueba != null && !prueba.PuertaAbierta)) return;
        mostrado = true;
        if (panelGameOver != null) panelGameOver.SetActive(true);
        if (sonido != null) sonido.Play();
    }
}
