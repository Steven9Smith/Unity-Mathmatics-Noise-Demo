using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.UI;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
/// <summary>
/// This Component is the thing the user will interact with
/// NOTE: This will only work when you hit Play
/// </summary>
public class NoiseComponent : MonoBehaviour
{
	// Offset of the Dummy GameObject on the Z axis
	private const int Z_OFFSET = 20;

	// Visual Interpertation
	public VisualInterpertationSettings VisualInterpertationSettings;

	// Dynamic toolbar Variables
	public int tab = 0;
	public List<string> tabNames = new List<string>();
	public string eArithmetic = "";
	public string eMapArithmetic = "";

	// Dynamic Noise Profile Editor Values
	public List<NoiseProfileEditorAttributes> noiseProfileEditorAttributes = new List<NoiseProfileEditorAttributes>();

	private VisualInterpertationSettings oldVisualInterpertationSettings;
	// GameObject things

	//		This will be refered to as the Final Noise Profile since it will hold the image that will effect the Visual Interpertation
	public MeshRenderer meshRenderer;

	public MeshFilter meshFilter;
	//		this dummy gameobject will be used for different Visual Interpertations
	public GameObject Dummy;
	//		this material will be used on the dummy for different Visual Interpertations
	public Material DefaultMaterial;
	//		 display textures based on which Noise Profile you have currently selected
	public MeshRenderer meshRenderer2;

	// Noise Profiles
	public List<NoiseProfileOptions> noiseProfileOptions = new List<NoiseProfileOptions>();
	// arithmetic variables
	public string[] arithmetic = new string[0];
	public bool performArithmetic = false;

	public string[] mapArithmetic =new string[0];
	public bool performMapArithmetic = false;

	public Texture2D Map;
	// determines if the noise values were changed
	public bool NoiseValuesChange = false;

//	public int textureOverride = -1;
	

	// Start is called before the first frame update
	void Start()
	{
		// get our components
		meshFilter = GetComponent<MeshFilter>(); 
		meshRenderer = GetComponent<MeshRenderer>();
		if (meshRenderer == null)
			Debug.LogError("Failed to get MeshRenderer!");
		else if (meshFilter == null)
			Debug.LogError("Failed to get MeshFilter!");
		// we need to store this for later to be used on the terrain material
		DefaultMaterial = Material.Instantiate(meshRenderer.material);
		// we are going to create another Quad to display textures based on which Noise Profile you have currently selected
		// remeber we got to create copies and not references!
		GameObject otherDisplay = GameObject.CreatePrimitive(PrimitiveType.Quad);
		meshRenderer2 = otherDisplay.GetComponent<MeshRenderer>();
		meshRenderer2.material = Material.Instantiate(DefaultMaterial);
	}

	// Update is called once per frame
	void Update()
	{
		// Only Update if we have at least one noise profile
		if (noiseProfileOptions.Count > 0)
		{
			// first we need to check if any value was changed on any of the noise textures
			NoiseValuesChange = false;
			for (int i = 0; i < noiseProfileOptions.Count; i++)
			{
				// hold the result so we don't have to call it more than once
				bool tmp = noiseProfileOptions[i].ValuesChange();
				if (!NoiseValuesChange)
					NoiseValuesChange = tmp;
				if (tmp)
					noiseProfileOptions[i].UpdateTexture();
			}
			
			// update the main texture if arithmetic is detected
			if (performArithmetic && noiseProfileOptions.Count > 1 && NoiseValuesChange)
			{
			//	Debug.Log("performing arithmetic!");
				// Now we have to update the true material

				// Get the index of the Noise Profile that wan'ts to be used as the main texture
				int exclude_index = NoiseClass.GetIndexOfNoiseProfileWithUseAsMainTexture(noiseProfileOptions);
				// This will be modified texture that will be later applied to the real one
				Texture2D mainTexture;

				if (exclude_index == -1)
					// set the texture to black (0,0,0) if no Noise Profile is set to be the main starting texture
					mainTexture = NoiseClass.GenerateOneColorTexture2D(noiseProfileOptions[0].texture_data.Length, noiseProfileOptions[0].texture_data[0].Length, new Color(0, 0, 0));
				else
				{
					// set the texture to the excluded one since we are using it as the starting texture to be modified
					mainTexture = noiseProfileOptions[exclude_index].GetTexture();
					// update the arrithmetic string to ignore this texture in the later operation
					arithmetic[exclude_index] = "i";
				}

				HandleArithmeticOperations(ref mainTexture, arithmetic);
				
				// now we change the main texture of the Final Noise Profile
				meshRenderer.material.mainTexture = mainTexture;
			}
			else if (NoiseValuesChange == true && noiseProfileOptions.Count > 1)
			{
				// go through and set the texture based on what is disabled and enabled
				// This loop does not discriminate
				for (int i = 0; i < noiseProfileOptions.Count; i++)
					if (noiseProfileOptions[i].profileMode == NoiseClass.NoiseProfileMode.Texture)
						meshRenderer.material.mainTexture = noiseProfileOptions[i].GetTexture();
			}
			else if (NoiseValuesChange)
			{
				// just set the texture the first noise profile
				meshRenderer.material.mainTexture = noiseProfileOptions[0].GetTexture();
			}

			// Map Arithmetic
			List<NoiseProfileOptions> maps = GetActiveMaps(noiseProfileOptions);
			if (performMapArithmetic && maps.Count > 1)
			{
				// maps start with a black texture 
				Texture2D mapTexture = NoiseClass.GenerateOneColorTexture2D(meshRenderer.material.mainTexture.width, meshRenderer.material.mainTexture.height, new Color(0, 0, 0));

				HandleArithmeticOperations(ref mapTexture, mapArithmetic);
				
				// now we change the main texture of the Final Noise Profile
				Map = mapTexture;
			}
			else if (maps.Count > 0)
				Map = maps[0].GetTexture();
			else
				Map = (Texture2D)meshRenderer.material.mainTexture;

			// Now that the main texture has been updated we have to update the visual interpertation based on it

			if (NonNoiseValuesChange() || NoiseValuesChange || performArithmetic)
			{
				// test if the visual interpertation changes
				if (VisualInterpertationSettings.visualInterpertation != oldVisualInterpertationSettings.visualInterpertation)
					ChangeVisualInterpertation();
				// update the visual interpertation
				UpdateVisualInterpertation();

				oldVisualInterpertationSettings = VisualInterpertationSettings;
			}
		}
	}

	public void HandleArithmeticOperations(ref Texture2D texture, string[] arithmeticString)
	{
		/* Arithmetic symbols:
				 * +		performs matrix addition of the main texture to the indexed texture
				 * -		performs matrix subtraction of the main texture to the indexed texture
				 * /		performs matrix division of the main texture to the indexed texture
				 * *		performs matrix multiplication of the main texture to the indexed texture
				 * avg		averages each pixel of the main texture to the index texture
				 */


		// go through the arithmetic and performs the operations
		for (int i = 0; i < arithmeticString.Length; i++)
		{
			// check if the current Noise Profile is supposed to be ignored 
			if (arithmeticString[i] != "i")
			{
				int4 cropSection = noiseProfileOptions[i].crop.GetCropSection();
				//Note: With Arithmetic you must make sure all Noise Profiles are all the same size
				int startWidth = noiseProfileOptions[i].crop.useCroppedSection ? cropSection.x : 0;
				int width = noiseProfileOptions[i].crop.useCroppedSection ? cropSection.y : noiseProfileOptions[i].texture_data.Length;
				int startHeight = noiseProfileOptions[i].crop.useCroppedSection ? cropSection.z : 0;
				int height = noiseProfileOptions[i].crop.useCroppedSection ? cropSection.w : noiseProfileOptions[i].texture_data[0].Length;

				// the Noise Profile is not ignored so we perform the operations on each pixel
				for (int j = startWidth; j < width; j++)
				{
					for (int k = startHeight; k < height; k++)
					{
						// mainTexture pixel
						Color mPixel = texture.GetPixel(j, k);
						// other texture pixel
						Color oPixel = new Color(noiseProfileOptions[i].texture_data[j][k].x, noiseProfileOptions[i].texture_data[j][k].y, noiseProfileOptions[i].texture_data[j][k].z);
						//	Color oPixel = noiseProfileOptions[i].GetTexture().GetPixel(i,j);
						switch (arithmeticString[i])
						{
							case "+":
								{
									texture.SetPixel(j, k, new Color(
										math.clamp(mPixel.r + oPixel.r, 0, 255),
										math.clamp(mPixel.g + oPixel.g, 0, 255),
										math.clamp(mPixel.b + oPixel.b, 0, 255)
									));
								}
								break;
							case "-":
								{
									texture.SetPixel(j, k, new Color(
										math.clamp(mPixel.r - oPixel.r, 0, 255),
										math.clamp(mPixel.g - oPixel.g, 0, 255),
										math.clamp(mPixel.b - oPixel.b, 0, 255)
									));
								}
								break;
							case "/":
								{
									texture.SetPixel(j, k, new Color(
										math.clamp(mPixel.r / oPixel.r, 0, 255),
										math.clamp(mPixel.g / oPixel.g, 0, 255),
										math.clamp(mPixel.b / oPixel.b, 0, 255)
									));
								}
								break;
							case "*":
								{
									texture.SetPixel(j, k, new Color(
										   math.clamp(mPixel.r * oPixel.r, 0, 255),
										   math.clamp(mPixel.g * oPixel.g, 0, 255),
										   math.clamp(mPixel.b * oPixel.b, 0, 255)
									   ));
								}
								break;
							case "avg":
								{
									texture.SetPixel(j, k, new Color(
										math.clamp((mPixel.r + oPixel.r) / 2, 0, 255),
										math.clamp((mPixel.g + oPixel.g) / 2, 0, 255),
										math.clamp((mPixel.b + oPixel.b) / 2, 0, 255)
									));
								}
								break;
							case "i":
								// ignore
								break;
							default:
								Debug.LogWarning("Detected an unknown arithmatic symbol \"" + arithmeticString[i] + "\" thus skipping texture arithmetic at index " + i + " with stirng \"" + System.String.Join(" ", arithmeticString) + "\"");
								break;
						}


					}
				}
				// apply the changes
				texture.Apply();
			}
		}
	}

	/// <summary>
	 /// returns a list of all Active Map Noise Profiles within a List of Profiles
	 /// </summary>
	 /// <param name="noiseProfileOptions"></param>
	 /// <returns></returns>
	public List<NoiseProfileOptions> GetActiveMaps(List<NoiseProfileOptions> noiseProfileOptions)
	{
		List<NoiseProfileOptions> tmp = new List<NoiseProfileOptions>();
		for (int i = 0; i < noiseProfileOptions.Count; i++)
			if (noiseProfileOptions[i].profileMode == NoiseClass.NoiseProfileMode.Map)
			{
				tmp.Add(noiseProfileOptions[i]);
			}
		return tmp;
	}

