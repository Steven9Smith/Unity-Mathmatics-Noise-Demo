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
	// Visual Interpertation



	// Noise type
	public NoiseClass.NoiseType NoiseType = NoiseClass.NoiseType.CellularNoise;
	// used to determine dimension of the current noise type
	public bool Is2D = true, Is3D = false, Is4D = false;

	// this is currently unused
	public string FileName = "";

	// scale
	public float Scale = 10f;
	public bool UseScaleAsMax = true;

	// Values that hold Gradients and Rotation Values
	public Vector2 MinMaxValue;

	public float ValueA;
	public float ValueB;
	public float ValueC;
	public float ValueD;

	// Holds the Min and Max Demensions of the texture or Interpertation Method

	public Vector2Int MinMaxWidth = new Vector2Int(0,64);
	public Vector2Int MinMaxHeight = new Vector2Int(0,64);
	// This is used in 3D noise
	public Vector2Int MinMaxLength = new Vector2Int(0, 64);
	// This is used for 4D noise. Depth is not its true name, its just a placeholder name
	public Vector2Int MinMaxDepth = new Vector2Int(0, 64);

	public int width = 64, height = 64,length = 1,depth = 1;

	// Value Interpertation
	public ValueInterpertation ValueInterpertation;

	// holds the values of previous values

	private float4 oldValues;
	private float4 oldSize;
	private float oldScale;
	private NoiseClass.NoiseType OldNoiseType;
	private ValueInterpertation oldValueInterpertation;

	public MeshRenderer mr;

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

			mr.material.mainTexture = NoiseClass.GenerateTexture(NoiseType,new int4(MinMaxWidth.x,MinMaxHeight.x,MinMaxLength.x,MinMaxDepth.x),new int4(width, height,length,depth),ValueInterpertation, Scale, new float4(ValueA, ValueB, ValueC, ValueD),Is2D ? 2 : Is3D ? 3 : Is4D ? 4 : 0);
			oldValues = new float4(ValueA, ValueB, ValueC, ValueD);
			oldScale = Scale;
			OldNoiseType = NoiseType;
			oldSize = new float4(width, height,length,depth);
			oldValueInterpertation = ValueInterpertation;
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
			// test Value Interpertation
			&& ValueInterpertation.Equals(oldValueInterpertation)
		);
	}

}
[CustomEditor(typeof(NoiseComponent))]
public class NoiseEditor : Editor
{

	public NoiseComponent nc;

	private bool MaterialAttributesFoldout = true;
	private bool GradientsAndRotationValuesFoldout = true;


	private bool ValueInterpertation = true;

	private bool ValueInterpertation2DX = true;
	private bool ValueInterpertation2DY = true;


	private bool ValueInterpertation3DX = true;
	private bool ValueInterpertation3DY = true;
	private bool ValueInterpertation3DZ = true;

	private int ValueInterpertationDimension = 1;


	// Save Noise Variables
	private string filenameSave = "";
	private bool SaveAsHeightMap = false;
	private bool SaveFoldout = true;
	// Load Noise variables
	private string filenameLoad = "";
	private bool LoadFoldout = true;

