using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
	static int 
		baseColorId = Shader.PropertyToID("_BaseColor"),
		cutoffId = Shader.PropertyToID("_Cutoff"),
		metallicId = Shader.PropertyToID("_Metallic"),
		smoothnessId = Shader.PropertyToID("_Smoothness"),
		emissionColorId = Shader.PropertyToID("_EmissionColor");

	static MaterialPropertyBlock block;

	[SerializeField]
	Color baseColor = Color.white;

	[SerializeField, ColorUsage(false, true)]
	Color emissionColor = Color.black;

	[SerializeField]
	float cutoff = 0.5f, metallic = 0f, smoothness = 0.5f;

	void Awake()
	{
		OnValidate();
	}

	void OnValidate()
	{
		if (block == null)
		{
			block = new MaterialPropertyBlock();
		}

		block.SetColor(baseColorId, baseColor);
		block.SetColor(emissionColorId, emissionColor);
		block.SetFloat(cutoffId, cutoff);
		block.SetFloat(metallicId, metallic);
		block.SetFloat(smoothnessId, smoothness);

		GetComponent<Renderer>().SetPropertyBlock(block);//这种方式修改材质参数能够避免材质实例化 节省内存
	}

}