	/// <summary>
	/// Determines if a Value was changed on the Visual Inteperation end of the program
	/// </summary>
	/// <returns></returns>
	bool NonNoiseValuesChange()
	{
		return !VisualInterpertationSettings.Equals(oldVisualInterpertationSettings);
	}
	/// <summary>
	/// Changed the Visual Interpertation based on the current Visual Interpertation Settings value
	/// </summary>
	void ChangeVisualInterpertation()
	{
		switch (VisualInterpertationSettings.visualInterpertation)
		{
			case NoiseClass.VisualInterpertation.Texture:
				if (Dummy != null)
					Destroy(Dummy);
				break;
			case NoiseClass.VisualInterpertation.Shape3D:
				// change to a primative type shape
				Dummy = Dummy == null ? GameObject.CreatePrimitive(PrimitiveType.Cube) : Dummy;
				Dummy.name = "Dummy";
				break;
			case NoiseClass.VisualInterpertation.Terrain:
				// change into a terrain
				Dummy = Dummy == null ? GameObject.CreatePrimitive(PrimitiveType.Cube) : Dummy;
				Dummy.name = "Dummy";
				break;
		}
	}
	/// <summary>
	/// Updates the Visual Interpertation based on the retreived values
	/// </summary>
	void UpdateVisualInterpertation()
	{
		// gotta store the bounds for re-positioning.
		Bounds bounds = new Bounds();

		switch (VisualInterpertationSettings.visualInterpertation)
		{
			case NoiseClass.VisualInterpertation.Texture:
				// The texture is already updated
				break;
			case NoiseClass.VisualInterpertation.Shape3D:
				// The shape was already changed and the texture as well
				
				Dummy.transform.localScale = VisualInterpertationSettings.Scale;
				Dummy.GetComponent<MeshRenderer>().material = meshRenderer.material;
				GameObject tmp = GameObject.CreatePrimitive(VisualInterpertationSettings.shape);
				Dummy.GetComponent<MeshFilter>().mesh = tmp.GetComponent<MeshFilter>().mesh;
				Destroy(tmp);
				// offset the object behind the noise plane
				bounds = Dummy.GetComponent<MeshFilter>().mesh.bounds;
				// we want the object to be behind the camera so it doen't obstruct the textures
				Dummy.transform.position = new float3( 0,0,- bounds.size.z - Z_OFFSET);
				break;
			case NoiseClass.VisualInterpertation.Terrain:
				MeshRenderer mr = Dummy.GetComponent<MeshRenderer>();
				// here we go...
				Dummy.GetComponent<MeshFilter>().mesh = GenerateTerrain((Texture2D) meshRenderer.material.mainTexture, VisualInterpertationSettings.MaxHeight);
				Dummy.transform.localScale = VisualInterpertationSettings.Scale;
				// offset the object behind the noise plane
				bounds = Dummy.GetComponent<MeshFilter>().mesh.bounds;
				// we want the object to be behind the camera so it doen't obstruct the textures
				Dummy.transform.position = new float3(0, 0,- bounds.size.z - Z_OFFSET);
				if (VisualInterpertationSettings.UseMaterialOnTerrain)
				{
					//	if (textureOverride == -1)
					//		mr.material = meshRenderer.material;
					//	else
					//		mr.material.mainTexture = noiseProfileOptions[textureOverride].GetTexture();
					mr.material.mainTexture = Map;

					// reset these 
					mr.material.mainTextureScale = new Vector2(1 ,1);
					mr.material.mainTextureOffset = -0.5f * mr.material.mainTextureScale;
				}
				else if (VisualInterpertationSettings.UseColorHeightsOnTerrain)
				{
					//	if (textureOverride == -1)
					//		mr.material = meshRenderer.material;
					//	else
					//		mr.material.mainTexture = noiseProfileOptions[textureOverride].GetTexture();

					mr.material.mainTexture = Map;

					// stretch the texutre to match the heights on the terrain
					mr.material.mainTextureScale = new Vector2(1 / bounds.size.x, 1 / bounds.size.z);
					mr.material.mainTextureOffset = -0.5f * mr.material.mainTextureScale;
				}
				else
					Dummy.GetComponent<MeshRenderer>().material = DefaultMaterial;
				break;
		}
	}
	/// <summary>
	/// V1 of Terrain generation
	/// </summary>
	/// <param name="texture">texture to be used (this is converted to grayscale)</param>
	/// <param name="maxHeight">max height of terrain (represented by 1f in gray scale or 255 (white) in color)</param>
	/// <returns></returns>
	Mesh GenerateTerrain(Texture2D texture,float maxHeight)
	{
		// Credit for this function goes to cjdev  Aug 21, 2015 at 07:06 AM 
		// source: https://answers.unity.com/questions/1033085/heightmap-to-mesh.html

		List<Vector3> verts = new List<Vector3>();
		List<int> tris = new List<int>();
		int width = texture.width;
		int height = texture.height;
		for(int i = 0; i < width; i++)
		{
			for(int j = 0; j < height; j++)
			{
				// converts all textures to grayscale
				//Add each new vertex in the plane
				verts.Add(new Vector3(i,texture.GetPixel(i,j).grayscale*maxHeight,j ));
				//Skip if a new square on the plane hasn't been formed
				if (i == 0 || j == 0) continue;

				// to prevent tearing in triangles we will use only the width
				// See cjdev's example for more info

				tris.Add(width * i + j); //Top right
				tris.Add(width * i + j - 1); //Bottom right
				tris.Add(width * (i - 1) + j - 1); //Bottom left - First triangle
				tris.Add(width * (i - 1) + j - 1); //Bottom left 
				tris.Add(width * (i - 1) + j); //Top left
				tris.Add(width * i + j); //Top right - Second triangle
			}
		}
		Vector2[] uvs = new Vector2[verts.Count];
		for (var i = 0; i < uvs.Length; i++) //Give UV coords X,Z world coords
			uvs[i] = new Vector2(verts[i].x, verts[i].z);
	//	GameObject plane = new GameObject("ProcPlane"); //Create GO and add necessary components
	//	plane.AddComponent<MeshFilter>();
	//	plane.AddComponent<MeshRenderer>();
		Mesh mesh = new Mesh();
		mesh.vertices = verts.ToArray(); //Assign verts, uvs, and tris to the mesh
		mesh.uv = uvs;
		mesh.triangles = tris.ToArray();
		mesh.RecalculateNormals(); //Determines which way the triangles are facing
	//	plane.GetComponent<MeshFilter>().mesh = procMesh; //Assign Mesh object to MeshFilter
		return mesh;
	}
	/// <summary>
	/// creates a copy of the Noise Profile at the given index and adds it to the end of the list
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	public bool CopyNoiseProfile(int index)
	{
		if (index > -1 && index < noiseProfileOptions.Count)
		{
			NoiseProfileOptions tmpA = new NoiseProfileOptions();
			noiseProfileOptions[index].CopyTo(ref tmpA);
			NoiseProfileEditorAttributes tmpB = new NoiseProfileEditorAttributes();
			noiseProfileEditorAttributes[index].CopyTo(ref tmpB);
			AddNoiseProfile(tmpA,tmpB);
			return true;
		}
		return false;
	}
	/// <summary>
	/// Addes a new noise profile
	/// </summary>
	public void AddNoiseProfile()
	{
		AddNoiseProfile(new NoiseProfileOptions(), new NoiseProfileEditorAttributes());
	}
	/// <summary>
	/// Adds a Noise Profile
	/// </summary>
	/// <param name="noiseProfile"></param>
	public void AddNoiseProfile(NoiseProfileOptions noiseProfile)
	{
		AddNoiseProfile(noiseProfile, new NoiseProfileEditorAttributes());
	}
	/// <summary>
	/// Adds a Noise Profile
	/// </summary>
	/// <param name="noiseProfile"></param>
	/// <param name="editorAttributes"></param>
	public void AddNoiseProfile(NoiseProfileOptions noiseProfile,NoiseProfileEditorAttributes editorAttributes)
	{
		noiseProfileOptions.Add(noiseProfile);
		noiseProfileEditorAttributes.Add(editorAttributes);
		tabNames.Add("Noise Profile " +tabNames.Count);
	}
	/// <summary>
	/// Removes a noise profile
	/// </summary>
	/// <param name="noiseProfile"></param>
	/// <param name="noiseProfileEditor"></param>
	public void DeleteNoiseProfile(NoiseProfileOptions noiseProfile,NoiseProfileEditorAttributes noiseProfileEditor)
	{
		noiseProfileOptions.Remove(noiseProfile);
		noiseProfileEditorAttributes.Remove(noiseProfileEditor);
		tabNames.Remove(tabNames[tab]);
		tab = 0;
	}
	/// <summary>
	/// Resets the NoiseProfiles
	/// </summary>
	public void ResetNoiseProfiles()
	{
		noiseProfileEditorAttributes = new List<NoiseProfileEditorAttributes>();
		noiseProfileOptions = new List<NoiseProfileOptions>();
		tabNames = new List<string>();
		tab = 0;
	}
}

/// <summary>
/// These are the attributes that the Cutom GUI uses.
/// The reason its a class is because of initialization really.
/// </summary>
public class NoiseProfileEditorAttributes
{
	// Material Attributes Variables
	public bool MaterialAttributesFoldout = true;
	// Gradients and Rotations Variables
	public bool GradientsAndRotationValuesFoldout = true;

	// Crop Section
	public bool CropSectionFoldout = true;

	// Value Interpertation Variables
	public bool ValueInterpertation = true;

	public bool ValueInterpertation2DX = true;
	public bool ValueInterpertation2DY = true;


	public bool ValueInterpertation3DX = true;
	public bool ValueInterpertation3DY = true;
	public bool ValueInterpertation3DZ = true;

	public int ValueInterpertationDimension = 1;

	public List<bool> ValueInterpertationCustomColorFoldouts = new List<bool>();

	public void CopyTo(ref NoiseProfileEditorAttributes other)
	{
		other.MaterialAttributesFoldout = MaterialAttributesFoldout;
		other.GradientsAndRotationValuesFoldout = GradientsAndRotationValuesFoldout;
		other.ValueInterpertation = ValueInterpertation;
		other.ValueInterpertation2DX = ValueInterpertation2DX;
		other.ValueInterpertation2DY = ValueInterpertation2DY;
		other.ValueInterpertation3DX = ValueInterpertation3DX;
		other.ValueInterpertation3DY = ValueInterpertation3DY;
		other.ValueInterpertation3DZ = ValueInterpertation3DZ;
		other.ValueInterpertationDimension = ValueInterpertationDimension;
		other.ValueInterpertationCustomColorFoldouts = ValueInterpertationCustomColorFoldouts;
		other.CropSectionFoldout = CropSectionFoldout;
	}
}

/// <summary>
/// This is the Custom GUi for the Noise Copmonent
/// </summary>
[CustomEditor(typeof(NoiseComponent))]
public class NoiseEditor : Editor
{
	public NoiseComponent nc;

	private const int MAX_GRADIENT_KEYS = 8;

	// Save Noise Variables
	public string filenameSave = "";
	public bool SaveAsHeightMap = false;
	public bool SaveFoldout = true;
	// Load Noise variables
	public string filenameLoad = "";
	private bool LoadFoldout = true;

	// Crop variables
	private Vector2Int startPoint;
	private Vector2Int endPoint;

	// Visual Interpertation Variables
	private bool VisualInterpertationFolout = true;

	// Button Variables
	private const int BUTTON_WIDTH = 200;
	private const int BUTTON_HEIGHT = 20;


	// Shorthand GUI Functions
	private void Vert()
	{
		GUILayout.BeginVertical();
	}
	private void EVert()
	{
		GUILayout.EndVertical();
	}
	private void Hori()
	{
		GUILayout.BeginHorizontal();
	}
	private void EHori()
	{
		GUILayout.EndHorizontal();
	}

	private bool ValidateArithmetic(ref string eArithmetic,ref string[] arithmetic,ref List<NoiseProfileOptions> noiseProfileOptions,ref List<NoiseProfileEditorAttributes> noiseProfileEditorAttributes, bool MapMode = false)
	{
		if (eArithmetic.Length == 0)
			Debug.LogWarning("Arithmetic string is empty");
		else if (noiseProfileOptions.Count < 2)
			Debug.LogWarning("Not enough Noise Profiles to perform arithmetic operations");
		else
		{
			// now it's time to verify the string

			//first we split it
			string[] tmp = eArithmetic.Split(' ');

			if (tmp.Length != noiseProfileOptions.Count)
				Debug.LogWarning("Arithmetic string is not the same length as amount of Noise Profiles");
			else if (tmp.Length < 2)
				Debug.LogWarning("broken string is less than 2 make sure you using the right format and the number of symbols match the number fo Noise Profiles");
			else if (arithmetic.Length > noiseProfileEditorAttributes.Count)
				Debug.LogWarning("arithmetic doesn't match number of profiles!");
			else
			{
				// validate the symbols
				for (int i = 0; i < tmp.Length; i++)
					if (!IsValidSymbol(tmp[i]))
						tmp[i] = "i";
				// ignore the one that is used as the main texture (if that box is checked) and if disabled
				for (int i = 0; i < noiseProfileOptions.Count; i++)
						if(
							(noiseProfileOptions[i].profileMode == NoiseClass.NoiseProfileMode.Disabled)
							|| (MapMode && noiseProfileOptions[i].profileMode == NoiseClass.NoiseProfileMode.Texture)
							|| (!MapMode && noiseProfileOptions[i].profileMode == NoiseClass.NoiseProfileMode.Map)
						)
						tmp[i] = "i";
				// test for space/blank character
				if (tmp[tmp.Length - 1] == "")
					tmp[tmp.Length - 1] = "i";
				arithmetic = tmp;
				eArithmetic = System.String.Join(" ", tmp);
				return true;
			}
		}
		return false;
	}

