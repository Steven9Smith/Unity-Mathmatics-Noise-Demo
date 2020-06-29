using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.UI;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

public class NoiseComponent : MonoBehaviour
{

	[Tooltip("Noise Type to test")]
	public NoiseClass.NoiseType NoiseType = NoiseClass.NoiseType.CellularNoise;
	public bool Is2D = true, Is3D = false, Is4D = false;

	public float Scale = 10f;



	public string FileName = "";

	public Vector2 MinMaxValue;

	public bool UseScaleAsMax = true;

	public float ValueA;
	public float ValueB;
	public float ValueC;
	public float ValueD;

	private float4 oldValues;
	private float4 oldSize;
	private float oldScale;
	private NoiseClass.NoiseType OldNoiseType;

	private MeshRenderer mr;

	public Vector2Int MinMaxWidth = new Vector2Int(1,64);
	public Vector2Int MinMaxHeight = new Vector2Int(1,64);
	// This is used in 3D noise
	public Vector2Int MinMaxLength = new Vector2Int(1, 64);
	// This is used for 4D noise. Depth is not its true name, its just a placeholder name
	public Vector2Int MinMaxDepth = new Vector2Int(1, 64);
	public int width = 64, height = 64,length = 1,depth = 1;

	// Start is called before the first frame update
	void Start()
	{
		mr = GetComponent<MeshRenderer>();
		if (mr == null)
			Debug.LogError("Failed to get Mesh Renderer");
	}



	// Update is called once per frame
	void Update()
	{
		if (ValuesChange())
		{

			mr.material.mainTexture = NoiseClass.GenerateTexture(NoiseType, width, height,length,depth, Scale, new float4(ValueA, ValueB, ValueC, ValueD),Is2D ? 2 : Is3D ? 3 : Is4D ? 4 : 0);
			oldValues = new float4(ValueA, ValueB, ValueC, ValueD);
			oldScale = Scale;
			OldNoiseType = NoiseType;
			oldSize = new float4(width, height,length,depth);
		}
	}

	bool ValuesChange()
	{
		return !(
			// Test Values
			ValueA == oldValues.x && ValueB == oldValues.y && oldValues.z == ValueC && oldValues.w == ValueD
			// Test NoiseType
			&& NoiseType == OldNoiseType
			// Test Scale
			&& Scale == oldScale
			// Test for size changes
			&& oldSize.x == width && oldSize.y == height && oldSize.z == length && oldSize.w == depth
		);
	}

}
[CustomEditor(typeof(NoiseComponent))]
public class NoiseEditor : Editor
{

	public NoiseComponent nc;

