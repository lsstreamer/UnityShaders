using UnityEngine;

/// <summary>Native alpha-silhouette outline for foliage cards, combined with material outlines.</summary>
[ExecuteAlways,RequireComponent(typeof(Camera))]
public class DatasetSilhouetteOutline : MonoBehaviour
{
    public float pixelsAt1024=1.8f;
    public Color outlineColor=new Color(.008f,.012f,.02f,1);
    Material material;
    void OnRenderImage(RenderTexture source,RenderTexture destination)
    {
        if(pixelsAt1024<=0){Graphics.Blit(source,destination);return;}
        if(!material)
        {
            var shader=Shader.Find("Hidden/Dataset/SilhouetteOutline");
            if(!shader||!shader.isSupported){Graphics.Blit(source,destination);return;}
            material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        }
        material.SetVector("_RadiusUV",new Vector4(pixelsAt1024/1024f,pixelsAt1024/1024f*source.width/source.height,0,0));
        material.SetColor("_EdgeColor",outlineColor);
        material.SetVector("_Backdrop",GetComponent<Camera>().backgroundColor);
        Graphics.Blit(source,destination,material);
    }
    void OnDisable()
    {
        if(material){DestroyImmediate(material);material=null;}
    }
}