	// GUI
	public override void OnInspectorGUI()
	{
		// Test if your in Play Mode 
		if (Application.isPlaying)
		{
			// First we get the noise component
			nc = target as NoiseComponent;

			// Visual Interpertation Block
			{
				Vert();
				EditorGUILayout.LabelField("Visual Interpertation", EditorStyles.boldLabel);
				GUILayout.Space(2);
				// get the Visual Interpertaion type from the drop down
				nc.VisualInterpertationSettings.visualInterpertation = (NoiseClass.VisualInterpertation)EditorGUILayout.EnumPopup("Visual Interpertation", nc.VisualInterpertationSettings.visualInterpertation);
				if (nc.VisualInterpertationSettings.visualInterpertation == NoiseClass.VisualInterpertation.Shape3D)
				{
					// Handle 3D Shape 
					VisualInterpertationFolout = EditorGUILayout.Foldout(VisualInterpertationFolout, "3D Shape Settings");
					if (VisualInterpertationFolout)
					{
						nc.VisualInterpertationSettings.Scale = EditorGUILayout.Vector3Field("Scale", nc.VisualInterpertationSettings.Scale);
						nc.VisualInterpertationSettings.shape = (PrimitiveType)EditorGUILayout.EnumPopup("Shape", nc.VisualInterpertationSettings.shape);
					}
				}
				else if (nc.VisualInterpertationSettings.visualInterpertation == NoiseClass.VisualInterpertation.Terrain)
				{
					// Handle Terrain
					VisualInterpertationFolout = EditorGUILayout.Foldout(VisualInterpertationFolout, "Terrain Settings");
					if (VisualInterpertationFolout)
					{
						nc.VisualInterpertationSettings.Scale = EditorGUILayout.Vector3Field("Scale", nc.VisualInterpertationSettings.Scale);
						nc.VisualInterpertationSettings.MaxHeight = EditorGUILayout.FloatField("Max height", nc.VisualInterpertationSettings.MaxHeight);
						nc.VisualInterpertationSettings.UseMaterialOnTerrain = EditorGUILayout.Toggle("Display Noise Material on Terrain", nc.VisualInterpertationSettings.UseMaterialOnTerrain);
						nc.VisualInterpertationSettings.UseColorHeightsOnTerrain = EditorGUILayout.Toggle("Use Color Heights on Terrain", nc.VisualInterpertationSettings.UseColorHeightsOnTerrain);
					}
				}
				// else do nothing because the Quad Visual is already setup
				EVert();
			}
			// We have to make sure the editor attributes match the number of profiles
			if (nc.noiseProfileEditorAttributes.Count != nc.noiseProfileOptions.Count)
			{
				// adding and removing will be handled by other functions so lets just raise an error here
				Debug.LogError("Editor and Profiles do not match in length, please check code and try again " + nc.noiseProfileEditorAttributes.Count + ":" + nc.noiseProfileOptions.Count);
			}
			EditorGUILayout.Space();
			// Noise Profile Tabs Buttons
			{
				Vert();
				GUILayout.Label("Noise Profile Tab Buttons", EditorStyles.boldLabel);
				Hori();
				EditorGUILayout.Space(1);
				if (nc.noiseProfileOptions.Count > 0)
					//Handle Noise Profile Copy
					if (GUILayout.Button("Copy Current Noise Profile Tab", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
						if (!nc.CopyNoiseProfile(nc.tab))
							Debug.LogWarning("Failed to copy Noise Profile "+nc.tab+", index out of bounds!");
				// Handle Noise Profile Deletion
				if (GUILayout.Button("Delete Current Noise Profile Tab", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
					nc.DeleteNoiseProfile(nc.noiseProfileOptions[nc.tab], nc.noiseProfileEditorAttributes[nc.tab]);
				// Handle Noise Profile Addition
				if (GUILayout.Button("Add New Noise Profile Tab", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
					nc.AddNoiseProfile();
				EHori();
				EVert();
			}
			EditorGUILayout.Space();
			// Noise Arithmetic
			{
				// If there is at least 2 Noise Profile than show arithmetic
				if (nc.noiseProfileOptions.Count > 1)
				{
					GUILayout.BeginVertical("box");
					GUILayout.Space(2);
					// Handle arithmetic
					EditorGUILayout.LabelField("Noise Arithmetic", EditorStyles.boldLabel);

					nc.eArithmetic = EditorGUILayout.TextField("Arithmetic Opertaion String", nc.eArithmetic);
					nc.performArithmetic = EditorGUILayout.Toggle("Perform Arithmetic", nc.performArithmetic);
					nc.eMapArithmetic = EditorGUILayout.TextField("Map Arithmetic Opertaion String", nc.eMapArithmetic);
					nc.performMapArithmetic = EditorGUILayout.Toggle("Perform Map Arithmetic", nc.performMapArithmetic);
					
					if (nc.performArithmetic)
					{
						
						if (!ValidateArithmetic(ref nc.eArithmetic,ref nc.arithmetic,ref nc.noiseProfileOptions,ref nc.noiseProfileEditorAttributes))
						{
							if (nc.arithmetic.Length > 0)
								nc.arithmetic = new string[0];
							nc.performArithmetic = false;
						}
					}
					if (nc.performMapArithmetic)
					{
						// now we handle the map arithmetic
						if (!ValidateArithmetic(ref nc.eMapArithmetic, ref nc.mapArithmetic, ref nc.noiseProfileOptions, ref nc.noiseProfileEditorAttributes,true))
						{
							if (nc.mapArithmetic.Length > 0)
								nc.mapArithmetic = new string[0];
							nc.performMapArithmetic = false;
						}

					}
					EVert();
				}
			}
			EditorGUILayout.Space();
			// Noise Profile Tabs
			{
				// Get and display the current tab
				nc.tab = GUILayout.Toolbar(nc.tab, nc.tabNames.ToArray());
				// Redundant checking - this will always be true
				if (nc.noiseProfileEditorAttributes.Count > 0)
				{
					// Handle Noise Profile Atributes

					nc.noiseProfileOptions[nc.tab].profileMode = (NoiseClass.NoiseProfileMode)EditorGUILayout.EnumPopup("Profile Mode", nc.noiseProfileOptions[nc.tab].profileMode);

					EditorGUILayout.Space();
					// used with arithmetic opertations
					nc.noiseProfileOptions[nc.tab].useAsMainTexture = EditorGUILayout.Toggle("Use As Arithmetic Starting Texture", nc.noiseProfileOptions[nc.tab].useAsMainTexture);
					// set all other noise profiles use as main texture to false
					EditorGUILayout.Space();

					if (nc.noiseProfileOptions[nc.tab].useAsMainTexture)
					{
						// Handle a Noise Profile being used as the main texture for arithmetic
						for (int i = 0; i < nc.noiseProfileOptions.Count; i++)
							if (i != nc.tab)
								nc.noiseProfileOptions[i].useAsMainTexture = false;
						// useAsMaintexture will only work if the profile is enabled which can't happen with useAsTerrainTexture
					//	nc.noiseProfileOptions[nc.tab].useAsTerrainTexture = false;
					}

					// terrain texture override
					{
					//	nc.noiseProfileOptions[nc.tab].useAsTerrainTexture = EditorGUILayout.Toggle("Use As Map", nc.noiseProfileOptions[nc.tab].useAsTerrainTexture);
						// First we have to verify all terrain texture ovverrides

						// find if there any overrides 
						for (int i = 0; i < nc.noiseProfileOptions.Count; i++)
						{
							if (nc.noiseProfileOptions[i].profileMode == NoiseClass.NoiseProfileMode.Map)
							{
								// can't have it be a texture and affecting the final profile
								nc.noiseProfileOptions[nc.tab].useAsMainTexture = false;
							}
						}
						// the rest of the logic for this is handled in arithmetic
					}

					// Noise Type
					EditorGUILayout.LabelField("Noise Type", EditorStyles.boldLabel);
					nc.noiseProfileOptions[nc.tab].NoiseType = (NoiseClass.NoiseType)EditorGUILayout.EnumPopup("Noise Type", nc.noiseProfileOptions[nc.tab].NoiseType);

					// determine the dimension of the return values of a particular Noise
					nc.noiseProfileOptions[nc.tab].Is2D = NoiseClass.TypeIs2D(nc.noiseProfileOptions[nc.tab].NoiseType);
					if (!nc.noiseProfileOptions[nc.tab].Is2D)
						nc.noiseProfileOptions[nc.tab].Is3D = NoiseClass.TypeIs3D(nc.noiseProfileOptions[nc.tab].NoiseType);
					else
						nc.noiseProfileOptions[nc.tab].Is3D = false;
					if (!nc.noiseProfileOptions[nc.tab].Is3D)
						nc.noiseProfileOptions[nc.tab].Is4D = NoiseClass.TypeIs4D(nc.noiseProfileOptions[nc.tab].NoiseType);
					else
						nc.noiseProfileOptions[nc.tab].Is4D = false;

					EditorGUILayout.Space();

					// Value Interpertation
					nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation = EditorGUILayout.Foldout(nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation, "Value Interpitation");
					if (nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation)
					{
						// Get the value of  UseCustomColors
						nc.noiseProfileOptions[nc.tab].ValueInterpertation.UseCutsomColors = EditorGUILayout.Toggle("Use Custom Colors", nc.noiseProfileOptions[nc.tab].ValueInterpertation.UseCutsomColors);
						if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.UseCutsomColors)
							nc.noiseProfileOptions[nc.tab].ValueInterpertation.useGradientValue = EditorGUILayout.Toggle("Use Gradient Value", nc.noiseProfileOptions[nc.tab].ValueInterpertation.useGradientValue);

						// Storing the value of the dimension
						nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertationDimension = nc.noiseProfileOptions[nc.tab].ValueInterpertation.ReturnDimension(nc.noiseProfileOptions[nc.tab].NoiseType);
						// Value Handling
						if (nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertationDimension == 1)
						{
							EditorGUILayout.LabelField("1D Noise Value Return Interpertation", EditorStyles.boldLabel);
							nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertation1D.x = EditorGUILayout.Toggle("R", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertation1D.x);
							nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertation1D.y = EditorGUILayout.Toggle("G", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertation1D.y);
							nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertation1D.z = EditorGUILayout.Toggle("B", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertation1D.z);
						}
						else if (nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertationDimension == 2)
						{
							EditorGUILayout.LabelField("2D Noise Value Return Interpertation", EditorStyles.boldLabel);
							// Handle the Noise Return 2D X options
							nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation2DX = EditorGUILayout.Foldout(nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation2DX, "Noise Return X Options");
							if (nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation2DX)
							{
								// Get the values
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.x = EditorGUILayout.Toggle("R", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.x);
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.y = EditorGUILayout.Toggle("G", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.y);
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.z = EditorGUILayout.Toggle("B", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.z);
								// verify there is no conflicts between A2D and B2D
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.x && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.x)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.x = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.y && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.y)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.y = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.z && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.z)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.z = false;
							}

							// Handle the Noise Return 2D Y options
							nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation2DY = EditorGUILayout.Foldout(nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation2DY, "Noise Return Y Options");
							if (nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation2DY)
							{
								// Get the values
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.x = EditorGUILayout.Toggle("R", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.x);
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.y = EditorGUILayout.Toggle("G", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.y);
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.z = EditorGUILayout.Toggle("B", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.z);

								// verify there is no conflicts between A2D and B2D
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.x && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.x)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.x = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.y && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.y)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.y = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.z && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB2D.z)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA2D.z = false;
							}
						}
						else if (nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertationDimension == 3)
						{
							EditorGUILayout.LabelField("3D Noise Value Return Interpertation", EditorStyles.boldLabel);
							// Handle the Noise Return 3D X options
							nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation3DX = EditorGUILayout.Foldout(nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation3DX, "Noise Return X Options");
							if (nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation3DX)
							{
								// Get the values
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.x = EditorGUILayout.Toggle("R", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.x);
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.y = EditorGUILayout.Toggle("G", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.y);
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.z = EditorGUILayout.Toggle("B", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.z);
								// verify there is no conflicts between A3D and B3D and C3D
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.x && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.x)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.x = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.y && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.y)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.y = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.z && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.z)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.z = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.x && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.x)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.x = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.y && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.y)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.y = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.z && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.z)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.z = false;
							}

							// Handle the Noise Return 3D Y options
							nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation3DY = EditorGUILayout.Foldout(nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation3DY, "Noise Return Y Options");
							if (nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation3DY)
							{
								// Get the values
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.x = EditorGUILayout.Toggle("R", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.x);
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.y = EditorGUILayout.Toggle("G", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.y);
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.z = EditorGUILayout.Toggle("B", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.z);
								// verify there is no conflicts between A3D and B3D and C3D
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.x && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.x)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.x = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.y && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.y)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.y = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.z && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.z)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.z = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.x && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.x)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.x = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.y && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.y)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.y = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.z && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.z)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.z = false;
							}

							// Handle the Noise Return 3D Z options
							nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation3DZ = EditorGUILayout.Foldout(nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation3DZ, "Noise Return Z Options");
							if (nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertation3DZ)
							{
								// Get the values
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.x = EditorGUILayout.Toggle("R", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.x);
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.y = EditorGUILayout.Toggle("G", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.y);
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.z = EditorGUILayout.Toggle("B", nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.z);
								// verify there is no conflicts between A3D and B3D and C3D
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.x && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.x)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.x = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.y && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.y)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.y = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.z && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.z)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationA3D.z = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.x && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.x)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.x = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.y && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.y)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.y = false;
								if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.z && nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationC3D.z)
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.ColorInterpertationB3D.z = false;
							}
						}
						else if (nc.noiseProfileOptions[nc.tab].NoiseType == NoiseClass.NoiseType.Mixed)
						{
							//ignore. proabbly loaded a texture of something
						}
						else Debug.LogError("Detected Unknown Dimension!");
						// Handle using Cutsom Color and not a Custom Gradient
						if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.UseCutsomColors && !nc.noiseProfileOptions[nc.tab].ValueInterpertation.useGradientValue)
						{
							// Handle Custom Color Options
							EditorGUILayout.BeginVertical("box");
							// Create the add button
							EditorGUILayout.BeginHorizontal();
							EditorGUILayout.Space();
							// Handle the onclick of "Add Color" Button
							if (GUILayout.Button("Add Color", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
							{
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.AddColor();
								nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertationCustomColorFoldouts.Add(true);
							}

							EditorGUILayout.EndHorizontal();

							// This is dynamic so we have to loop through all colors and use nc.tab and i to get the right Noise Profiles and indexes

							for (int i = 0; i < nc.noiseProfileOptions[nc.tab].ValueInterpertation.colors.Length; i++)
							{
								// Handle Foldout
								nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertationCustomColorFoldouts[i] = EditorGUILayout.Foldout(nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertationCustomColorFoldouts[i], "Custom Color " + i);
								if (nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertationCustomColorFoldouts[i])
								{

									EditorGUILayout.BeginHorizontal();
									EditorGUILayout.Space();
									// Handle "Remove Color" Button
									if (GUILayout.Button("Remove Color", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
									{
										nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertationCustomColorFoldouts.RemoveAt(i);
										nc.noiseProfileOptions[nc.tab].ValueInterpertation.RemoveColor(i);
										EditorGUILayout.EndHorizontal();
										break;
									}
									else
									{
										// Display Color and options
										EditorGUILayout.EndHorizontal();
										bool enable;
										float height;
										float3 color;
										if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.GetColorData(i, out height, out color, out enable))
										{
											enable = EditorGUILayout.Toggle("Enabled", enable);
											height = EditorGUILayout.FloatField("Starting Height", height);
											// Basic, this needs to be changed
											if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.colors[i].Height > 1)
												nc.noiseProfileOptions[nc.tab].ValueInterpertation.colors[i].Height = 1f;
											else if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.colors[i].Height < 0)
												nc.noiseProfileOptions[nc.tab].ValueInterpertation.colors[i].Height = 0;

											Color tmpColor = EditorGUILayout.ColorField("Color", new Color(color.x, color.y, color.z));
											color = new float3(tmpColor.r, tmpColor.g, tmpColor.b);

											nc.noiseProfileOptions[nc.tab].ValueInterpertation.ChangeColorData(i, height, color, enable);
										}
										else Debug.LogWarning("Failed to get custom color for index " + i);
									}

								}
							}

							EditorGUILayout.EndVertical();
						}
						// Handle using Cutom Color and Custom Gradient
						else if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.UseCutsomColors && nc.noiseProfileOptions[nc.tab].ValueInterpertation.useGradientValue)
						{
							if(nc.noiseProfileOptions[nc.tab].ValueInterpertation.colors.Length > 0)
							{
								while( nc.noiseProfileOptions[nc.tab].ValueInterpertation.colors.Length > 0)
								{
									nc.noiseProfileEditorAttributes[nc.tab].ValueInterpertationCustomColorFoldouts.RemoveAt(0);
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.RemoveColor(0);
								}
							}

							// Handle Gradient Options
							EditorGUILayout.BeginVertical("box");
							Gradient gradient = new Gradient();
							nc.noiseProfileOptions[nc.tab].ValueInterpertation.GradientEnabled = EditorGUILayout.Toggle("Enable Gradient", nc.noiseProfileOptions[nc.tab].ValueInterpertation.GradientEnabled);
							nc.noiseProfileOptions[nc.tab].ValueInterpertation.UpdateProfileOnEveryFrame = EditorGUILayout.Toggle("Update Every Frame", nc.noiseProfileOptions[nc.tab].ValueInterpertation.UpdateProfileOnEveryFrame);
							Hori();
							float originalValue = EditorGUIUtility.labelWidth;
							EditorGUIUtility.labelWidth = BUTTON_WIDTH / 2;
							nc.noiseProfileOptions[nc.tab].ValueInterpertation.NumberOfdecimalPlaces = EditorGUILayout.IntField("# of Decimals", nc.noiseProfileOptions[nc.tab].ValueInterpertation.NumberOfdecimalPlaces);
							if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.NumberOfdecimalPlaces < 1)
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.NumberOfdecimalPlaces = 1;
							nc.noiseProfileOptions[nc.tab].ValueInterpertation.desiredAmountOfColors = EditorGUILayout.IntField("Amount Of Colors", nc.noiseProfileOptions[nc.tab].ValueInterpertation.desiredAmountOfColors);
							if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.desiredAmountOfColors < 1)
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.desiredAmountOfColors = 1;
							EditorGUIUtility.labelWidth = originalValue;
							int AmountOfDetectedColors = 0;
							if (GUILayout.Button("Copy Generated Noise Profile",GUILayout.Width(BUTTON_WIDTH),GUILayout.Height(BUTTON_HEIGHT)))
							{
								AmountOfDetectedColors = nc.noiseProfileOptions[nc.tab].CopyGradientFromNoiseProfile(nc.noiseProfileOptions[nc.tab].ValueInterpertation.desiredAmountOfColors, nc.noiseProfileOptions[nc.tab].ValueInterpertation.NumberOfdecimalPlaces) ;
							}
							EHori();

							EditorGUILayout.BeginVertical("box");
							// Gradient Generation
							{
								// now we have to generate the gradients but let us create some variables first
								int gradientLength = (int)math.ceil((float)nc.noiseProfileOptions[nc.tab].ValueInterpertation.desiredAmountOfColors/ GradientDataClass.MAX_KEY_AMOUNT);
								int colorLength = (int)math.ceil((float)nc.noiseProfileOptions[nc.tab].ValueInterpertation.gradientData.ColorKeys.Length / GradientDataClass.MAX_KEY_AMOUNT);
								// fix for too little colors when you hit the copy button


								if (AmountOfDetectedColors > 0 && AmountOfDetectedColors < nc.noiseProfileOptions[nc.tab].ValueInterpertation.desiredAmountOfColors)
								{
									Debug.LogWarning("Forcing resize...Feel free to ignrore this");
									// Force a resize
						//			Debug.Log(AmountOfDetectedColors+","+gradientLength+","+colorLength);
									gradientLength = (int)math.ceil((float)AmountOfDetectedColors / GradientDataClass.MAX_KEY_AMOUNT);
									nc.noiseProfileOptions[nc.tab].ValueInterpertation.desiredAmountOfColors = AmountOfDetectedColors ;
								}
								Gradient[] gradients = new Gradient[gradientLength];
								
								
								// Blend Mode
								nc.noiseProfileOptions[nc.tab].ValueInterpertation.gradientData.GradientMode = (GradientMode)EditorGUILayout.EnumPopup("Gradient Mode", nc.noiseProfileOptions[nc.tab].ValueInterpertation.gradientData.GradientMode);
								// verify the size of the graidnet matchs the gradientData
								if(gradientLength > colorLength)
								{
									Debug.Log("Adding Gradient "+colorLength+","+gradientLength);
									// add more colors!
									for (int i = colorLength; i < gradientLength; i++)
										nc.noiseProfileOptions[nc.tab].ValueInterpertation.gradientData.AddGradient();
									gradientLength = (int)math.ceil((float)nc.noiseProfileOptions[nc.tab].ValueInterpertation.desiredAmountOfColors / GradientDataClass.MAX_KEY_AMOUNT);
									colorLength = (int)math.ceil((float)nc.noiseProfileOptions[nc.tab].ValueInterpertation.gradientData.ColorKeys.Length / GradientDataClass.MAX_KEY_AMOUNT);


								}
								else if(gradientLength < colorLength)
								{
									Debug.Log("Removing Gradient");
									// remove those colors!
									while (gradientLength < colorLength)
									{
										nc.noiseProfileOptions[nc.tab].ValueInterpertation.gradientData.RemoveGradient(nc.noiseProfileOptions[nc.tab].ValueInterpertation.gradientData.AlphaKeys.Length - 1);
										gradientLength = (int)math.ceil((float)nc.noiseProfileOptions[nc.tab].ValueInterpertation.desiredAmountOfColors / GradientDataClass.MAX_KEY_AMOUNT);
										colorLength = (int)math.ceil((float)nc.noiseProfileOptions[nc.tab].ValueInterpertation.gradientData.ColorKeys.Length / GradientDataClass.MAX_KEY_AMOUNT);
									}
								}

							//	Debug.Log("length ="+gradients.Length );

								// display the gradients
								for (int i = 0; i < gradients.Length; i++)
								{
									if (nc.noiseProfileOptions[nc.tab].ValueInterpertation.gradientData.GetGradient(i, out Gradient g))
									{
									//	float diff = nc.noiseProfileOptions[nc.tab].ValueInterpertation.gradientData.GetTimeDifference();
										gradients[i] = EditorGUILayout.GradientField((i* ((float)1/ gradients.Length)) +"-"+((i * ((float)1 / gradients.Length) +((float)1/ gradients.Length))), g);
										nc.noiseProfileOptions[nc.tab].ValueInterpertation.gradientData.SetGradient(i, gradients[i]);
									}
									else
										Debug.LogError("Failed to set the gradient for index " + i);
								}

								
							}
							EVert();
							EVert();
						}
					}

					// Material Attributes
					EditorGUILayout.Space();
					nc.noiseProfileEditorAttributes[nc.tab].MaterialAttributesFoldout = EditorGUILayout.Foldout(nc.noiseProfileEditorAttributes[nc.tab].MaterialAttributesFoldout, "Material Attributes");
					if (nc.noiseProfileEditorAttributes[nc.tab].MaterialAttributesFoldout)
					{
						// Material Attributes

						EditorGUILayout.LabelField("Scale");

						//		Scale
						nc.noiseProfileOptions[nc.tab].Scale = EditorGUILayout.FloatField("Scale", nc.noiseProfileOptions[nc.tab].Scale);
						nc.noiseProfileOptions[nc.tab].UseScaleAsMax = EditorGUILayout.Toggle("Use Scale As Size Limit", nc.noiseProfileOptions[nc.tab].UseScaleAsMax);
						EditorGUILayout.Space();
						EditorGUILayout.BeginVertical("box");
						nc.noiseProfileEditorAttributes[nc.tab].CropSectionFoldout = EditorGUILayout.Foldout(nc.noiseProfileEditorAttributes[nc.tab].CropSectionFoldout, "Crop Section");
						if (nc.noiseProfileEditorAttributes[nc.tab].CropSectionFoldout)
						{
							//		Crop
							int4 cropSection = nc.noiseProfileOptions[nc.tab].crop.GetOriginalCropSection();
							startPoint = new Vector2Int(cropSection.x, cropSection.y);
							endPoint = new Vector2Int(cropSection.z, cropSection.w);
							startPoint = EditorGUILayout.Vector2IntField("Start Point", startPoint);
							endPoint = EditorGUILayout.Vector2IntField("End Point", endPoint);
							int4 offsetMinMaxValues = nc.noiseProfileOptions[nc.tab].crop.CalculateOffsetBounds(nc.noiseProfileOptions[nc.tab].width, nc.noiseProfileOptions[nc.tab].height);
							nc.noiseProfileOptions[nc.tab].crop.offsetX = EditorGUILayout.IntSlider("Offset X", nc.noiseProfileOptions[nc.tab].crop.offsetX, offsetMinMaxValues.x, offsetMinMaxValues.y);
							nc.noiseProfileOptions[nc.tab].crop.offsetY = EditorGUILayout.IntSlider("Offset Y", nc.noiseProfileOptions[nc.tab].crop.offsetY, offsetMinMaxValues.z, offsetMinMaxValues.w);
							nc.noiseProfileOptions[nc.tab].crop.SetCropSection(new int4(startPoint.x, startPoint.y, endPoint.x, endPoint.y));
							nc.noiseProfileOptions[nc.tab].crop.useCroppedSection = EditorGUILayout.Toggle("Use Crop Section", nc.noiseProfileOptions[nc.tab].crop.useCroppedSection);
						
						}
						EVert();
						EditorGUILayout.Space();
						//Update MinMaxValue is UseScaleAsMax is true
						if (nc.noiseProfileOptions[nc.tab].UseScaleAsMax)
							nc.noiseProfileOptions[nc.tab].MinMaxValue = new float2(0, nc.noiseProfileOptions[nc.tab].Scale);
						else
							nc.noiseProfileOptions[nc.tab].MinMaxValue = EditorGUILayout.Vector2Field("MinMax Value", nc.noiseProfileOptions[nc.tab].MinMaxValue);

						//		Width
						nc.noiseProfileOptions[nc.tab].MinMaxWidth = Vector2IntToInt2(EditorGUILayout.Vector2IntField("Min Max Width", Int2ToVector2Int(nc.noiseProfileOptions[nc.tab].MinMaxWidth)));
						if (nc.noiseProfileOptions[nc.tab].MinMaxWidth.x > nc.noiseProfileOptions[nc.tab].MinMaxWidth.y)
							nc.noiseProfileOptions[nc.tab].MinMaxWidth.x = nc.noiseProfileOptions[nc.tab].MinMaxWidth.y - 1;
						//		Height
						nc.noiseProfileOptions[nc.tab].MinMaxHeight = Vector2IntToInt2(EditorGUILayout.Vector2IntField("Min Max Height", Int2ToVector2Int(nc.noiseProfileOptions[nc.tab].MinMaxHeight)));
						if (nc.noiseProfileOptions[nc.tab].MinMaxHeight.x > nc.noiseProfileOptions[nc.tab].MinMaxHeight.y)
							nc.noiseProfileOptions[nc.tab].MinMaxHeight.x = nc.noiseProfileOptions[nc.tab].MinMaxHeight.y - 1;

						//		Length
						if (nc.noiseProfileOptions[nc.tab].Is3D || nc.noiseProfileOptions[nc.tab].Is4D)
						{
							nc.noiseProfileOptions[nc.tab].MinMaxLength = Vector2IntToInt2(EditorGUILayout.Vector2IntField("Min Max Length", Int2ToVector2Int(nc.noiseProfileOptions[nc.tab].MinMaxLength)));
							if (nc.noiseProfileOptions[nc.tab].MinMaxLength.x > nc.noiseProfileOptions[nc.tab].MinMaxLength.y)
								nc.noiseProfileOptions[nc.tab].MinMaxLength.x = nc.noiseProfileOptions[nc.tab].MinMaxLength.y - 1;
						}
						//		Depth
						if (nc.noiseProfileOptions[nc.tab].Is4D)
						{
							nc.noiseProfileOptions[nc.tab].MinMaxDepth = Vector2IntToInt2(EditorGUILayout.Vector2IntField("Min Max Depth", Int2ToVector2Int(nc.noiseProfileOptions[nc.tab].MinMaxDepth)));
							if (nc.noiseProfileOptions[nc.tab].MinMaxDepth.x > nc.noiseProfileOptions[nc.tab].MinMaxDepth.y)
								nc.noiseProfileOptions[nc.tab].MinMaxDepth.x = nc.noiseProfileOptions[nc.tab].MinMaxDepth.y - 1;
						}
						// use the normal max width and height
						nc.noiseProfileOptions[nc.tab].width = EditorGUILayout.IntSlider("Width", nc.noiseProfileOptions[nc.tab].width, nc.noiseProfileOptions[nc.tab].MinMaxWidth.x, nc.noiseProfileOptions[nc.tab].MinMaxWidth.y);
						if (nc.noiseProfileOptions[nc.tab].width < 1)
							nc.noiseProfileOptions[nc.tab].width = 1;
						nc.noiseProfileOptions[nc.tab].height = EditorGUILayout.IntSlider("Height", nc.noiseProfileOptions[nc.tab].height, nc.noiseProfileOptions[nc.tab].MinMaxHeight.x, nc.noiseProfileOptions[nc.tab].MinMaxHeight.y);
						if (nc.noiseProfileOptions[nc.tab].height < 1)
							nc.noiseProfileOptions[nc.tab].height = 1;

						// handle the length and depth
						if (nc.noiseProfileOptions[nc.tab].Is3D || nc.noiseProfileOptions[nc.tab].Is4D)
							nc.noiseProfileOptions[nc.tab].length = EditorGUILayout.IntSlider("Length", nc.noiseProfileOptions[nc.tab].length, nc.noiseProfileOptions[nc.tab].MinMaxLength.x, nc.noiseProfileOptions[nc.tab].MinMaxLength.y);
						if (nc.noiseProfileOptions[nc.tab].Is4D)
							nc.noiseProfileOptions[nc.tab].depth = EditorGUILayout.IntSlider("Depth", nc.noiseProfileOptions[nc.tab].depth, nc.noiseProfileOptions[nc.tab].MinMaxDepth.x, nc.noiseProfileOptions[nc.tab].MinMaxDepth.y);

					}

					EditorGUILayout.Space();

					EditorGUILayout.BeginVertical("box");
					// Gradients and Rotation foldout
					nc.noiseProfileEditorAttributes[nc.tab].GradientsAndRotationValuesFoldout = EditorGUILayout.Foldout(nc.noiseProfileEditorAttributes[nc.tab].GradientsAndRotationValuesFoldout, "Gradients and Rotations");
					if (nc.noiseProfileEditorAttributes[nc.tab].GradientsAndRotationValuesFoldout)
					{


						//	GradientsAndRotationValuesFoldout = EditorGUILayout.Foldout(GradientsAndRotationValuesFoldout, "Fradients and Rotation");

						switch (nc.noiseProfileOptions[nc.tab].NoiseType)
						{
							case NoiseClass.NoiseType.PerlinNoise:
								DisplaySliders(new string[] { "Explicit Period A", "Explicit Peroid B" }, nc.tab);
								break;

							case NoiseClass.NoiseType.SRDNoise2D:
							case NoiseClass.NoiseType.SRNoise2D:

								DisplaySliders(new string[] { "Gradient Rotation" }, nc.tab);
								break;
							// these are currently not programmed
							case NoiseClass.NoiseType.PerlinNoise3x3x3:
								DisplaySliders(new string[] { "Period X", "Period Y", "Period Z" }, nc.tab);
								break;
							case NoiseClass.NoiseType.PerlinNoise4x4x4x4:

								DisplaySliders(new string[] { "Period X", "Period Y", "Period Z", "Period W" }, nc.tab);
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
								DisplaySliders(new string[] { }, nc.tab);
								break;
							default:
								break;
						}
					}

					nc.meshRenderer2.transform.position = new float3(10, 7.2f, 0);
					nc.meshRenderer2.transform.localScale = new float3(8, 8, 8);
					nc.meshRenderer2.material.mainTexture = nc.noiseProfileOptions[nc.tab].GetTexture();

					EVert();

					EditorGUILayout.Space();
				}
				// else ignore
			}
			EditorGUILayout.Space();

			// Noise Save foldout
			SaveFoldout = EditorGUILayout.Foldout(SaveFoldout, "Save Noise Profile(s)");
			if (SaveFoldout)
			{
				filenameSave = EditorGUILayout.TextField("filename", filenameSave);
				if (GUILayout.Button("Export Noise Profile", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
				{
					int numberOfActiveNoiseProfiles = NoiseClass.NumberOfEnabledNoiseProfiles(nc.noiseProfileOptions);
					NoiseClass.NoiseType type = numberOfActiveNoiseProfiles == 1 ? nc.noiseProfileOptions[nc.tab].NoiseType : NoiseClass.NoiseType.Mixed;
					// add the arithmetic string to the first profile
					nc.noiseProfileOptions[0].arithmetic_save = nc.arithmetic;
					nc.noiseProfileOptions[0].map_arithmetic_save = nc.mapArithmetic;
					// save the profiles
					if (filenameSave == "")
						filenameSave = "_blank";
					Core.Procedural.NoiseSaveDataClass.ExportNoiseProfiles(
						type,
						filenameSave,
						nc.noiseProfileOptions,
						(Texture2D)nc.meshRenderer.material.mainTexture);
					// Save the Map as a png
					Core.Procedural.NoiseSaveDataClass.SavePng(nc.Map,Core.Procedural.NoiseSaveDataClass.path+NoiseClass.Type2String(type)+"/"+filenameSave+"/"+filenameSave+"_Map.png");
					Core.Procedural.NoiseSaveDataClass.SaveHeightMap(nc.noiseProfileOptions[nc.tab].texture_data, Core.Procedural.NoiseSaveDataClass.path + NoiseClass.Type2String(type) + "/" + filenameSave + "/" + filenameSave + ".map");
				}

			}

			EditorGUILayout.Space();
			// Noise Load foldout
			LoadFoldout = EditorGUILayout.Foldout(LoadFoldout, "Load Noise Profile(s)");
			if (LoadFoldout)
			{
				filenameLoad = EditorGUILayout.TextField("file path", filenameLoad);
				if (GUILayout.Button("Load Noise Profile", GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
				{
					if (filenameLoad == "")
						filenameLoad = "_blank";
					nc.ResetNoiseProfiles();
					if (Core.Procedural.NoiseSaveDataClass.LoadNoiseProfiles(filenameLoad, out List<NoiseProfileOptions> options, out Texture2D tmp))
					{
						// update the noise profile
						for (int i = 0; i < options.Count; i++)
							nc.AddNoiseProfile(options[i]);

						nc.meshRenderer.material.mainTexture = tmp;
						if (nc.noiseProfileOptions[0].arithmetic_save.Length > 0)
						{
							nc.arithmetic = nc.noiseProfileOptions[0].arithmetic_save;
							nc.eArithmetic = System.String.Join(" ", nc.arithmetic);
							nc.performArithmetic = true;
						}
						if(nc.noiseProfileOptions[0].map_arithmetic_save.Length > 0)
						{
							nc.mapArithmetic = nc.noiseProfileOptions[0].map_arithmetic_save;
							nc.eMapArithmetic = System.String.Join(" ", nc.mapArithmetic);
							nc.performMapArithmetic = true;
						}
						nc.NoiseValuesChange = true;
						Debug.Log("Successfully Loaded Noise profiles");
					}
				}
			}
		}
		else
		{
			EditorGUILayout.LabelField("Please Enter Play Mode To Begin");
		}
	}
	/// <summary>
	/// Converts a Vector2 into an int2
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	private int2 Vector2IntToInt2(Vector2Int value)
	{
		return new int2(value.x,value.y);
	}
	/// <summary>
	/// Converts an int2 into a Vector2
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	private Vector2Int Int2ToVector2Int(int2 value) {
		return new Vector2Int(value.x, value.y);
	}
	
	/// <summary>
	/// This is responsible for displaying the sliders on certain noises
	/// </summary>
	/// <param name="valueStrings">an array of what the sliders text will be</param>
	/// <param name="numOfValueSliders">determines the numbers of slider that will be shown</param>
	private void DisplaySliders(string[] valueStrings,int tab)
	{
		if (valueStrings.Length > 0)
			nc.noiseProfileOptions[tab].ValueA = EditorGUILayout.Slider(valueStrings[0], nc.noiseProfileOptions[tab].ValueA, nc.noiseProfileOptions[tab].MinMaxValue.x, nc.noiseProfileOptions[tab].MinMaxValue.y);
		if (valueStrings.Length > 1)
			nc.noiseProfileOptions[tab].ValueB = EditorGUILayout.Slider(valueStrings[1], nc.noiseProfileOptions[tab].ValueB, nc.noiseProfileOptions[tab].MinMaxValue.x, nc.noiseProfileOptions[tab].MinMaxValue.y);
		if (valueStrings.Length > 2)
			nc.noiseProfileOptions[tab].ValueC = EditorGUILayout.Slider(valueStrings[2], nc.noiseProfileOptions[tab].ValueC, nc.noiseProfileOptions[tab].MinMaxValue.x, nc.noiseProfileOptions[tab].MinMaxValue.y);
		if (valueStrings.Length > 3)
			nc.noiseProfileOptions[tab].ValueD = EditorGUILayout.Slider(valueStrings[3], nc.noiseProfileOptions[tab].ValueD, nc.noiseProfileOptions[tab].MinMaxValue.x, nc.noiseProfileOptions[tab].MinMaxValue.y);
	}
	/// <summary>
	/// Determines if a string value is a valid Symbol used in Arithmetic.
	/// Valid values are "+" "-" "/" "*" "avg"
	/// </summary>
	/// <param name="test"></param>
	/// <returns></returns>
	private bool IsValidSymbol(string test)
	{
		string[] valid = { "+", "-", "*", "/", "avg","" };
		foreach(string value in valid)
			if (value == test)
				return true;
		return false;
	}
}

/// <summary>
/// This class hold functions used in one or more classes
/// </summary>
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

	// These are gradients retrived from the pdf on the page
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
	/// <summary>
	/// Noise Profile Mode
	/// </summary>
	public enum NoiseProfileMode {
		Texture,
		Map,
		Disabled
	}
	/// <summary>
	/// Visual interpertation of the Noise Profile
	/// </summary>
	public enum VisualInterpertation
	{
		// A 2D Texture 
		Texture,
		// A 3D Shape
		Shape3D,
		// A 3D Terrain
		Terrain
	}
	/// <summary>
	/// Type of Noise
	/// </summary>
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
		SimplexNoise3x3x3Gradient,

		// Used when noise is merged with other using arithmetic
		Mixed

	}
	/// <summary>
	/// Returns the value of a NoiseType as a String
	/// </summary>
	/// <param name="type"></param>
	/// <returns></returns>
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
			case NoiseType.SRDNoise:
				return "SRDNoise";
			case NoiseType.SRNoise2D:
				return "SRNoise2D";
			case NoiseType.SimplexNoise3x3x3Gradient:
				return "SimplexNoise3x3x3Gradient";
			case NoiseType.Mixed:
				return "Mixed";
			default:
				return "UNKNOWN_TYPE";
		}
	}

	/// <summary>
	/// returns a copy of the given Texture2D
	/// </summary>
	/// <param name="source">source Texture2D</param>
	/// <returns></returns>
	public static Texture2D CopyTexture2D(Texture2D source)
	{
		Texture2D texture = new Texture2D(source.width, source.height);
		for(int i = 0; i < source.width; i++)
			for(int j = 0; j < source.height;j++)
				texture.SetPixel(i, j, source.GetPixel(i, j));
		texture.Apply();
		return texture;
	}
	/// <summary>
	/// Creates a texture with the given color
	/// </summary>
	/// <param name="width"></param>
	/// <param name="height"></param>
	/// <param name="color"></param>
	/// <returns></returns>
	public static Texture2D GenerateOneColorTexture2D(int width,int height,Color color)
	{
		Texture2D texture = new Texture2D(width, height);
		for (int i = 0; i < width; i++)
			for (int j = 0; j < height; j++)
				texture.SetPixel(i, j, color);
		texture.Apply();
		return texture;
	}

	/// <summary>
	/// returns the index of the noise profile that has UseAsMainTexture set to true.
	/// if none are found then it returns -1.
	/// </summary>
	/// <returns></returns>
	public static int GetIndexOfNoiseProfileWithUseAsMainTexture(List<NoiseProfileOptions> noiseProfileOptions)
	{
		for (int i = 0; i < noiseProfileOptions.Count; i++)
			if (noiseProfileOptions[i].useAsMainTexture)
				return i;
		return -1;
	}
	/// <summary>
	/// returns the number of enabled noise profiles
	/// </summary>
	/// <returns></returns>
	public static int NumberOfEnabledNoiseProfiles(List<NoiseProfileOptions> noiseProfileOptions)
	{
		int a = 0;
		for (int i = 0; i < noiseProfileOptions.Count; i++)
			if (noiseProfileOptions[i].profileMode == NoiseProfileMode.Texture || noiseProfileOptions[i].profileMode == NoiseProfileMode.Map)
				a++;
		return a;
	}
	/// <summary>
	/// Converts a float3[][] into a Texture2D
	/// </summary>
	/// <param name="input"></param>
	/// <returns></returns>
	public static Texture2D float3_2DToTexture2D(float3[][] input)
	{
		Texture2D texture = new Texture2D(input.Length, input[0].Length);
		for (int i = 0; i < input.Length; i++)
		{
		//	Debug.Log("\"" + input[i][0].x + "," + input[i][0].y + "," + input[i][0].z + "\"");
			for (int j = 0; j < input[i].Length; j++)
			{
				texture.SetPixel(i, j, new Color(input[i][j].x, input[i][j].y, input[i][j].z));
			}
		}
		texture.Apply();
		return texture;
	}
	/// <summary>
	/// Converts a Texture2D into a float3[][]
	/// </summary>
	/// <param name="texture"></param>
	/// <returns></returns>
	public static float3[][] Texture2D_ToFloat3_2D(Texture2D texture)
	{
		float3[][] data = new float3[texture.width][];
		for (int i = 0; i < data.Length; i++)
		{
			data[i] = new float3[texture.height];
			for (int j = 0; j < data[i].Length; j++)
			{
				Color color = texture.GetPixel(i, j);
		//		Debug.Log("Color: " + color.ToString());
				data[i][j] = new float3(color.r, color.g, color.b);
			}
		}
		return data;
	}

	// These were kept to help make the GenerateTexture function and were obselete before they were completely finished
	// but feel free to look at the simpler version of getting noise function values.

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
	public static Texture2D GenerateTexture(NoiseType type,int4 minDimensions, int4 dimensions,CropSection crop,ValueInterpertation valueInterpertation, float Scale, float4 input,int dimension = 2)
	{
		Texture2D texture = new Texture2D(dimensions.x, dimensions.y);
		float x, y;
		Color color = new Color();
		// get the crop section
		int4 cropSection = crop.GetCropSection();
		CustomColorAttributes cca = new CustomColorAttributes
		{
			Enabled = new bool2(valueInterpertation.UseCutsomColors, valueInterpertation.useGradientValue && valueInterpertation.GradientEnabled),
			Gradient = valueInterpertation.gradientData
		};
		for (int i = minDimensions.x; i < dimensions.x; i++)
		{
			x = (float)i / dimensions.x * Scale;
			for (int j = minDimensions.y; j < dimensions.y; j++)
			{
				y = (float)j / dimensions.y * Scale;
				cca.CustomColor = valueInterpertation.colors;
				if (crop.useCroppedSection)
				{
					if (i < cropSection.x || i > cropSection.y || j < cropSection.z || j > cropSection.w)
						color = new Color(0, 0, 0);
					else
					{
						color = GenerateColor(type, minDimensions, dimensions, valueInterpertation, Scale, input, x, y,
							cca
							, dimension);
					}
				}
				else
					color = GenerateColor(type, minDimensions, dimensions, valueInterpertation, Scale, input, x, y, cca, dimension);
				texture.SetPixel(i, j, color);
			}
		}

		texture.Apply();

		return texture;
	}

	/// <summary>
	/// Handles the custom colors of heights
	/// </summary>
	/// <param name="currentColor">current color your algorithm determined</param>
	/// <param name="customColor"></param>
	/// <returns></returns>
	private static Color HandleCustomColor(Color currentColor,CustomColorAttributes customColor)
	{
		currentColor = new Color(
			Mathf.Lerp(0, 1, Mathf.InverseLerp(-1, 1, currentColor.r)),
			Mathf.Lerp(0, 1, Mathf.InverseLerp(-1, 1, currentColor.g)),
			Mathf.Lerp(0, 1, Mathf.InverseLerp(-1, 1, currentColor.b))
			);
		if (customColor.Enabled.x && !customColor.Enabled.y)
		{
			float lowestHeight = 1f;
			Color tmpColor = currentColor;
			for (int i = 0; i < customColor.CustomColor.Length; i++)
			{
				// check if color is enabled
				if (customColor.CustomColor[i].Enable)
				{
					// now we need to see if the values match the height restriction
					if (currentColor.grayscale <= customColor.CustomColor[i].Height)
					{
						if (customColor.CustomColor[i].Height <= lowestHeight)
						{
							tmpColor = new Color(customColor.CustomColor[i].Color.x, customColor.CustomColor[i].Color.y, customColor.CustomColor[i].Color.z);
							lowestHeight = customColor.CustomColor[i].Height;
						}
					}
				}
			}
			return tmpColor;
		}
		else if (customColor.Enabled.x && customColor.Enabled.y){
			float height = currentColor.grayscale;
			Gradient gradient = customColor.Gradient.GetGradient(height,out float newHeight);
			return gradient.Evaluate(newHeight);
		}
		return currentColor;
	}
	/// <summary>
	/// Generates a Color based on the given attributes. Please look at GenerateTexure for a better understanding on this works
	/// </summary>
	/// <param name="type"></param>
	/// <param name="minDimensions"></param>
	/// <param name="dimensions"></param>
	/// <param name="valueInterpertation"></param>
	/// <param name="Scale"></param>
	/// <param name="input"></param>
	/// <param name="x"></param>
	/// <param name="y"></param>
	/// <param name="customColor"></param>
	/// <param name="dimension"></param>
	/// <returns></returns>
	private static Color GenerateColor(NoiseType type, int4 minDimensions, int4 dimensions, ValueInterpertation valueInterpertation, float Scale, float4 input, float x,float y,CustomColorAttributes customColor, int dimension = 2)
	{
		float z, w, value;
		float2 value2;
		float3 value3;
		Color color = new Color();

		if (dimension == 2)
		{
			switch (type)
			{
				case NoiseClass.NoiseType.PerlinNoise:
					{
						// Classic Perlin noise, periodic variant
						value = noise.pnoise(new float2(x, y), new float2(input.x, input.y));

						color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
						color = HandleCustomColor(color, customColor);
						break;
					}

				case NoiseClass.NoiseType.SRNoise2D:
					{
						// 2-D non-tiling simplex noise with rotating gradients,
						// without the analytical derivative.
						value = noise.srnoise(new float2(x, y), input.x);
						color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
						color = HandleCustomColor(color, customColor);
						break;
					}
				case NoiseClass.NoiseType.SRNoise:
					{
						value = noise.srnoise(new float2(x, y));
						color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
						color = HandleCustomColor(color, customColor);
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
						color = HandleCustomColor(color, customColor);


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
						color = HandleCustomColor(color, customColor);

						break;
					}
				case NoiseClass.NoiseType.ClassicPerlinNoise:
					{
						value = noise.cnoise(new float2(x, y));
						color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
						color = HandleCustomColor(color, customColor);
						break;
					}
				case NoiseClass.NoiseType.SimplexNoise:
					{
						value = noise.snoise(new float2(x, y));
						color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
						color = HandleCustomColor(color, customColor);
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
						color = HandleCustomColor(color, customColor);
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
						color = HandleCustomColor(color, customColor);
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
							color = HandleCustomColor(color, customColor);

							break;
						}
					case NoiseClass.NoiseType.ClassicPerlinNoise3x3x3:
						{
							// Classic Perlin noise
							value = noise.cnoise(new float3(x, y, z));

							color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
							color = HandleCustomColor(color, customColor);
							break;
						}
					case NoiseClass.NoiseType.SimplexNoise3x3x3:
						{
							value = noise.snoise(new float3(x, y, z));

							color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
							color = HandleCustomColor(color, customColor);
							break;
						}
					case NoiseType.SimplexNoise3x3x3Gradient:
						{
							// idk what to do with the gradient
							float3 gradient = new float3();
							value = noise.snoise(new float3(x, y, z), out gradient);
							color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
							color = HandleCustomColor(color, customColor);
							break;
						}
					case NoiseClass.NoiseType.CellularNoise2x2x2:
						{
							// Cellular noise, returning F1 and F2 in a float2.
							// Speeded up by umath.sing 2x2x2 search window instead of 3x3x3,
							// at the expense of some pattern artifacts.
							// F2 is often wrong and has sharp discontinuities.
							// If you need a good F2, use the slower 3x3x3 version.
							value2 = noise.cellular2x2x2(new float3(x, y, z));


							//	color = new Color(value2.x, value2.x, value2.x);
							color = new Color(
								valueInterpertation.ColorInterpertationA2D.x ? value2.x : valueInterpertation.ColorInterpertationB2D.x ? value2.y : 0,
								valueInterpertation.ColorInterpertationA2D.y ? value2.x : valueInterpertation.ColorInterpertationB2D.y ? value2.y : 0,
								valueInterpertation.ColorInterpertationA2D.z ? value2.x : valueInterpertation.ColorInterpertationB2D.z ? value2.y : 0
							);
							color = HandleCustomColor(color, customColor);
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
							value2 = noise.cellular(new float3(x, y, z));


							//	color = new Color(value2.x, value2.x, value2.x);
							color = new Color(
								valueInterpertation.ColorInterpertationA2D.x ? value2.x : valueInterpertation.ColorInterpertationB2D.x ? value2.y : 0,
								valueInterpertation.ColorInterpertationA2D.y ? value2.x : valueInterpertation.ColorInterpertationB2D.y ? value2.y : 0,
								valueInterpertation.ColorInterpertationA2D.z ? value2.x : valueInterpertation.ColorInterpertationB2D.z ? value2.y : 0
							);
							color = HandleCustomColor(color, customColor);
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
							value = noise.pnoise(new float4(x, y, z, w), input);
							color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
							color = HandleCustomColor(color, customColor);
							break;
						case NoiseClass.NoiseType.ClassicPerlinNoise4x4x4x4:
							//		Debug.LogWarning("This Scene doesnto currently support 4D inputs because idk what they are");
							value = noise.cnoise(new float4(x, y, z, w));
							color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
							color = HandleCustomColor(color, customColor);
							break;
						case NoiseClass.NoiseType.SimplexNoise4x4x4x4:
							{
								//			Debug.LogWarning("This Scene doesnto currently support 4D inputs because idk what they are");
								value = noise.snoise(new float4(x, y, z, w));
								color = new Color(valueInterpertation.ColorInterpertation1D.x ? value : 0, valueInterpertation.ColorInterpertation1D.y ? value : 0, valueInterpertation.ColorInterpertation1D.z ? value : 0);
								color = HandleCustomColor(color, customColor);
								break;
							}
					}
				}
			}
		}
		else
			Debug.LogError("Unknown Dimension " + dimension + " detected!");
		return color;
	}

}