	public override void OnInspectorGUI()
	{
		//	DrawDefaultInspector();
		DrawHeader();
		nc = target as NoiseComponent;
		// Noise Type
		EditorGUILayout.LabelField("Noise Type", EditorStyles.boldLabel);
		nc.NoiseType = (NoiseClass.NoiseType)EditorGUILayout.EnumPopup("Noise Type", nc.NoiseType);

		// determine the dimension
		nc.Is2D = NoiseClass.TypeIs2D(nc.NoiseType);
		if (!nc.Is2D)
			nc.Is3D = NoiseClass.TypeIs3D(nc.NoiseType);
		if (!nc.Is3D)
			nc.Is4D = NoiseClass.TypeIs4D(nc.NoiseType);

		// Material Attributes
		EditorGUILayout.LabelField("Material Attribute", EditorStyles.boldLabel);
		//		Scale
		nc.UseScaleAsMax = EditorGUILayout.Toggle("Use Scale As Size Limit",nc.UseScaleAsMax);
		nc.Scale = EditorGUILayout.FloatField("Scale", nc.Scale);
		//		Width
		nc.MinMaxWidth = EditorGUILayout.Vector2IntField("Min Max Width", nc.MinMaxWidth);
		if (nc.MinMaxWidth.x > nc.MinMaxWidth.y)
			nc.MinMaxWidth.x = nc.MinMaxWidth.y - 1;
		//		Height
		nc.MinMaxHeight = EditorGUILayout.Vector2IntField("Min Max Height", nc.MinMaxHeight);
		if (nc.MinMaxHeight.x > nc.MinMaxHeight.y)
			nc.MinMaxHeight.x = nc.MinMaxHeight.y - 1;
		//		Length
		if(nc.Is3D || nc.Is4D)
		{
			nc.MinMaxLength = EditorGUILayout.Vector2IntField("Min Max Length", nc.MinMaxLength);
			if (nc.MinMaxLength.x > nc.MinMaxLength.y)
				nc.MinMaxLength.x = nc.MinMaxLength.y - 1;
		}
		//		Depth
		if (nc.Is4D)
		{
			nc.MinMaxDepth = EditorGUILayout.Vector2IntField("Min Max Depth", nc.MinMaxDepth);
			if (nc.MinMaxDepth.x > nc.MinMaxDepth.y)
				nc.MinMaxDepth.x = nc.MinMaxDepth.y - 1;
		}
		// create the size sliiders
		nc.width = EditorGUILayout.IntSlider("Width", nc.width,nc.MinMaxWidth.x,nc.MinMaxWidth.y);
		nc.height = EditorGUILayout.IntSlider("Height", nc.height, nc.MinMaxHeight.x, nc.MinMaxHeight.y);
		

		EditorGUILayout.LabelField("Gradients and Rotation Values", EditorStyles.boldLabel);
		//Update MinMaxValue is UseScaleAsMax is true
		if (nc.UseScaleAsMax)
			nc.MinMaxValue = new float2(0, nc.Scale);
		else
			nc.MinMaxValue = EditorGUILayout.Vector2Field("MinMax Value", nc.MinMaxValue);
		
		

		switch (nc.NoiseType)
		{
			case NoiseClass.NoiseType.PerlinNoise:
				DisplaySliders(new string[] { "Explicit Period A", "Explicit Peroid B" });
				break;
			
			case NoiseClass.NoiseType.SRDNoise2D:
			case NoiseClass.NoiseType.SRNoise2D:

				DisplaySliders(new string[] { "Gradient Rotation" });
				break;
			// these are currently not programmed
			case NoiseClass.NoiseType.PerlinNoise3x3x3:
			case NoiseClass.NoiseType.PerlinNoise4x4x4x4:
			case NoiseClass.NoiseType.CellularNoise2x2x2:
			case NoiseClass.NoiseType.CellularNoise3x3x3:
			case NoiseClass.NoiseType.ClassicPerlinNoise3x3x3:
			case NoiseClass.NoiseType.SimplexNoise3x3x3:
			case NoiseClass.NoiseType.ClassicPerlinNoise4x4x4x4:
			case NoiseClass.NoiseType.SimplexNoise4x4x4x4:
			case NoiseClass.NoiseType.CellularNoise2x2:
			// these take no extra input from the user (feel free to mess with the scale and size though)
			case NoiseClass.NoiseType.CellularNoise:
			case NoiseClass.NoiseType.ClassicPerlinNoise:
			case NoiseClass.NoiseType.SimplexNoise:
			case NoiseClass.NoiseType.SRNoise:
			case NoiseClass.NoiseType.SRDNoise:
				DisplaySliders(new string[] { });
				break;
			default:
				break;
		}

	}
	/// <summary>
	/// This is responsible for displaying the sliders on certain noises
	/// </summary>
	/// <param name="valueStrings">an array of what the sliders text will be</param>
	/// <param name="numOfValueSliders">determines the numbers of slider that will be shown</param>
	private void DisplaySliders(string[] valueStrings)
	{
		if (valueStrings.Length > 0)
			nc.ValueA = EditorGUILayout.Slider(valueStrings[0], nc.ValueA, nc.MinMaxValue.x, nc.MinMaxValue.y);
		if (valueStrings.Length > 1)
			nc.ValueB = EditorGUILayout.Slider(valueStrings[1], nc.ValueB, nc.MinMaxValue.x, nc.MinMaxValue.y);
		if (valueStrings.Length > 2)
			nc.ValueC = EditorGUILayout.Slider(valueStrings[2], nc.ValueC, nc.MinMaxValue.x, nc.MinMaxValue.y);
		if (valueStrings.Length > 3)
			nc.ValueD = EditorGUILayout.Slider(valueStrings[3], nc.ValueD, nc.MinMaxValue.x, nc.MinMaxValue.y);
	}
}


