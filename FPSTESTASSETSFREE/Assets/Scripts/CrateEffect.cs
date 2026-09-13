using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Comparte los efectos de ambas pruebas sin cambiar los materiales del prefab.
public class CrateEffect
{
    private readonly Renderer[] renderers;
    private readonly Material[][] originales;
    private readonly List<Material> materiales = new List<Material>();

    public CrateEffect(GameObject objeto)
    {
        renderers = objeto.GetComponentsInChildren<Renderer>(true);
        originales = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
            originales[i] = renderers[i].sharedMaterials;
    }

    public void Preparar(Shader shader, Color color, float progreso)
    {
        Restaurar();
        if (shader == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] nuevos = new Material[originales[i].Length];
            for (int j = 0; j < nuevos.Length; j++)
            {
                Material original = originales[i][j];
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
                nuevos[j] = efecto;
                materiales.Add(efecto);
            }
            renderers[i].sharedMaterials = nuevos;
        }
        Progreso(progreso);
    }

    public void Progreso(float valor)
    {
        foreach (Material material in materiales)
            material.SetFloat("_Progress", valor);
    }

    public IEnumerator Animar(float desde, float hasta, float duracion)
    {
        Progreso(desde);
        for (float tiempo = 0f; tiempo < duracion;)
        {
            tiempo += Time.deltaTime;
            Progreso(Mathf.Lerp(desde, hasta, tiempo / duracion));
            yield return null;
        }
        Progreso(hasta);
    }

    public void Restaurar()
    {
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].sharedMaterials = originales[i];
        foreach (Material material in materiales) Object.Destroy(material);
        materiales.Clear();
    }
}