/// <summary>
/// These holds the values used to handl custom colors
/// </summary>
[System.Serializable]
public struct CustomColorAttributes
{
	public GradientData Gradient;
	public bool2 Enabled;
	public CustomColor[] CustomColor;
}
/// <summary>
/// This is a Noise Profile
/// </summary>
[System.Serializable]
public class NoiseProfileOptions
{
	public NoiseClass.NoiseProfileMode profileMode;

	public NoiseClass.NoiseType NoiseType;
	// these hold the value of extra inputs some noise function needs
	public float ValueA, ValueB, ValueC, ValueD;

	public int width, height, length, depth;
	public ValueInterpertation ValueInterpertation = new ValueInterpertation(false);

	// anything with old infront of it is used for update handling

	public NoiseClass.NoiseType OldNoiseType;
	public float4 oldValues, oldSize;
	public CropSection oldCrop;
	public float oldScale;
	private ValueInterpertation oldValueInterpertation = new ValueInterpertation(false);
	public bool oldInvertColor, oldUseAsMainTexture;
	public NoiseClass.NoiseProfileMode oldMode;

	public bool Is2D, Is3D, Is4D, invertColor, useAsMainTexture;

	// scale
	public float Scale;
	public bool UseScaleAsMax;

	// Values that hold Gradients and Rotation Values
	public float2 MinMaxValue;
	// Holds the Min and Max Demensions of the texture or Interpertation Method