public static class NoiseClass
{
	public enum NoiseType
	{
		// Cellular noise, returning F1 and F2 in a float2.
		// Standard 3x3 search window for good F1 and F2 values
		CellularNoise,
		// Cellular noise, returning F1 and F2 in a float2.
		// Speeded up by umath.sing 2x2 search window instead of 3x3,
		// at the expense of some strong pattern artifacts.
		// F2 is often wrong and has sharp discontinuities.
		// If you need a smooth F2, use the slower 3x3 version.
		// F1 is sometimes wrong, too, but OK for most purposes.
		// TL;DR - Faster at the cost of accuracy and artifacts
		CellularNoise2x2,
		// Cellular noise, returning F1 and F2 in a float2.
		// Speeded up by umath.sing 2x2x2 search window instead of 3x3x3,
		// at the expense of some pattern artifacts.
		// F2 is often wrong and has sharp discontinuities.
		// If you need a good F2, use the slower 3x3x3 version.
		CellularNoise2x2x2,
		// Cellular noise, returning F1 and F2 in a float2.
		// 3x3x3 search region for good F2 everywhere, but a lot
		// slower than the 2x2x2 version.
		// The code below is a bit scary even to its author,
		// but it has at least half decent performance on a
		// math.modern GPU. In any case, it beats any software
		// implementation of Worley noise hands down.
		CellularNoise3x3x3,
		// Classic Perlin noise
		ClassicPerlinNoise,
		// Classic Perlin noise
		ClassicPerlinNoise3x3x3,
		// Classic Perlin noise
		ClassicPerlinNoise4x4x4x4,

		// Array and textureless GLSL 2D simplex noise function.
		SimplexNoise,
		// Array and textureless GLSL 2D/3D/4D simplex noise functions
		SimplexNoise3x3x3,
		// Array and textureless GLSL 2D/3D/4D simplex noise functions
		SimplexNoise4x4x4x4,
		// Array and textureless GLSL 2D/3D/4D simplex noise functions
		//	SimplexNoiseGradient,
		//
		// 2-D tiling simplex noise with rotating gradients and analytical derivative.
		// The first component of the 3-element return vector is the noise value,
		// and the second and third components are the x and y partial derivatives.
		//
		//	RotatingNoise,
		// Assumming pnoise is perlin noise
		PerlinNoise,
		PerlinNoise3x3x3,
		PerlinNoise4x4x4x4,

		SRNoise,
		SRDNoise,
		SRDNoise2D,
		SRNoise2D

	}

	public static string Type2String(NoiseType type)
	{
		switch (type)
		{
			case NoiseType.CellularNoise:
				return "Cellular";
			case NoiseType.CellularNoise2x2:
				return "Cellular2x2";
			case NoiseType.CellularNoise2x2x2:
				return "Cellular2x2x2";
			case NoiseType.CellularNoise3x3x3:
				return "Cellular3x3x3";
			case NoiseType.ClassicPerlinNoise:
				return "ClassicPerlinNoise";
			case NoiseType.ClassicPerlinNoise3x3x3:
				return "ClassicPerlinNoise3x3x3";
			case NoiseType.ClassicPerlinNoise4x4x4x4:
				return "ClassicPerlinNoise4x4x4x4";
			case NoiseType.PerlinNoise:
				return "PerlinNoise";
			case NoiseType.PerlinNoise3x3x3:
				return "PerlinNoise3x3x3";
			case NoiseType.PerlinNoise4x4x4x4:
				return "PerlinNOise4x4x4x4";
			case NoiseType.SimplexNoise:
				return "SimplexNoise";
			case NoiseType.SimplexNoise3x3x3:
				return "SimplexNoise3x3x3";
			case NoiseType.SimplexNoise4x4x4x4:
				return "SimplexNoise4x4x4x4";
			case NoiseType.SRDNoise2D:
				return "SRDNoise2D";
			case NoiseType.SRNoise:
				return "SRNoise";
			case NoiseType.SRNoise2D:
				return "SRNoise2D";
			default:
				return "UNKNOWN_TYPE";
		}
	}