	private const int BUTTON_WIDTH = 200;
	private const int BUTTON_HEIGHT = 20;

	
	public override void OnInspectorGUI()
	{
		//	DrawDefaultInspector();
	//	DrawHeader();
		nc = target as NoiseComponent;
		// Noise Type
		EditorGUILayout.LabelField("Noise Type", EditorStyles.boldLabel);
		nc.NoiseType = (NoiseClass.NoiseType)EditorGUILayout.EnumPopup("Noise Type", nc.NoiseType);

		// determine the dimension
		nc.Is2D = NoiseClass.TypeIs2D(nc.NoiseType);
		if (!nc.Is2D)
			nc.Is3D = NoiseClass.TypeIs3D(nc.NoiseType);
		else
			nc.Is3D = false;
		if (!nc.Is3D)
			nc.Is4D = NoiseClass.TypeIs4D(nc.NoiseType);
		else
			nc.Is4D = false;
		
		
		EditorGUILayout.Space();
		ValueInterpertation = EditorGUILayout.Foldout(ValueInterpertation, "Value Interpitation");
		if (ValueInterpertation)
		{
			ValueInterpertationDimension = nc.ValueInterpertation.ReturnDimension(nc.NoiseType);	
			// Value Handling
			if (ValueInterpertationDimension == 1)
			{
				EditorGUILayout.LabelField("1D Noise Value Return Interpertation", EditorStyles.boldLabel);
				nc.ValueInterpertation.ColorInterpertation1D.x = EditorGUILayout.Toggle("X", nc.ValueInterpertation.ColorInterpertation1D.x);
				nc.ValueInterpertation.ColorInterpertation1D.y = EditorGUILayout.Toggle("Y", nc.ValueInterpertation.ColorInterpertation1D.y);
				nc.ValueInterpertation.ColorInterpertation1D.z = EditorGUILayout.Toggle("Z", nc.ValueInterpertation.ColorInterpertation1D.z);
			}
			else if( ValueInterpertationDimension == 2)
			{
				EditorGUILayout.LabelField("2D Noise Value Return Interpertation", EditorStyles.boldLabel);
				// Handle the Noise Return 2D X options
				ValueInterpertation2DX = EditorGUILayout.Foldout(ValueInterpertation2DX, "Noise Return X Options");
				if (ValueInterpertation2DX)
				{
					// Get the values
					nc.ValueInterpertation.ColorInterpertationA2D.x = EditorGUILayout.Toggle("X", nc.ValueInterpertation.ColorInterpertationA2D.x);
					nc.ValueInterpertation.ColorInterpertationA2D.y = EditorGUILayout.Toggle("Y", nc.ValueInterpertation.ColorInterpertationA2D.y);
					nc.ValueInterpertation.ColorInterpertationA2D.z = EditorGUILayout.Toggle("Z", nc.ValueInterpertation.ColorInterpertationA2D.z);
					// verify there is no conflicts between A2D and B2D
					if (nc.ValueInterpertation.ColorInterpertationA2D.x && nc.ValueInterpertation.ColorInterpertationB2D.x)
						nc.ValueInterpertation.ColorInterpertationB2D.x = false;
					if (nc.ValueInterpertation.ColorInterpertationA2D.y && nc.ValueInterpertation.ColorInterpertationB2D.y)
						nc.ValueInterpertation.ColorInterpertationB2D.y = false;
					if (nc.ValueInterpertation.ColorInterpertationA2D.z && nc.ValueInterpertation.ColorInterpertationB2D.z)
						nc.ValueInterpertation.ColorInterpertationB2D.z = false;
				}
				
				// Handle the Noise Return 2D Y options
				ValueInterpertation2DY = EditorGUILayout.Foldout(ValueInterpertation2DY, "Noise Return Y Options");
				if (ValueInterpertation2DY)
				{
					// Get the values
					nc.ValueInterpertation.ColorInterpertationB2D.x = EditorGUILayout.Toggle("X", nc.ValueInterpertation.ColorInterpertationB2D.x);
					nc.ValueInterpertation.ColorInterpertationB2D.y = EditorGUILayout.Toggle("Y", nc.ValueInterpertation.ColorInterpertationB2D.y);
					nc.ValueInterpertation.ColorInterpertationB2D.z = EditorGUILayout.Toggle("Z", nc.ValueInterpertation.ColorInterpertationB2D.z);
				
				// verify there is no conflicts between A2D and B2D
				if (nc.ValueInterpertation.ColorInterpertationA2D.x && nc.ValueInterpertation.ColorInterpertationB2D.x)
					nc.ValueInterpertation.ColorInterpertationA2D.x = false;
				if (nc.ValueInterpertation.ColorInterpertationA2D.y && nc.ValueInterpertation.ColorInterpertationB2D.y)
					nc.ValueInterpertation.ColorInterpertationA2D.y = false;
				if (nc.ValueInterpertation.ColorInterpertationA2D.z && nc.ValueInterpertation.ColorInterpertationB2D.z)
					nc.ValueInterpertation.ColorInterpertationA2D.z = false;
				}
			}
			else if (ValueInterpertationDimension == 3)
			{
				EditorGUILayout.LabelField("3D Noise Value Return Interpertation", EditorStyles.boldLabel);
				// Handle the Noise Return 3D X options
				ValueInterpertation3DX = EditorGUILayout.Foldout(ValueInterpertation3DX, "Noise Return X Options");
				if (ValueInterpertation3DX)
				{
					// Get the values
					nc.ValueInterpertation.ColorInterpertationA3D.x = EditorGUILayout.Toggle("X", nc.ValueInterpertation.ColorInterpertationA3D.x);
					nc.ValueInterpertation.ColorInterpertationA3D.y = EditorGUILayout.Toggle("Y", nc.ValueInterpertation.ColorInterpertationA3D.y);
					nc.ValueInterpertation.ColorInterpertationA3D.z = EditorGUILayout.Toggle("Z", nc.ValueInterpertation.ColorInterpertationA3D.z);
					// verify there is no conflicts between A3D and B3D and C3D
					if (nc.ValueInterpertation.ColorInterpertationA3D.x && nc.ValueInterpertation.ColorInterpertationB3D.x)
						nc.ValueInterpertation.ColorInterpertationB3D.x = false;
					if (nc.ValueInterpertation.ColorInterpertationA3D.y && nc.ValueInterpertation.ColorInterpertationB3D.y)
						nc.ValueInterpertation.ColorInterpertationB3D.y = false;
					if (nc.ValueInterpertation.ColorInterpertationA3D.z && nc.ValueInterpertation.ColorInterpertationB3D.z)
						nc.ValueInterpertation.ColorInterpertationB3D.z = false;
					if (nc.ValueInterpertation.ColorInterpertationA3D.x && nc.ValueInterpertation.ColorInterpertationC3D.x)
						nc.ValueInterpertation.ColorInterpertationC3D.x = false;
					if (nc.ValueInterpertation.ColorInterpertationA3D.y && nc.ValueInterpertation.ColorInterpertationC3D.y)
						nc.ValueInterpertation.ColorInterpertationC3D.y = false;
					if (nc.ValueInterpertation.ColorInterpertationA3D.z && nc.ValueInterpertation.ColorInterpertationC3D.z)
						nc.ValueInterpertation.ColorInterpertationC3D.z = false;
				}

				// Handle the Noise Return 3D Y options
				ValueInterpertation3DY = EditorGUILayout.Foldout(ValueInterpertation3DY, "Noise Return Y Options");
				if (ValueInterpertation3DY)
				{
					// Get the values
					nc.ValueInterpertation.ColorInterpertationB3D.x = EditorGUILayout.Toggle("X", nc.ValueInterpertation.ColorInterpertationB3D.x);
					nc.ValueInterpertation.ColorInterpertationB3D.y = EditorGUILayout.Toggle("Y", nc.ValueInterpertation.ColorInterpertationB3D.y);
					nc.ValueInterpertation.ColorInterpertationB3D.z = EditorGUILayout.Toggle("Z", nc.ValueInterpertation.ColorInterpertationB3D.z);
					// verify there is no conflicts between A3D and B3D and C3D
					if (nc.ValueInterpertation.ColorInterpertationA3D.x && nc.ValueInterpertation.ColorInterpertationB3D.x)
						nc.ValueInterpertation.ColorInterpertationA3D.x = false;
					if (nc.ValueInterpertation.ColorInterpertationA3D.y && nc.ValueInterpertation.ColorInterpertationB3D.y)
						nc.ValueInterpertation.ColorInterpertationA3D.y = false;
					if (nc.ValueInterpertation.ColorInterpertationA3D.z && nc.ValueInterpertation.ColorInterpertationB3D.z)
						nc.ValueInterpertation.ColorInterpertationA3D.z = false;
					if (nc.ValueInterpertation.ColorInterpertationB3D.x && nc.ValueInterpertation.ColorInterpertationC3D.x)
						nc.ValueInterpertation.ColorInterpertationC3D.x = false;
					if (nc.ValueInterpertation.ColorInterpertationB3D.y && nc.ValueInterpertation.ColorInterpertationC3D.y)
						nc.ValueInterpertation.ColorInterpertationC3D.y = false;
					if (nc.ValueInterpertation.ColorInterpertationB3D.z && nc.ValueInterpertation.ColorInterpertationC3D.z)
						nc.ValueInterpertation.ColorInterpertationC3D.z = false;
				}

				// Handle the Noise Return 3D Z options
				ValueInterpertation3DZ = EditorGUILayout.Foldout(ValueInterpertation3DZ, "Noise Return Z Options");
				if (ValueInterpertation3DZ)
				{
					// Get the values
					nc.ValueInterpertation.ColorInterpertationC3D.x = EditorGUILayout.Toggle("X", nc.ValueInterpertation.ColorInterpertationC3D.x);
					nc.ValueInterpertation.ColorInterpertationC3D.y = EditorGUILayout.Toggle("Y", nc.ValueInterpertation.ColorInterpertationC3D.y);
					nc.ValueInterpertation.ColorInterpertationC3D.z = EditorGUILayout.Toggle("Z", nc.ValueInterpertation.ColorInterpertationC3D.z);
					// verify there is no conflicts between A3D and B3D and C3D
					if (nc.ValueInterpertation.ColorInterpertationA3D.x && nc.ValueInterpertation.ColorInterpertationC3D.x)
						nc.ValueInterpertation.ColorInterpertationA3D.x = false;
					if (nc.ValueInterpertation.ColorInterpertationA3D.y && nc.ValueInterpertation.ColorInterpertationC3D.y)
						nc.ValueInterpertation.ColorInterpertationA3D.y = false;
					if (nc.ValueInterpertation.ColorInterpertationA3D.z && nc.ValueInterpertation.ColorInterpertationC3D.z)
						nc.ValueInterpertation.ColorInterpertationA3D.z = false;
					if (nc.ValueInterpertation.ColorInterpertationB3D.x && nc.ValueInterpertation.ColorInterpertationC3D.x)
						nc.ValueInterpertation.ColorInterpertationB3D.x = false;
					if (nc.ValueInterpertation.ColorInterpertationB3D.y && nc.ValueInterpertation.ColorInterpertationC3D.y)
						nc.ValueInterpertation.ColorInterpertationB3D.y = false;
					if (nc.ValueInterpertation.ColorInterpertationB3D.z && nc.ValueInterpertation.ColorInterpertationC3D.z)
						nc.ValueInterpertation.ColorInterpertationB3D.z = false;
				}
			}
			else Debug.LogError("Detected Unknown dimension!");
		}

		// Material Attributes
		EditorGUILayout.Space();
		MaterialAttributesFoldout = EditorGUILayout.Foldout(MaterialAttributesFoldout, "Material Attributes");
		if (MaterialAttributesFoldout)
		{
			// Material Attributes
		
			//		Scale
			nc.UseScaleAsMax = EditorGUILayout.Toggle("Use Scale As Size Limit", nc.UseScaleAsMax);
			nc.Scale = EditorGUILayout.FloatField("Scale", nc.Scale);
			//Update MinMaxValue is UseScaleAsMax is true
			if (nc.UseScaleAsMax)
				nc.MinMaxValue = new float2(0, nc.Scale);
			else
				nc.MinMaxValue = EditorGUILayout.Vector2Field("MinMax Value", nc.MinMaxValue);

			//		Width
			nc.MinMaxWidth = EditorGUILayout.Vector2IntField("Min Max Width", nc.MinMaxWidth);
			if (nc.MinMaxWidth.x > nc.MinMaxWidth.y)
				nc.MinMaxWidth.x = nc.MinMaxWidth.y - 1;
			//		Height
			nc.MinMaxHeight = EditorGUILayout.Vector2IntField("Min Max Height", nc.MinMaxHeight);
			if (nc.MinMaxHeight.x > nc.MinMaxHeight.y)
				nc.MinMaxHeight.x = nc.MinMaxHeight.y - 1;
			
			//		Length
			if (nc.Is3D || nc.Is4D)
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
			nc.width = EditorGUILayout.IntSlider("Width", nc.width, nc.MinMaxWidth.x, nc.MinMaxWidth.y);
			nc.height = EditorGUILayout.IntSlider("Height", nc.height, nc.MinMaxHeight.x, nc.MinMaxHeight.y);
			if(nc.Is3D || nc.Is4D)
				nc.length = EditorGUILayout.IntSlider("Length", nc.length, nc.MinMaxLength.x, nc.MinMaxLength.y);
			if (nc.Is4D)
				nc.depth = EditorGUILayout.IntSlider("Depth", nc.depth, nc.MinMaxDepth.x, nc.MinMaxDepth.y);
			
		}

		EditorGUILayout.Space();

		// Gradients and Rotation foldout
		GradientsAndRotationValuesFoldout = EditorGUILayout.Foldout(GradientsAndRotationValuesFoldout,"Gradients and Rotations");
		if (GradientsAndRotationValuesFoldout)
		{
			

		//	GradientsAndRotationValuesFoldout = EditorGUILayout.Foldout(GradientsAndRotationValuesFoldout, "Fradients and Rotation");

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
					DisplaySliders(new string[] { "Period X", "Period Y", "Period Z" });
					break;
				case NoiseClass.NoiseType.PerlinNoise4x4x4x4:

					DisplaySliders(new string[] { "Period X", "Period Y", "Period Z", "Period W" });
					break;
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

		EditorGUILayout.Space();

		// Noise Save foldout
		SaveFoldout = EditorGUILayout.Foldout(SaveFoldout, "Save Noise");
		if (SaveFoldout)
		{
			filenameSave = EditorGUILayout.TextField("filename",filenameSave);
			SaveAsHeightMap = EditorGUILayout.Toggle("Save As Height Map",SaveAsHeightMap);
			if (GUILayout.Button("Save Noise Profile",GUILayout.Width(BUTTON_WIDTH),GUILayout.Height(BUTTON_HEIGHT)))
			{
				if (filenameSave == "")
					filenameSave = "_blank";
				if (!SaveAsHeightMap)
					Core.Procedural.NoiseSaveDataClass.SaveNoise(nc.NoiseType, filenameSave, nc.mr.material, new float4(nc.ValueA, nc.ValueB, nc.ValueC, nc.ValueD));
				else
					Core.Procedural.NoiseSaveDataClass.SaveHeightMap(nc.NoiseType, filenameSave, nc.mr.material);
			}
			if(GUILayout.Button("Export Noise Profile", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
			{
				if (filenameSave == "")
					filenameSave = "_blank";
				Core.Procedural.NoiseSaveDataClass.ExportNoiseProfile(nc.NoiseType, filenameSave, nc.mr.material, new float4(nc.ValueA, nc.ValueB, nc.ValueC, nc.ValueD));
			}
		}
		// Noise Load foldout
		LoadFoldout = EditorGUILayout.Foldout(LoadFoldout, "Load Noise");
		if (LoadFoldout)
		{
			filenameLoad = EditorGUILayout.TextField("file path",filenameLoad);
			if(GUILayout.Button("Load Noise Profile/Heightmap", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
			{
				if (filenameLoad == "")
					filenameLoad = "_blank";
				string[] path_split = filenameLoad.Split('/');
				string[] filename_split = path_split[path_split.Length - 1].Split('.');
				if (filename_split.Length > 1)
				{
					if(filename_split[1] == "map")
					{
						Debug.LogWarning("Note: loading a height map will not change noise type and other non-color values.");
						float[][] heights;
						if (Core.Procedural.NoiseSaveDataClass.LoadHeightMap(filenameLoad, out heights)) {
							Texture2D texture;
							if (Core.Procedural.NoiseSaveDataClass.HeightMapToTexture2D(heights,out texture))
							{
								nc.mr.material.mainTexture = texture;
								Debug.Log("Successfuly loaded the heightmap");
							}
						}
					}
					else
					{
						Debug.Log("attempting to load noise profile");
						//assumes .dat
						Texture2D texture2D;
						float4 values;
						NoiseClass.NoiseType type;
						if (Core.Procedural.NoiseSaveDataClass.LoadNoise(filenameLoad, out texture2D, out values, out type))
						{
							nc.NoiseType = type;
							nc.mr.material.mainTexture = texture2D;
							nc.ValueA = values.x;
							nc.ValueB = values.y;
							nc.ValueC = values.z;
							nc.ValueD = values.w;
							Debug.Log("Successfully Loaded the Noise profile!");
						}
					}

				}
				else
					Debug.LogWarning("Cannot load a file without an extension");
			}
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
	/*
	 Picking the gradients
	 
		For the noise function to be repeatable, i.e. always yield the same value for a given 
		input point, gradients need to be pseudo-random, not truly random. They need to have 
		enough variation to conceal the fact that the function is not truly random, but too 
		much variation will cause unpre-dictable behaviour for the noise function. A good 
		choice for 2D and higher is to pick gradients of unit length but different directions.
		For 2D, 8 or 16 gradients distributed around the unit circle is a good choice. For 3D,
		Ken Perlin’s recommended set of gradients is the midpoints of each of the 12 edges of
		a cube centered on the origin.
		 
	*/


	private static float3[] gradient3D = new float3[]{new float3(1,1,0),new float3(-1,1,0),new float3(1,-1,0),new float3(-1,-1,0),
								 new float3(1,0,1),new float3(-1,0,1),new float3(1,0,-1),new float3(-1,0,-1),
								 new float3(0,1,1),new float3(0,-1,1),new float3(0,1,-1),new float3(0,-1,-1) };

	private static float4[] gradient4D = new float4[]{new float4(0, 1, 1, 1), new float4(0, 1, 1, -1), new float4(0, 1, -1, 1), new float4(0, 1, -1, -1),
				   new float4(0, -1, 1, 1), new float4(0, -1, 1, -1), new float4(0, -1, -1, 1), new float4(0, -1, -1, -1),
				   new float4(1, 0, 1, 1), new float4(1, 0, 1, -1), new float4(1, 0, -1, 1), new float4(1, 0, -1, -1),
				   new float4(-1, 0, 1, 1), new float4(-1, 0, 1, -1), new float4(-1, 0, -1, 1), new float4(-1, 0, -1, -1),
				   new float4(1, 1, 0, 1), new float4(1, 1, 0, -1), new float4(1, -1, 0, 1), new float4(1, -1, 0, -1),
				   new float4(-1, 1, 0, 1), new float4(-1, 1, 0, -1), new float4(-1, -1, 0, 1), new float4(-1, -1, 0, -1),
				   new float4(1, 1, 1, 0), new float4(1, 1, -1, 0), new float4(1, -1, 1, 0), new float4(1, -1, -1, 0),
				   new float4(-1, 1, 1, 0), new float4(-1, 1, -1, 0), new float4(-1, -1, 1, 0), new float4(-1, -1, -1, 0)};

	public enum VisualInterpertation
	{
		Texture,
		Shape3D,
		Terrain,
		Map3D
	}

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
		SRNoise2D,
		SimplexNoise3x3x3Gradient

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
			case NoiseType.SimplexNoise3x3x3Gradient:
				return "SimplexNoise3x3x3Gradient";
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
			case NoiseType.PerlinNoise:
			case NoiseType.SRNoise2D:
			case NoiseType.SRNoise:
			case NoiseType.SRDNoise2D:
			case NoiseType.SRDNoise:
			case NoiseType.ClassicPerlinNoise:
			case NoiseType.SimplexNoise:
			case NoiseType.CellularNoise2x2:
			case NoiseType.CellularNoise:
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
			case NoiseType.SimplexNoise3x3x3Gradient:
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

	public static Texture2D GenerateTexture(NoiseType type,int4 minDimensions, int4 dimensions,ValueInterpertation valueInterpertation, float Scale, float4 input,int dimension = 2)
	{
		Texture2D texture = new Texture2D(dimensions.x, dimensions.y);
		float x, y, z, w, value;
		float2 value2;
		float3 value3;
		Color color = new Color();
		for (int i = minDimensions.x; i < dimensions.x; i++)
		{
			x = (float)i / dimensions.x * Scale;
			for (int j = minDimensions.y; j < dimensions.y; j++)
			{
				y = (float)j / dimensions.y * Scale;
				if (dimension == 2)
				{
					switch (type)
					{
						case NoiseClass.NoiseType.PerlinNoise:
							{
								// Classic Perlin noise, periodic variant
								value = noise.pnoise(new float2(x, y), new float2(input.x, input.y));

								color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
								break;
							}

						case NoiseClass.NoiseType.SRNoise2D:
							{
								// 2-D non-tiling simplex noise with rotating gradients,
								// without the analytical derivative.
								value = noise.srnoise(new float2(x, y), input.x);
								color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
								break;
							}
						case NoiseClass.NoiseType.SRNoise:
							{
								value = noise.srnoise(new float2(x, y));
								color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
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
								//	color = new Color(value3.x, value3.y, value3.z);

								// new color system based on the value interperter
								color = new Color(
									valueInterpertation.ColorInterpertationA3D.x ? value3.x : valueInterpertation.ColorInterpertationB3D.x ? value3.y : valueInterpertation.ColorInterpertationC3D.x ? value3.z : 0,
									valueInterpertation.ColorInterpertationA3D.y ? value3.x : valueInterpertation.ColorInterpertationB3D.y ? value3.y : valueInterpertation.ColorInterpertationC3D.y ? value3.z : 0,
									valueInterpertation.ColorInterpertationA3D.z ? value3.x : valueInterpertation.ColorInterpertationB3D.z ? value3.y : valueInterpertation.ColorInterpertationC3D.z ? value3.z : 0
								);


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
								//	color = new Color(value3.x, value3.y, value3.z);
								color = new Color(
										valueInterpertation.ColorInterpertationA3D.x ? value3.x : valueInterpertation.ColorInterpertationB3D.x ? value3.y : valueInterpertation.ColorInterpertationC3D.x ? value3.z : 0,
										valueInterpertation.ColorInterpertationA3D.y ? value3.x : valueInterpertation.ColorInterpertationB3D.y ? value3.y : valueInterpertation.ColorInterpertationC3D.y ? value3.z : 0,
										valueInterpertation.ColorInterpertationA3D.z ? value3.x : valueInterpertation.ColorInterpertationB3D.z ? value3.y : valueInterpertation.ColorInterpertationC3D.z ? value3.z : 0
									);

								break;
							}
						case NoiseClass.NoiseType.ClassicPerlinNoise:
							{
								value = noise.cnoise(new float2(x, y));
								color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
								break;
							}
						case NoiseClass.NoiseType.SimplexNoise:
							{
								value = noise.snoise(new float2(x, y));
								color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
								break;
							}
						case NoiseClass.NoiseType.CellularNoise2x2:
							{
								value2 = noise.cellular2x2(new float2(x, y));
							//	color = new Color(value2.x, value2.x, value2.x);

								color = new Color(
									valueInterpertation.ColorInterpertationA2D.x ? value2.x : valueInterpertation.ColorInterpertationB2D.x ? value2.y : 0,
									valueInterpertation.ColorInterpertationA2D.y ? value2.x : valueInterpertation.ColorInterpertationB2D.y ? value2.y : 0,
									valueInterpertation.ColorInterpertationA2D.z ? value2.x : valueInterpertation.ColorInterpertationB2D.z ? value2.y : 0
								);
								break;
							}
						case NoiseClass.NoiseType.CellularNoise:
							{
								value2 = noise.cellular(new float2(x, y));
								//	color = new Color(value2.x, value2.x, value2.x);

								color = new Color(
									valueInterpertation.ColorInterpertationA2D.x ? value2.x : valueInterpertation.ColorInterpertationB2D.x ? value2.y : 0,
									valueInterpertation.ColorInterpertationA2D.y ? value2.x : valueInterpertation.ColorInterpertationB2D.y ? value2.y : 0,
									valueInterpertation.ColorInterpertationA2D.z ? value2.x : valueInterpertation.ColorInterpertationB2D.z ? value2.y : 0
								);
								break;
							}


					}
				}
				else if (dimension == 3)
				{
					for (int k = minDimensions.z; k < dimensions.z; k++)
					{
						z = (float)k / dimensions.z * Scale;
						switch (type)
						{
							case NoiseClass.NoiseType.PerlinNoise3x3x3:
								{
									// Classic Perlin noise, periodic variant
									value = noise.pnoise(new float3(x, y, z), new float3(input.x, input.y, input.z));

									color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);

									break;
								}
							case NoiseClass.NoiseType.ClassicPerlinNoise3x3x3:
								{
									// Classic Perlin noise
									value = noise.cnoise(new float3(x,y,z));

									color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
									break;
								}
							case NoiseClass.NoiseType.SimplexNoise3x3x3:
								{
									value = noise.snoise(new float3(x,y,z));

									color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
									break;
								}
							case NoiseType.SimplexNoise3x3x3Gradient:
								{
									// idk what to do with the gradient
									float3 gradient = new float3();
									value = noise.snoise(new float3(x,y,z), out gradient);
									color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
									break;
								}
							case NoiseClass.NoiseType.CellularNoise2x2x2:
								{
									// Cellular noise, returning F1 and F2 in a float2.
									// Speeded up by umath.sing 2x2x2 search window instead of 3x3x3,
									// at the expense of some pattern artifacts.
									// F2 is often wrong and has sharp discontinuities.
									// If you need a good F2, use the slower 3x3x3 version.
									value2 = noise.cellular2x2x2(new float3(x,y,z));


									//	color = new Color(value2.x, value2.x, value2.x);
									color = new Color(
										valueInterpertation.ColorInterpertationA2D.x ? value2.x : valueInterpertation.ColorInterpertationB2D.x ? value2.y : 0,
										valueInterpertation.ColorInterpertationA2D.y ? value2.x : valueInterpertation.ColorInterpertationB2D.y ? value2.y : 0,
										valueInterpertation.ColorInterpertationA2D.z ? value2.x : valueInterpertation.ColorInterpertationB2D.z ? value2.y : 0
									);
									break;
								}
							case NoiseClass.NoiseType.CellularNoise3x3x3:
								{
									// Cellular noise, returning F1 and F2 in a float2.
									// 3x3x3 search region for good F2 everywhere, but a lot
									// slower than the 2x2x2 version.
									// The code below is a bit scary even to its author,
									// but it has at least half decent performance on a
									// math.modern GPU. In any case, it beats any software
									// implementation of Worley noise hands down.
									value2 = noise.cellular(new float3(x,y,z));


									//	color = new Color(value2.x, value2.x, value2.x);
									color = new Color(
										valueInterpertation.ColorInterpertationA2D.x ? value2.x : valueInterpertation.ColorInterpertationB2D.x ? value2.y : 0,
										valueInterpertation.ColorInterpertationA2D.y ? value2.x : valueInterpertation.ColorInterpertationB2D.y ? value2.y : 0,
										valueInterpertation.ColorInterpertationA2D.z ? value2.x : valueInterpertation.ColorInterpertationB2D.z ? value2.y : 0
									);
									break;
								}


						}
					}
				}
				else if (dimension == 4)
				{
					for (int k = minDimensions.z; k < dimensions.z; k++)
					{
						z = (float)k / dimensions.z * Scale;
						for (int l = minDimensions.w; l < dimensions.w; l++)
						{
							w = (float)l / dimensions.w * Scale;
							switch (type)
							{
								case NoiseClass.NoiseType.PerlinNoise4x4x4x4:
							//		Debug.LogWarning("This Scene doesnto currently support 4D inputs because idk what they are");
									value = noise.pnoise(new float4(x,y,z,w), input);
									color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
									break;
								case NoiseClass.NoiseType.ClassicPerlinNoise4x4x4x4:
							//		Debug.LogWarning("This Scene doesnto currently support 4D inputs because idk what they are");
									value = noise.cnoise(new float4(x, y, z, w));
									color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
									break;
								case NoiseClass.NoiseType.SimplexNoise4x4x4x4:
									{
							//			Debug.LogWarning("This Scene doesnto currently support 4D inputs because idk what they are");
										value = noise.snoise(new float4(x, y, z, w));
										color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
										break;
									}
							}
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

public struct ValueInterpertation
{
	// interpertation values (only works in 1D, 2D, and 3D return values)

	// 1D options

		// chooses which rgb value to be set to the noise value
		public bool3 ColorInterpertation1D;
	// 2D options
		// chooses which rgb value to be set to the noise value x
		public bool3 ColorInterpertationA2D;
		// chooses which rgb value to be set to the noise value y
		public bool3 ColorInterpertationB2D;
	// 3D options
		// chooses which rgb value to be set to the noise value x
		public bool3 ColorInterpertationA3D;
		// chooses which rgb value to be set to the noise value y
		public bool3 ColorInterpertationB3D;
		// chooses which rgb value to be set to the noise value z
		public bool3 ColorInterpertationC3D;

	public int ReturnDimension(NoiseClass.NoiseType type)
	{
		switch (type)
		{
			case NoiseClass.NoiseType.SimplexNoise4x4x4x4:
			case NoiseClass.NoiseType.PerlinNoise3x3x3:
			case NoiseClass.NoiseType.ClassicPerlinNoise3x3x3:
			case NoiseClass.NoiseType.SimplexNoise3x3x3:
			case NoiseClass.NoiseType.SimplexNoise3x3x3Gradient:
			case NoiseClass.NoiseType.PerlinNoise:
			case NoiseClass.NoiseType.SRNoise2D:
			case NoiseClass.NoiseType.SRNoise:
			case NoiseClass.NoiseType.ClassicPerlinNoise:
			case NoiseClass.NoiseType.SimplexNoise:
			case NoiseClass.NoiseType.ClassicPerlinNoise4x4x4x4:
			case NoiseClass.NoiseType.PerlinNoise4x4x4x4:
				return 1;
			case NoiseClass.NoiseType.CellularNoise2x2x2:
			case NoiseClass.NoiseType.CellularNoise3x3x3:
			case NoiseClass.NoiseType.CellularNoise:
			case NoiseClass.NoiseType.CellularNoise2x2:
				return 2;
			case NoiseClass.NoiseType.SRDNoise:
			case NoiseClass.NoiseType.SRDNoise2D:
				return 3;

		}
		return 0;
	}
	public override bool Equals(object obj)
	{
		ValueInterpertation vi = (ValueInterpertation)obj;
		return (
			ColorInterpertation1D.Equals(vi.ColorInterpertation1D)
			&& ColorInterpertationA2D.Equals(vi.ColorInterpertationA2D)
			&& ColorInterpertationB2D.Equals(vi.ColorInterpertationB2D)
			&& ColorInterpertationA3D.Equals(vi.ColorInterpertationA3D)
			&& ColorInterpertationB3D.Equals(vi.ColorInterpertationB3D)
			&& ColorInterpertationC3D.Equals(vi.ColorInterpertationC3D)
			
		);
	}
}