	public int2 MinMaxWidth;
	public int2 MinMaxHeight;
	// This is used in 3D noise
	public int2 MinMaxLength;
	// This is used for 4D noise. Depth is not its true name, its just a placeholder name
	public int2 MinMaxDepth;
	// Crop Section
	public CropSection crop;

	public string[] arithmetic_save;
	public string[] map_arithmetic_save;

	public float3[][] texture_data;
	/// <summary>
	/// returns the "texture" of the Noise Profile
	/// </summary>
	/// <returns></returns>
	public Texture2D GetTexture()
	{
		return NoiseClass.float3_2DToTexture2D(texture_data);
	}
	/// <summary>
	/// sets the "texture" of the Noise Profile
	/// </summary>
	/// <param name="texture"></param>
	public void SetTexture(Texture2D texture)
	{
		texture_data = NoiseClass.Texture2D_ToFloat3_2D(texture);
	}
	/// <summary>
	/// Converts a texture into a grdient based on the grayscale height values
	/// </summary>
	/// <returns></returns>
	public int CopyGradientFromNoiseProfile(int ColorArrayMaxSize = 8, int numberOfDecimalPlaces = 1)
	{
		
		CGradientColorKey[] ColorKeys = new CGradientColorKey[0]; 

		ColorPopularity[] cPop = new ColorPopularity[0];
		// go through the data and get all possible color keys
		for (int i = 0; i < texture_data.Length; i++)
		{
			for (int j = 0; j < texture_data[i].Length; j++)
			{
				Color color = new Color(texture_data[i][j].x, texture_data[i][j].y, texture_data[i][j].z);
				// get height
				float height = (float)System.Math.Round(color.grayscale, numberOfDecimalPlaces);
				// used to prevent duplicates
				bool match = false;

				// go through all ready existing colors to test for duplicates
				for (int k = 0; k < ColorKeys.Length; k++)
				{
					if (ColorKeys[k].time == height)
					{
						match = true;
						break;
					}
				}
				// if no duplicate is found then add a new color
				if ((!match || ColorKeys.Length == 0)) {
					// add new gradient color
					CGradientColorKey[] tmpData = ColorKeys;
					ColorKeys = new CGradientColorKey[ColorKeys.Length + 1];
					if(ColorKeys.Length > 0)
						System.Array.Copy(tmpData, ColorKeys, tmpData.Length);
					ColorKeys[ColorKeys.Length - 1] = new CGradientColorKey
					{
						color = texture_data[i][j],
						time = height
					};
					// Color Popularity
					{
						match = false;
						for (int l = 0; l < cPop.Length; l++)
						{
							if (cPop[l].key.time == height)
							{
								cPop[l].amount++;
								match = true;
							}
						}
						if (!match || cPop.Length == 0)
						{
							ColorPopularity[] tmp = cPop;
							cPop = new ColorPopularity[tmp.Length + 1];
							System.Array.Copy(tmp, cPop, tmp.Length);
							cPop[cPop.Length - 1] = new ColorPopularity
							{
								amount = 1,
								key = ColorKeys[ColorKeys.Length - 1]
							};
						}
					}
				}
			}
		}
		// now we have to filter the best canidates 
		System.Array.Sort(cPop);
		// first we test if the captured amount of colors exceeds the desired amount
		if (cPop.Length > ColorArrayMaxSize)
		{
		//	Debug.Log("Doing the impossible");
			// length too great, getting most popular colors
			cPop = ColorPopularityClass.GetMostPopularColors(cPop, ColorArrayMaxSize);
		}
		ColorKeys = ColorPopularityClass.ColorPopularityArrayToCGradientArray(cPop);
		if(ColorKeys.Length / GradientDataClass.MAX_KEY_AMOUNT >= 2)
			// now we have to inject duplcates at the beginning of each color array (except the first one)
			for (int i = 1; i < ColorKeys.Length; i += GradientDataClass.MAX_KEY_AMOUNT)
				ValueInterpertation.gradientData.InsertColor((int)math.ceil(i / GradientDataClass.MAX_KEY_AMOUNT), i + GradientDataClass.MAX_KEY_AMOUNT - 1,ColorKeys[i]);
		
		// Debugging stuff... just leave it here
		//	foreach (ColorPopularity b in cPop)
		//		Debug.Log("BB" + b.key.color);
		//	foreach (CGradientColorKey a in ColorKeys)
		//		Debug.Log("AA" + a.color);
		//	CGradientColorKey[][] tmpa = GradientDataClass.SingleColorAraryTo2DColorArray(ColorKeys);
		//	for(int i = 0; i < tmpa.Length; i++)
		//		for (int j = 0; j < tmpa[i].Length; j++)
		//			Debug.Log("WW"+tmpa[i][j].color);

		ValueInterpertation.gradientData.ColorKeys = GradientDataClass.SingleColorAraryTo2DColorArray(ColorKeys);
		// reset the alphas
		ValueInterpertation.gradientData.AlphaKeys = new CGradientAlphaKey[ValueInterpertation.gradientData.ColorKeys.Length][];
		for(int i = 0; i < ValueInterpertation.gradientData.AlphaKeys.Length; i++)
		{
			ValueInterpertation.gradientData.AlphaKeys[i] = new CGradientAlphaKey[]
			{
				new CGradientAlphaKey
				{
					alpha = 1,
					time = 0
				},
				new CGradientAlphaKey
				{
					alpha = 1,
					time = 1
				}
			};
		}

		return ColorKeys.Length;
	}