	// These were kept to help make the GenerateTexture function and were obselete before they were completely finished

	[System.Obsolete]
	/// <summary>
	/// Gets the noise value
	/// </summary>
	/// <param name="type">type of noise</param>
	/// <param name="inputA"></param>
	/// <param name="inputB"></param>
	/// <returns></returns>
	public static float4 GetNoiseValue(NoiseType type, float4 inputA, float4 inputB)
	{
		switch (type)
		{
			case NoiseType.PerlinNoise:
				return F1ToF4(noise.pnoise(F4ToF2(inputA), F4ToF2(inputB)));
			case NoiseType.PerlinNoise3x3x3:
				return F1ToF4(noise.pnoise(F4ToF3(inputA), F4ToF3(inputB)));
			case NoiseType.PerlinNoise4x4x4x4:
				return F1ToF4(noise.pnoise(inputA, inputB));
			case NoiseType.SRDNoise2D:
				return F3ToF4(noise.srdnoise(F4ToF2(inputA), F4ToF1(inputB)));
			case NoiseType.SRNoise2D:
				return F1ToF4(noise.srnoise(F4ToF2(inputA), F4ToF1(inputB)));
			default:
				Debug.LogError("Cannot use given NoiseType");
				return float4.zero;
		}
	}
	[System.Obsolete]
	/// <summary>
	/// gets the noise value
	/// </summary>
	/// <param name="type">type of noise</param>
	/// <param name="input"></param>
	/// <returns></returns>
	public static float4 GetNoiseValue(NoiseType type, float4 input)
	{
		switch (type)
		{
			case NoiseType.CellularNoise:
				return F2ToF4(noise.cellular(F4ToF2(input)));
			case NoiseType.CellularNoise2x2:
				return F2ToF4(noise.cellular2x2(F4ToF2(input)));
			case NoiseType.CellularNoise2x2x2:
				return F2ToF4(noise.cellular2x2x2(F4ToF3(input)));
			case NoiseType.CellularNoise3x3x3:
				return F2ToF4(noise.cellular(F4ToF3(input)));
			case NoiseType.ClassicPerlinNoise:
				return F1ToF4(noise.cnoise(F4ToF2(input)));
			case NoiseType.ClassicPerlinNoise3x3x3:
				return F1ToF4(noise.cnoise(F4ToF3(input)));
			case NoiseType.ClassicPerlinNoise4x4x4x4:
				return F1ToF4(noise.cnoise(input));
			case NoiseType.SimplexNoise:
				return F1ToF4(noise.snoise(F4ToF2(input)));
			case NoiseType.SimplexNoise3x3x3:
				return F1ToF4(noise.snoise(F4ToF3(input)));
			case NoiseType.SimplexNoise4x4x4x4:
				return F1ToF4(noise.snoise(input));
			case NoiseType.SRNoise:
				return F1ToF4(noise.srnoise(F4ToF2(input)));
			default:
				Debug.LogError("Cannot use given NoiseType");
				return float4.zero;
		}
	}

	// These functions are not longer used

	/// <summary>
	/// Converts a float4 to a float2
	/// </summary>
	/// <param name="a"></param>
	/// <returns></returns>
	public static float2 F4ToF2(float4 a)
	{
		return new float2(a.x, a.y);
	}
	/// <summary>
	/// Converts a float4 to a float3
	/// </summary>
	/// <param name="a"></param>
	/// <returns></returns>
	public static float3 F4ToF3(float4 a)
	{
		return new float3(a.x, a.y, a.z);
	}
	/// <summary>
	/// Converts a float4 to a float
	/// </summary>
	/// <param name="a"></param>
	/// <returns></returns>
	public static float F4ToF1(float4 a)
	{
		return a.x;
	}
	/// <summary>
	/// Converts a float3 to a float4
	/// </summary>
	/// <param name="a"></param>
	/// <returns></returns>
	public static float4 F3ToF4(float3 a)
	{
		return new float4(a.x, a.y, a.z, 0);
	}
	/// <summary>
	/// Converts a float2 to a float4
	/// </summary>
	/// <param name="a"></param>
	/// <returns></returns>
	public static float4 F2ToF4(float2 a)
	{
		return new float4(a.x, a.y, 0, 0);
	}
	/// <summary>
	/// Converts a float to a float4
	/// </summary>
	/// <param name="a"></param>
	/// <returns></returns>
	public static float4 F1ToF4(float a)
	{
		return new float4(a, 0, 0, 0);
	}