	// use this as a default contructor
	public NoiseProfileOptions()
	{
		NoiseType = NoiseClass.NoiseType.ClassicPerlinNoise;
		ValueA = ValueB = ValueC = ValueD = 1;
		width = height = 64;
		length = depth = 1;
		ValueInterpertation = new ValueInterpertation(false);
		Is2D = true;
		Is3D = Is4D = false;
		Scale = 10f;
		UseScaleAsMax = true;
		MinMaxValue = new float2(1, Scale);
		MinMaxWidth = new int2(0, width);
		MinMaxHeight = new int2(0, height);
		MinMaxLength = new int2(0, length);
		MinMaxDepth = new int2(0, depth);
		invertColor = false;
		useAsMainTexture = false;
		profileMode = NoiseClass.NoiseProfileMode.Texture;

		UpdateTexture();
	}
	/// <summary>
	/// Updates the texture in this struct
	/// </summary>
	public void UpdateTexture()
	{
		// update the texture data (and i know these is some unnessary conversion but it makes it easier for you to understand)
		texture_data = NoiseClass.Texture2D_ToFloat3_2D(
			NoiseClass.GenerateTexture(
				NoiseType,
				new int4(MinMaxWidth.x, MinMaxHeight.x, MinMaxLength.x, MinMaxDepth.x),
				new int4(width, height, length, depth),
				crop,
				ValueInterpertation,
				Scale,
				new float4(ValueA, ValueB, ValueC, ValueD),
				Is2D ? 2 : Is3D ? 3 : Is4D ? 4 : 0
			)
		);
		// set the oldies
		OldNoiseType = NoiseType;
		oldValues = new float4(ValueA, ValueB, ValueC, ValueD);
		oldSize = new float4(width, height, length, depth);
		oldScale = Scale;
		oldValueInterpertation = ValueInterpertation;
		oldMode = profileMode;
		oldInvertColor = invertColor;
		oldUseAsMainTexture = useAsMainTexture;
		oldCrop = crop;
	}
	/// <summary>
	/// Checks to see if any values have changed
	/// </summary>
	/// <returns></returns>
	public bool ValuesChange()
	{
		if (ValueInterpertation.ValueChanged)
			return true;
		else if (ValueInterpertation.UpdateProfileOnEveryFrame)
			return true;
		else
			return !(
				// Test Values
				ValueA == oldValues.x && ValueB == oldValues.y && oldValues.z == ValueC && oldValues.w == ValueD
				// Test NoiseType
				&& NoiseType == OldNoiseType
				// Test Scale
				&& Scale == oldScale
				// Test for size changes
				&& oldSize.x == width && oldSize.y == height && oldSize.z == length && oldSize.w == depth
				// Test Value Interpertation
				&& ValueInterpertation.Equals(oldValueInterpertation)
				&& oldMode == profileMode
				&& invertColor == oldInvertColor
				&& useAsMainTexture == oldUseAsMainTexture
				&& crop.Equals(oldCrop)
			);
	}
	/// <summary>
	/// Copies this class values to another
	/// </summary>
	/// <param name="other"></param>
	public void CopyTo(ref NoiseProfileOptions other)
	{
		other.texture_data = texture_data;
		other.arithmetic_save = arithmetic_save;
		other.crop = crop;
		other.MinMaxDepth = MinMaxDepth;
		other.MinMaxLength = MinMaxLength;
		other.MinMaxHeight = MinMaxHeight;
		other.MinMaxWidth = MinMaxWidth;
		other.MinMaxValue = MinMaxValue;
		other.UseScaleAsMax = UseScaleAsMax;
		other.Scale = Scale;
		other.Is2D = Is2D;
		other.Is3D = Is3D;
		other.Is4D = Is4D;
		other.invertColor = invertColor;
		other.useAsMainTexture = useAsMainTexture;
		other.ValueA = ValueA;
		other.ValueB = ValueB;
		other.ValueC = ValueC;
		other.ValueD = ValueD;
		other.ValueInterpertation = ValueInterpertation;
		other.width = width;
		other.height = height;
		other.length = length;
		other.depth = depth;
		other.NoiseType = NoiseType;
		other.profileMode = profileMode;
	}
	
}

static class ColorPopularityClass {
	/// <summary>
	/// takes a color popularity array and returns the most popular colors with the length of the array being the given amountOfColors value
	/// </summary>
	/// <param name="popularity">a sorted array of ColorPopularity</param>
	/// <param name="amountOfColors">amont of colors you want returned from the organized array</param>
	/// <returns></returns>
	public static ColorPopularity[] GetMostPopularColors(ColorPopularity[] popularity, int amountOfColors = 8)
	{
		if (amountOfColors == 0 || amountOfColors < 0)
		{
			Debug.LogWarning("Returnning an empty Color Popularity!");
			return new ColorPopularity[0];
		}
		else if (amountOfColors == 1){
			return new ColorPopularity[] {
				new ColorPopularity
				{
					amount = 1,
					key = new CGradientColorKey
					{
						color = new float3(),
						time = 0
					}
				}
			};
		}
		else if (amountOfColors == 2)
		{
			return new ColorPopularity[] {
				new ColorPopularity
				{
					amount = 1,
					key = new CGradientColorKey
					{
						color = new float3(),
						time = 0
					}
				},
				new ColorPopularity
				{
					amount = 1,
					key = new CGradientColorKey
					{
						color = new float3(1,1,1),
						time = 1f
					}
				}
			};
		}
		else {
			/*
				Coding Explination
				decimalPlaces can be only be 1 or 2. if we choose 1 decimal place the we get a max of 10 values
				0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0
				but if we choose 2 decimal places we can get values from 0.00 to 1.00 which gives us 100 possible values.
				With all these values we need to determine which values from the range, 0.x to 0.y or 0.ax to 0.by, are the most popular which will be our resulting colors
			*/

			// sort first
			System.Array.Sort(popularity);


			// initialize the final array
			ColorPopularity[] cPop = new ColorPopularity[amountOfColors];
			// calculate the difference between height intervals
			float difference = 1f / amountOfColors;
			// determine if the difference denominator is even
			bool even = amountOfColors - 1 % 2 == 0;
			int popularityStartingIndex = 0;

			int index = 0;
			for(float currentHeight = 0; currentHeight <= 1f; currentHeight += difference)
			{
			//	Debug.Log("Color = "+popularity[index].key.color+"height = "+popularity[index].key.time);
				// store the current poplarity for testing
				ColorPopularity mostPopular = popularity[index];
				// goes through a sorted popularity array to determine the most popular color within currentHeigt - (currentHeight+difference)
				for(; popularityStartingIndex < popularity.Length; popularityStartingIndex++)
				{
					if(popularityStartingIndex == index)
					{
						// do nothing
					}
					else if (popularity[popularityStartingIndex].key.time <= currentHeight + difference)
					{
						// compare
						if (mostPopular.CompareTo(popularity[popularityStartingIndex]) == -1)
							mostPopular = popularity[popularityStartingIndex];
					}
					else
						break;
				}
				cPop[index] = mostPopular;
				if(index < cPop.Length-1)
					index++;
			}
		//	Debug.Log(cPop.Length+",,"+index);
		//	for(int i = 0; i < cPop.Length;i++)
		//		Debug.Log(cPop[i].key.color+","+cPop[i].key.time);
		//	Debug.Log("AAAAAAAAAAAAAAAAA");
			System.Array.Sort(cPop);
		//	for (int i = 0; i < cPop.Length; i++)
		//		Debug.Log(cPop[i].key.color + "," + cPop[i].key.time);
			return cPop;
		}
	}
	/// <summary>
	/// Converts a ColorPopularity To CGradientKey
	/// </summary>
	/// <param name="popularity"></param>
	/// <returns></returns>
	public static CGradientColorKey ColorPopularityToCGradientKey(ColorPopularity popularity)
	{
		return popularity.key;
	}
	/// <summary>
	/// Converts a ColorPopularityArray To CGradientArray
	/// </summary>
	/// <param name="popularity"></param>
	/// <returns></returns>
	public static CGradientColorKey[] ColorPopularityArrayToCGradientArray(ColorPopularity[] popularity) {
		CGradientColorKey[] keys = new CGradientColorKey[popularity.Length];
		for (int i = 0; i < keys.Length; i++)
			keys[i] = popularity[i].key;
		return keys;
	}
}

public struct ColorPopularity : System.IComparable
{
	public int amount;
	public CGradientColorKey key;

	public int CompareTo(object obj)
	{
		ColorPopularity other = (ColorPopularity)obj;
		if (amount == other.amount)
		{
			float GrayA = new Color(key.color.x, key.color.y, key.color.z).grayscale;
			float GrayB = new Color(other.key.color.x, other.key.color.y, other.key.color.z).grayscale;
			if (GrayA < GrayB)
				return -1;
			else if (GrayA > GrayB)
				return 1;
			else
				return 0;
		}
		else if (amount > other.amount)
			return 1;
		else
			return -1;
	}
}

/// <summary>
/// When used in an array it acts like a gradient wih a higher color range
/// </summary>
[System.Serializable]
public struct CustomColor
{
	public float Height;
	public float3 Color;
	public bool Enable;

	public override bool Equals(object obj)
	{
		CustomColor color = (CustomColor)obj;
		return (Height == color.Height && Color.Equals(color.Color) && Enable == color.Enable);
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}
}
[System.Serializable]
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

	// Value Interpertation v2.0
	// Here we will let the user decide what heights repersents what colors
	public CustomColor[] colors;
	public bool ValueChanged;
	public bool UseCutsomColors;

	public bool useGradientValue;
	//	public GradientKeys gradientKeys;
	public bool GradientEnabled;
	public int NumberOfdecimalPlaces;
	public GradientData gradientData;
	public int desiredAmountOfColors;
	public bool UpdateProfileOnEveryFrame;

	public ValueInterpertation(bool dumy)
	{
		ColorInterpertation1D = new bool3(false,false,false);
		ColorInterpertationA2D = new bool3(false,false,false);
		ColorInterpertationB2D = new bool3(false, false, false);
		ColorInterpertationA3D = new bool3(false, false, false);
		ColorInterpertationB3D = new bool3(false, false, false);
		ColorInterpertationC3D = new bool3(false, false, false);
		GradientEnabled = false;
		desiredAmountOfColors = 2; 
		//	gradientKeys = new GradientKeys();
		gradientData = new GradientData(new Gradient());
		useGradientValue = false;
		NumberOfdecimalPlaces = 1;
		UpdateProfileOnEveryFrame = false;
		colors = new CustomColor[0];
		UseCutsomColors = false;
		ValueChanged = false;
	}

	public void AddColor()
	{
		CustomColor[] tmp = colors;
		colors = new CustomColor[tmp.Length + 1];
		System.Array.Copy(tmp, colors, tmp.Length);
		colors[colors.Length - 1] = new CustomColor
		{
			Color = new float3(),
			Enable = true,
			Height = 1f
		};
	}

	public void RemoveColor(int index)
	{
		if (colors.Length > 0)
		{
			if (index >= 0 && index < colors.Length)
			{
				CustomColor[] tmp = colors;
				colors = new CustomColor[tmp.Length - 1];
				System.Array.Copy(tmp, colors, colors.Length);
			}
			else Debug.LogWarning("index out of range!");
		}
		else Debug.LogWarning("Cannot remove color from empty array");
	}

	public bool ChangeColorData(int index,float height,float3 color,bool enabled)
	{
		if (index < colors.Length && index >= 0)
		{
			colors[index] = new CustomColor
			{
				Color = color,
				Enable = enabled,
				Height = height
			};
			ValueChanged = true;
			return true;
		}
		return false;
	}

	public bool GetColorData(int index,out float height,out float3 color, out bool enabled)
	{
		if(index < colors.Length && index >= 0)
		{
			height = colors[index].Height;
			color = colors[index].Color;
			enabled = colors[index].Enable;
			return true;
		}
		height = 0;
		color = new float3();
		enabled = false;
		return false;
	}

	public void ResetColorData()
	{
		colors = new CustomColor[0];
	}
	/// <summary>
	/// returns the Dimension of a NoiseType for ValueInterpertation
	/// </summary>
	/// <param name="type"></param>
	/// <returns></returns>
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
		if(obj == null)
		{
			Debug.LogError("Given obj is null");
			return false;
		}
		ValueInterpertation vi = (ValueInterpertation)obj;
		return (
			ColorInterpertation1D.Equals(vi.ColorInterpertation1D)
			&& ColorInterpertationA2D.Equals(vi.ColorInterpertationA2D)
			&& ColorInterpertationB2D.Equals(vi.ColorInterpertationB2D)
			&& ColorInterpertationA3D.Equals(vi.ColorInterpertationA3D)
			&& ColorInterpertationB3D.Equals(vi.ColorInterpertationB3D)
			&& ColorInterpertationC3D.Equals(vi.ColorInterpertationC3D)
			&& GradientEnabled == vi.GradientEnabled
			&& desiredAmountOfColors == vi.desiredAmountOfColors
			&& colors.Equals(vi.colors)
			&& UseCutsomColors == vi.UseCutsomColors
			&& UpdateProfileOnEveryFrame == vi.UpdateProfileOnEveryFrame
			&& NumberOfdecimalPlaces == vi.NumberOfdecimalPlaces
			&& gradientData.Equals(vi.gradientData)
			&& useGradientValue == vi.useGradientValue
		);
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}
}
/// <summary>
/// GradientData was created because you can't Serialized a Gradient
/// </summary>
[System.Serializable]
public struct GradientData
{
	public CGradientAlphaKey[][] AlphaKeys;
	public CGradientColorKey[][] ColorKeys;
	public GradientMode GradientMode;
	/// <summary>
	/// Create a new "Gradient"
	/// </summary>
	/// <param name="gradient"></param>
	public GradientData(Gradient gradient)
	{
		AlphaKeys = new CGradientAlphaKey[1][];
		AlphaKeys[0] = new CGradientAlphaKey[1];
		AlphaKeys[0][0] = new CGradientAlphaKey
		{
			alpha = 1,
			time = 0
		};
		ColorKeys = new CGradientColorKey[1][];
		ColorKeys[0] = new CGradientColorKey[1];
		ColorKeys[0][0] = new CGradientColorKey
		{
			color = new float3(),
			time = 0
		};
		GradientMode = gradient.mode;
		SetGradient(0,gradient);
	}
	public bool InsertColor(int AIndex,int BIndex,CGradientColorKey key)
	{
		if(IndexValid(AIndex,ColorKeys.Length))
		{
			if (IndexValid(BIndex, ColorKeys[AIndex].Length))
			{
				//AAAAAAAAAAAAAAAAAAAAAAAAAAA
				int oldSize = ColorKeys.Length;
				BIndex = (BIndex * GradientDataClass.MAX_KEY_AMOUNT) + BIndex;
				CGradientColorKey[] keys = GradientDataClass.ColorToSingleArray(ColorKeys);
				CGradientColorKey[] newKeys = new CGradientColorKey[keys.Length+1];
				System.Array.Copy(keys, newKeys, BIndex);
				newKeys[BIndex] = key;
				System.Array.Copy(keys,BIndex+1,newKeys,1,newKeys.Length-BIndex);

				ColorKeys = GradientDataClass.SingleColorAraryTo2DColorArray(newKeys);
				int newSize = ColorKeys.Length;
				if (newSize > oldSize) {
					CGradientAlphaKey[][] ATmp = AlphaKeys;
					AlphaKeys = new CGradientAlphaKey[AlphaKeys.Length + 1][];
					for (int i = 0; i < ATmp.Length; i++)
						AlphaKeys[i] = ATmp[i];
					AlphaKeys[AlphaKeys.Length - 1] = new CGradientAlphaKey[] {
						new CGradientAlphaKey
						{
							alpha = 1,
							time = 0
						},	new CGradientAlphaKey
						{
							alpha = 1,
							time = 1
						}
					};

				}
				// now we have update and check


			}
			Debug.LogWarning("Given BIndex is invalid!");
			return false;
		}
		Debug.LogWarning("Given AIndex is invalid!");
		return false;
	}
	/// <summary>
	/// Gets the "Gradient" from the given index
	/// </summary>
	/// <param name="index"></param>
	/// <param name="gradient"></param>
	/// <returns>true is successful, false otherwise</returns>
	public bool GetGradient(int index,out Gradient gradient)
	{
		gradient = new Gradient();
		if (IndexValid(index,ColorKeys.Length))
		{
			gradient.SetKeys(GradientDataClass.ConvertCColorKeysToColorKeys(ColorKeys[index]), GradientDataClass.ConvertCAlphaKeysToAlphaKeys(AlphaKeys[index]));
			gradient.mode = GradientMode;
			return true;
		}
		return false;
	}
	public Gradient GetGradient(float time,out float newTime)
	{
	//	time = math.abs(time);
		// we need the difference of time int each ColorKey group
		float sets = GetGradientSets();
		if (sets == 1)
		{
			newTime = time;
			return GradientDataClass.CreateGradient(AlphaKeys[0], ColorKeys[0], GradientMode);
		}
		else
		{
			// we need to find the right Colorkeys
			// NOTE: time is between 0-1
			int i = 0;
			float timeDifferenceBetweenSets = 1 / sets;
			for(; i < sets; i++)
			{
				if(i * timeDifferenceBetweenSets <= time && (i*timeDifferenceBetweenSets)+timeDifferenceBetweenSets > time)
					break;
			}
			// now we have to remap the height so you can corectly evalute the time using the selected ColorKeys
			newTime = math.clamp(Mathf.Lerp(0, timeDifferenceBetweenSets, Mathf.InverseLerp(-1, 1, time)), -1, 1f);
			if (i == sets)
			{
				i--;
			//	Debug.Log("BUGOFF " + timeDifferenceBetweenSets + "," + (i * timeDifferenceBetweenSets) + "," + ((i * timeDifferenceBetweenSets) + timeDifferenceBetweenSets) + "," + time + "," + newTime + "," + i);

			}

			//	if(newTime > 0.2)

			return GradientDataClass.CreateGradient(AlphaKeys[i], ColorKeys[i], GradientMode);
		}
	}
	/// <summary>
	/// Returns the amount of Gradient Sets this gradient data requires
	/// </summary>
	/// <returns></returns>
	public int GetGradientSets()
	{
		return (int)math.ceil((float)ColorKeys.Length / GradientDataClass.MAX_KEY_AMOUNT);
	}
	/// <summary>
	/// Removes a gradient at the given index
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	public bool RemoveGradient(int index)
	{
		if (IndexValid(index,ColorKeys.Length))
		{
			CGradientAlphaKey[][] ATmp = AlphaKeys;
			AlphaKeys = new CGradientAlphaKey[ATmp.Length - 1][];
			int AnIndex = 0;
			for (int i = 0; i < ATmp.Length; i++)
			{
				if (i != index)
				{
					AlphaKeys[AnIndex] = ATmp[i];
					AnIndex++;
				}
			}

			CGradientColorKey[][] CTmp = ColorKeys;
			ColorKeys = new CGradientColorKey[CTmp.Length - 1][];
			AnIndex = 0;
			for (int i = 0; i < CTmp.Length; i++)
			{
				if (i != index)
				{
					ColorKeys[AnIndex] = CTmp[i];
					AnIndex++;
				}
			}


			return true;
		}
		return false;
	}
	/// <summary>
	/// Add a new default gradient
	/// </summary>
	public void AddGradient()
	{
		AddGradient(new CGradientAlphaKey[] {
			new CGradientAlphaKey
			{
				alpha = 1,
				time = 0
			},
			new CGradientAlphaKey
			{
				alpha = 1,
				time = 1
			}
		},new CGradientColorKey[] {
			new CGradientColorKey
			{
				color = new float3(),
				time = 0
			},
			new CGradientColorKey
			{
				color= new float3(1,1,1),
				time = 1
			}
		});
	}
	/// <summary>
	/// Adds a gradient to the end of the array using the given values
	/// </summary>
	/// <param name="AKeys"></param>
	/// <param name="CKeys"></param>
	public void AddGradient(CGradientAlphaKey[] AKeys,CGradientColorKey[] CKeys)
	{
		CGradientColorKey[][] CTmp = ColorKeys;
		ColorKeys = new CGradientColorKey[ColorKeys.Length + 1][];
		for(int i = 0; i < CTmp.Length; i++)
			ColorKeys[i] = CTmp[i];
		ColorKeys[ColorKeys.Length - 1] = CKeys;

		CGradientAlphaKey[][] ATmp = AlphaKeys;
		AlphaKeys = new CGradientAlphaKey[AlphaKeys.Length + 1][];
		for(int i = 0; i < ATmp.Length; i++)
			AlphaKeys[i] = ATmp[i];
		AlphaKeys[AlphaKeys.Length - 1] = AKeys;
	}
	/// <summary>
	/// Gets the "Gradient" to the given index
	/// </summary>
	/// <param name="index"></param>
	/// <param name="gradient"></param>
	/// <returns>true is successful, false otherwise</returns>
	public bool SetGradient(int index,Gradient gradient)
	{
		if (IndexValid(index, AlphaKeys.Length))
		{
			
			GradientMode = gradient.mode;
			AlphaKeys[index] = GradientDataClass.ConvertAlphaKeysToCAlphaKeys(gradient.alphaKeys);
			ColorKeys[index] = GradientDataClass.ConvertColorKeysToCColorKeys(gradient.colorKeys);

			// make sure the last of every index is the beginning of the next!
			if(index > 0)
			{
				AlphaKeys[index - 1][AlphaKeys[index-1].Length-1].alpha = gradient.alphaKeys[0].alpha;
				ColorKeys[index - 1][ColorKeys[index-1].Length-1].color = new float3(gradient.colorKeys[0].color.r,gradient.colorKeys[0].color.g,gradient.colorKeys[0].color.b);
			}
			if (index < AlphaKeys.Length-1)
			{
			//	Debug.Log(index + "," + AlphaKeys.Length + "," + ColorKeys.Length);
				if (AlphaKeys[index + 1] != null)
				{
					AlphaKeys[index + 1][0].alpha = gradient.alphaKeys[gradient.alphaKeys.Length - 1].alpha;
					ColorKeys[index + 1][0].color = new float3(gradient.colorKeys[gradient.colorKeys.Length - 1].color.r, gradient.colorKeys[gradient.colorKeys.Length - 1].color.g, gradient.colorKeys[gradient.colorKeys.Length - 1].color.b);
				}
			}

			return true;
		}
		return false;
	}