	// these determines which dimension the noise is in

	public static bool TypeIs2D(NoiseType type)
	{
		switch (type)
		{
			case NoiseClass.NoiseType.PerlinNoise:
			case NoiseClass.NoiseType.SRNoise2D:
			case NoiseClass.NoiseType.SRNoise:
			case NoiseClass.NoiseType.SRDNoise2D:
			case NoiseType.SRDNoise:
			case NoiseClass.NoiseType.ClassicPerlinNoise:
			case NoiseClass.NoiseType.SimplexNoise:
			case NoiseClass.NoiseType.CellularNoise2x2:
			case NoiseClass.NoiseType.CellularNoise:
				return true;
			default:
				return false;
		}
	}

	public static bool TypeIs3D(NoiseType type)
	{
		switch (type)
		{
			case NoiseClass.NoiseType.PerlinNoise3x3x3:
			case NoiseClass.NoiseType.ClassicPerlinNoise3x3x3:
			case NoiseClass.NoiseType.SimplexNoise3x3x3:
			case NoiseClass.NoiseType.CellularNoise2x2x2:
			case NoiseClass.NoiseType.CellularNoise3x3x3:
				return true;
			default:
				return false;
		}
	}

	public static bool TypeIs4D(NoiseType type)
	{
		switch (type)
		{
			case NoiseClass.NoiseType.PerlinNoise4x4x4x4:
			case NoiseClass.NoiseType.ClassicPerlinNoise4x4x4x4:
			case NoiseClass.NoiseType.SimplexNoise4x4x4x4:
				return true;
			default:
				return false;
		}
	}

	// Use this to generate the texture

	/// <summary>
	/// Generates a Texture2D based on the given arguments
	/// </summary>
	/// <param name="type">type of noise to generate</param>
	/// <param name="width">width of texture</param>
	/// <param name="height">height of texture</param>
	/// <param name="length">used in 3D and 4D noise and is treated like another axis</param>
	/// <param name="depth">used in 4D noise and is treated like another axis</param>
	/// <param name="Scale">zoom in/out</param>
	/// <param name="input">the input is the gradient or extra values some noise may need (see the function for more information)</param>
	/// <param name="dimension">
	/// 2 = 2D noise
	/// 3 = 3D noise
	/// 4 = 4D noise
	/// </param>
	/// <returns></returns>