	/// <summary>
	/// determines if the given index is valid
	/// </summary>
	/// <param name="index"></param>
	/// <param name="length"></param>
	/// <returns></returns>
	private bool IndexValid(int index,int length)
	{
		return index > -1 && index < length;
	}
	// Overrides

	public override bool Equals(object obj)
	{
		if (obj == null) return false;
		GradientData gd = (GradientData)obj;
		if (AlphaKeys.Length != gd.AlphaKeys.Length)
			return false;
		else if (ColorKeys.Length != gd.AlphaKeys.Length)
			return false;
		else if (GradientMode != gd.GradientMode)
			return false;
		for (int i = 0; i < AlphaKeys.Length; i++) {
			if (AlphaKeys[i] != null && gd.AlphaKeys != null)
			{

				if (AlphaKeys[i].Length != gd.AlphaKeys[i].Length)
					return false;
				//else
				for (int j = 0; j < AlphaKeys[i].Length; j++)
					if (!AlphaKeys[i][j].Equals(gd.AlphaKeys[i][j]))
						return false;
			}
		}
		for (int i = 0; i < ColorKeys.Length; i++) {
			if (ColorKeys[i] != null && gd.ColorKeys != null)
			{
				if (ColorKeys[i].Length != gd.ColorKeys[i].Length)
					return false;
				//else
				for (int j = 0; j < ColorKeys[i].Length; j++)
					if (!ColorKeys[i][j].Equals(gd.ColorKeys[i][j]))
						return false;
			}
			else if (ColorKeys[i] != gd.ColorKeys[i])
				return false;
		}
		return true;
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}
}

public static class GradientDataClass
{
	public const int MAX_KEY_AMOUNT = 8;

	public static float Remap(float value,float start1,float stop1,float start2,float stop2)
	{
		return (start2 + (stop2 - start2)) * ((value - start1) / (stop1 - start1));
	}

	/// <summary>
	/// Creates a gradient based on the given values
	/// </summary>
	/// <param name="alphaKeys"></param>
	/// <param name="colorKeys"></param>
	/// <param name="mode"></param>
	/// <returns></returns>
	public static Gradient CreateGradient(CGradientAlphaKey[] alphaKeys,CGradientColorKey[] colorKeys,GradientMode mode)
	{
		Gradient gradient = new Gradient();
		gradient.SetKeys(ConvertCColorKeysToColorKeys(colorKeys), ConvertCAlphaKeysToAlphaKeys(alphaKeys));
		gradient.mode = mode;
		return gradient;
	}
	/// <summary>
	/// the name is the description
	/// </summary>
	/// <param name="keys"></param>
	/// <returns></returns>
	public static GradientAlphaKey[] ConvertCAlphaKeysToAlphaKeys(CGradientAlphaKey[] keys)
	{
		GradientAlphaKey[] tmp = new GradientAlphaKey[keys.Length];
		for (int i = 0; i < tmp.Length; i++)
			tmp[i] = new GradientAlphaKey(keys[i].alpha, keys[i].time);
		return tmp;
	}
	/// <summary>
	/// the name is the description
	/// </summary>
	/// <param name="keys"></param>
	/// <returns></returns>
	public static CGradientAlphaKey[] ConvertAlphaKeysToCAlphaKeys(GradientAlphaKey[] keys)
	{
		CGradientAlphaKey[] tmp = new CGradientAlphaKey[keys.Length];
		for (int i = 0; i < tmp.Length; i++)
			tmp[i] = new CGradientAlphaKey(keys[i].alpha, keys[i].time);
		return tmp;
	}
	/// <summary>
	/// the name is the description
	/// </summary>
	/// <param name="keys"></param>
	/// <returns></returns>
	public static GradientColorKey[] ConvertCColorKeysToColorKeys(CGradientColorKey[] keys)
	{
		GradientColorKey[] tmp = new GradientColorKey[keys.Length];
		for (int i = 0; i < tmp.Length; i++)
			tmp[i] = new GradientColorKey(keys[i].GetColor(), keys[i].time);
		return tmp;
	}
	/// <summary>
	/// the name is the description
	/// </summary>
	/// <param name="keys"></param>
	/// <returns></returns>
	public static CGradientColorKey[] ConvertColorKeysToCColorKeys(GradientColorKey[] keys)
	{
		CGradientColorKey[] tmp = new CGradientColorKey[keys.Length];
		for (int i = 0; i < tmp.Length; i++)
			tmp[i] = new CGradientColorKey(keys[i].color, keys[i].time);
		return tmp;
	}
	/// <summary>
	/// Converts the AlphaKey 2D array into a 1D array
	/// </summary>
	/// <returns></returns>
	public static CGradientAlphaKey[] AlphaToSingleArray(CGradientAlphaKey[][] AlphaKeys)
	{
		int lastSize = AlphaKeys[AlphaKeys.Length - 1].Length;
		CGradientAlphaKey[] tmp = new CGradientAlphaKey[MAX_KEY_AMOUNT * AlphaKeys.Length - (MAX_KEY_AMOUNT - lastSize)];
		for (int i = 0; i < AlphaKeys.Length; i++)
			for (int j = 0; j < AlphaKeys[i].Length; j++)
				tmp[(i * MAX_KEY_AMOUNT) + j] = AlphaKeys[i][j];
		return tmp;
	}
	/// <summary>
	/// Converts the ColorKey 2D array into a 1D array
	/// </summary>
	/// <returns></returns>
	public static CGradientColorKey[] ColorToSingleArray(CGradientColorKey[][] ColorKeys)
	{
		int lastSize = ColorKeys[ColorKeys.Length - 1].Length;
		CGradientColorKey[] tmp = new CGradientColorKey[MAX_KEY_AMOUNT * ColorKeys.Length - (MAX_KEY_AMOUNT - lastSize)];
		for (int i = 0; i < ColorKeys.Length; i++)
			for (int j = 0; j < ColorKeys[i].Length; j++)
				tmp[(i * MAX_KEY_AMOUNT) + j] = ColorKeys[i][j];
		return tmp;
	}
	/// <summary>
	/// Converts a 1D ColorKey array into a 2D one
	/// </summary>
	/// <param name="keys"></param>
	/// <returns></returns>
	public static CGradientColorKey[][] SingleColorAraryTo2DColorArray(CGradientColorKey[] keys)
	{
		int ArraySize = (int)math.ceil((float)keys.Length / MAX_KEY_AMOUNT);
		CGradientColorKey[][] tmp = new CGradientColorKey[ArraySize][];
		for (int i = 0; i < ArraySize; i++)
		{
			int currentSize = i == ArraySize - 1 ? keys.Length % MAX_KEY_AMOUNT : MAX_KEY_AMOUNT;
			currentSize = currentSize == 0 ? MAX_KEY_AMOUNT : currentSize;
		//	Debug.Log(currentSize+","+ ArraySize + ","+i);
			tmp[i] = new CGradientColorKey[currentSize];
			for (int j = 0; j < currentSize; j++)
				tmp[i][j] = keys[(i * MAX_KEY_AMOUNT) + j];
		}
		return tmp;
	}
	/// <summary>
	/// Converts a 1D AlphaKey array into a 2D one
	/// </summary>
	/// <param name="keys"></param>
	/// <returns></returns>
	public static CGradientAlphaKey[][] SingleAlphaAraryTo2DAlphaArray(CGradientAlphaKey[] keys)
	{
		int size = (int)math.ceil((float)keys.Length / MAX_KEY_AMOUNT);
		CGradientAlphaKey[][] tmp = new CGradientAlphaKey[size][];
		for (int i = 0; i < keys.Length; i++)
		{
			int currentSize = i == size - 1 ? keys.Length % MAX_KEY_AMOUNT : MAX_KEY_AMOUNT;
			currentSize = currentSize == 0 ? MAX_KEY_AMOUNT : currentSize;
			tmp[i] = new CGradientAlphaKey[currentSize];
			for (int j = 0; j < currentSize; j++)
				tmp[i][j] = keys[(i * MAX_KEY_AMOUNT) + j];
		}
		return tmp;
	}

}


/// <summary>
/// Settings for Visual Interpertation
/// </summary>
[System.Serializable]
public struct VisualInterpertationSettings
{
	public NoiseClass.VisualInterpertation visualInterpertation;
	public float MaxHeight;
	public float3 Scale;
	public PrimitiveType shape;
	public bool UseMaterialOnTerrain;
	public bool UseColorHeightsOnTerrain;

	public override bool Equals(object obj)
	{
		VisualInterpertationSettings ts = (VisualInterpertationSettings)obj;
		return MaxHeight == ts.MaxHeight
			&& Scale.Equals(ts.Scale)
			&& shape == ts.shape
			&& visualInterpertation == ts.visualInterpertation
			&& UseMaterialOnTerrain == ts.UseMaterialOnTerrain
			&& UseColorHeightsOnTerrain == ts.UseColorHeightsOnTerrain
			;
	}
	public override int GetHashCode()
	{
		return base.GetHashCode();
	}
}
/// <summary>
/// Crop Section
/// </summary>
[System.Serializable]
public struct CropSection
{
	public bool useCroppedSection;
	private int4 cropSection;
	public int offsetY;
	public int offsetX;
	/// <summary>
	/// calculates how much offset you can have based on the given inputs
	/// </summary>
	/// <param name="maxWidth"></param>
	/// <param name="maxHeight"></param>
	/// <returns>an int 4 of the format (-maxOffsetX/2,maxOddsetX/2,-maxOffsetY/2,maxOddsetY/2)</returns>
	public int4 CalculateOffsetBounds(int maxWidth,int maxHeight)
	{
		return new int4(-cropSection.x,maxWidth - cropSection.y, -cropSection.z, maxHeight - cropSection.w);
	}
	/// <summary>
	/// sets the crop section
	/// </summary>
	/// <param name="newSection"></param>
	public void SetCropSection(int4 newSection)
	{
		cropSection = newSection;
	}
	/// <summary>
	/// returns the crop section + the offset
	/// </summary>
	/// <returns></returns>
	public int4 GetCropSection()
	{
		return new int4(
			cropSection.x+offsetX,
			cropSection.y+offsetX,
			cropSection.z+offsetY,
			cropSection.w+offsetY
			);
	}
	/// <summary>
	/// returns the cropSection without the offset
	/// </summary>
	/// <returns></returns>
	public int4 GetOriginalCropSection()
	{
		return cropSection;
	}
	public override bool Equals(object obj)
	{
		CropSection cs = (CropSection)obj;
		return useCroppedSection == cs.useCroppedSection && cropSection.Equals(cs.cropSection) && offsetY == cs.offsetY && offsetX == cs.offsetX;
	}



	public override int GetHashCode()
	{
		return base.GetHashCode();
	}
}
/// <summary>
/// Have to make a copy of GradientAlphaKey since it doesn't have the serializable attribute on it
/// </summary>
[System.Serializable]
public struct CGradientAlphaKey
{
	//
	// Summary:
	//     Alpha channel of key.
	public float alpha;
	//
	// Summary:
	//     Time of the key (0 - 1).
	public float time;

	//
	// Summary:
	//     Gradient alpha key.
	//
	// Parameters:
	//   alpha:
	//     Alpha of key (0 - 1).
	//
	//   time:
	//     Time of the key (0 - 1).
	public CGradientAlphaKey(float _alpha, float _time)
	{
		alpha = _alpha;
		time = _time;
	}

	public override bool Equals(object obj)
	{
		CGradientAlphaKey key = (CGradientAlphaKey)obj;

		return alpha == key.alpha && time == key.time;
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}
}
/// <summary>
/// Have to make a copy of GradientColorKey since it doesn't have the serializable attribute on it
/// </summary>
[System.Serializable]
public struct CGradientColorKey {
	//
	// Summary:
	//     Color of key.
	public float3 color;
	//
	// Summary:
	//     Time of the key (0 - 1).
	public float time;

	//
	// Summary:
	//     Gradient color key.
	//
	// Parameters:
	//   color:
	//     Color of key.
	//
	//   time:
	//     Time of the key (0 - 1).
	//
	//   col:
	public CGradientColorKey(Color col, float _time)
	{
		color = new float3(col.r,col.g,col.b);
		time = _time;
	}
	public CGradientColorKey(float3 col, float _time)
	{
		color = col;
		time = _time;
	}

	public Color GetColor()
	{
		return new Color(color.x,color.y,color.z);
	}

	public override bool Equals(object obj)
	{
		CGradientColorKey key = (CGradientColorKey)obj;

		return color.Equals(key.color) && time == key.time;
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}
}