	public static Texture2D GenerateTexture(NoiseType type, int width, int height,int length,int depth, float Scale, float4 input,int dimension = 2)
	{
		Texture2D texture = new Texture2D(width, height);
		float x, y, z, w, value;
		float2 value2;
		float3 value3;
		Color color = new Color();
		for (int i = 0; i < width; i++)
		{
			x = (float)i / width * Scale;
			for (int j = 0; j < height; j++)
			{
				y = (float)j / height * Scale;
				if (dimension == 2)
				{
					switch (type)
					{
						case NoiseClass.NoiseType.PerlinNoise:
							{
								// Classic Perlin noise, periodic variant
								value = noise.pnoise(new float2(x, y), new float2(input.x, input.y));

								color = new Color(value, value, value);
								break;
							}

						case NoiseClass.NoiseType.SRNoise2D:
							{
								// 2-D non-tiling simplex noise with rotating gradients,
								// without the analytical derivative.
								value = noise.srnoise(new float2(x, y), input.x);
								color = new Color(value, value, value);
								break;
							}
						case NoiseClass.NoiseType.SRNoise:
							{
								value = noise.srnoise(new float2(x, y));
								color = new Color(value, value, value);
								break;
							}
						case NoiseClass.NoiseType.SRDNoise2D:
							{
								// 2-D non-tiling simplex noise with rotating gradients and analytical derivative.
								// The first component of the 3-element return vector is the noise value,
								// and the second and third components are the x and y partial derivatives.
								value3 = noise.srdnoise(new float2(x, y), input.x);
								// so for the color we use just the x since y and z are the deriratives

								//color = new Color(value3.x, value3.x, value3.x);
								//  i like the way the derivatives work so i'll keep it like this for now
								color = new Color(value3.x, value3.y, value3.z);
								break;
							}
						case NoiseType.SRDNoise:
							{
								// 2-D non-tiling simplex noise with rotating gradients,
								// without the analytical derivative.
								value3 = noise.srdnoise(new float2(x, y));
								// so for the color we use just the x since y and z are the deriratives

								//color = new Color(value3.x, value3.x, value3.x);
								//  i like the way the derivatives work so i'll keep it like this for now
								color = new Color(value3.x, value3.y, value3.z);
								break;
							}
						case NoiseClass.NoiseType.ClassicPerlinNoise:
							{
								value = noise.cnoise(new float2(x, y));
								color = new Color(value, value, value);
								break;
							}
						case NoiseClass.NoiseType.SimplexNoise:
							{
								value = noise.snoise(new float2(x, y));
								color = new Color(value, value, value);
								break;
							}
						case NoiseClass.NoiseType.CellularNoise2x2:
							{
								value2 = noise.cellular2x2(new float2(x, y));
								color = new Color(value2.x, value2.x, value2.x);
								break;
							}
						case NoiseClass.NoiseType.CellularNoise:
							{
								value2 = noise.cellular(new float2(x, y));
								color = new Color(value2.x, value2.x, value2.x);
								break;
							}
					}
				}
				else if (dimension == 3)
				{
					switch (type)
					{
						case NoiseClass.NoiseType.PerlinNoise3x3x3:
							{
								//	Debug.LogWarning("This Scene doesnto currently support 3D inputs because idk what they are");
								for (int k = 0; k < width; k++)
								{
									x = (float)i / width * Scale;
									y = (float)j / height * Scale;
									z = (float)k / width * Scale;

									value = noise.pnoise(new float3(x, y, z), new float3(input.x, input.y, input.z));

									color = new Color(value, value, value);

									texture.SetPixel(i, j, color);
								}
								break;
							}
						case NoiseClass.NoiseType.ClassicPerlinNoise3x3x3:
							Debug.LogWarning("This Scene doesnto currently support 3D inputs because idk what they are");

							break;
						case NoiseClass.NoiseType.SimplexNoise3x3x3:
							{
								Debug.LogWarning("This Scene doesnto currently support 3D inputs because idk what they are");
								break;
							}
						case NoiseClass.NoiseType.CellularNoise2x2x2:
							{
								Debug.LogWarning("This Scene doesnto currently support 3D inputs because idk what they are");
								break;
							}
						case NoiseClass.NoiseType.CellularNoise3x3x3:
							{
								Debug.LogWarning("This Scene doesnto currently support 3D inputs because idk what they are");
								break;
							}
					}
				}
				else if (dimension == 4)
				{
					switch (type)
					{
						case NoiseClass.NoiseType.PerlinNoise4x4x4x4:
							Debug.LogWarning("This Scene doesnto currently support 4D inputs because idk what they are");
							break;
						case NoiseClass.NoiseType.ClassicPerlinNoise4x4x4x4:
							Debug.LogWarning("This Scene doesnto currently support 4D inputs because idk what they are");
							break;
						case NoiseClass.NoiseType.SimplexNoise4x4x4x4:
							{

								Debug.LogWarning("This Scene doesnto currently support 4D inputs because idk what they are");
								break;
							}
					}
				}
				else
					Debug.LogError("Unknown Dimension "+dimension+" detected!");
				texture.SetPixel(i, j, color);
			}
		}

		texture.Apply();

		return texture;
	}

}

