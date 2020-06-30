using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor;

namespace Core.Procedural {
	public class NoiseSave : MonoBehaviour
	{
		// Start is called before the first frame update
		void Start()
		{

		}

		// Update is called once per frame
		void Update()
		{

		}
	}
	public static class NoiseSaveDataClass {

		private static string path = Application.persistentDataPath + "/NoiseSaves/";

		private static bool CheckAndCreateDirectoryIfMissing(string dir)
		{
			if (Directory.Exists(dir))
				return true;
			Debug.Log("Creating Directory \"" + dir + "\"");
			Directory.CreateDirectory(dir);
			return false;
		}

		public static void SaveHeightMap(NoiseClass.NoiseType type, string filename, Material mat, bool useFileNameAsPath = false)
		{
			// Verify Directory exists
			Debug.Log("saving Heights...");
			if (!useFileNameAsPath)
			{
				if (!CheckAndCreateDirectoryIfMissing(path))
					SaveHeightMap(type, filename, mat);
				else if (!CheckAndCreateDirectoryIfMissing(path + "/" + NoiseClass.Type2String(type)))
					SaveHeightMap(type, filename, mat);
				else if (!CheckAndCreateDirectoryIfMissing(path + "/" + NoiseClass.Type2String(type) + "/HeightMaps/"))
					SaveHeightMap(type, filename, mat);
				else
					PerformHeightSave(type, filename, mat, useFileNameAsPath);
			}
			else
				PerformHeightSave(type, filename, mat, useFileNameAsPath);
		}

		private static void PerformHeightSave(NoiseClass.NoiseType type, string filename, Material mat, bool useFileNameAsPath = false)
		{
			Texture2D texture = (Texture2D)mat.mainTexture;
			Color color;
			float value;
			float[][] values = new float[texture.width][];
			for (int i = 0; i < texture.width; i++)
			{
				values[i] = new float[texture.height];
				for (int j = 0; j < texture.height; j++)
				{
					color = texture.GetPixel(i, j);
					// This assumes that the image is black and white
					value = Remap(color.r, 0, 255f, 0f, 1f);
					values[i][j] = value;
				}
			}
			BinaryFormatter bf = new BinaryFormatter();
			FileStream file = File.Create(!useFileNameAsPath ? path + "/" + NoiseClass.Type2String(type) + "/HeightMaps/" + filename + ".map" : filename);
			bf.Serialize(file, values);
			file.Close();
			Debug.Log("Heights Saved!");
		}

		public static bool LoadHeightMap(string filePath,out float[][] heightMap)
		{
			if (File.Exists(filePath))
			{
				FileStream file = File.Open(filePath, FileMode.Open);

				BinaryFormatter bf = new BinaryFormatter();
				heightMap = (float[][])bf.Deserialize(file);
				file.Close();
				return true;
			}
			Debug.LogWarning("Failed to Load Height Map: failed to load file");
			heightMap = new float[0][];
			return false;
		}

		public static bool HeightMapToTexture2D(float[][] heightmap,out Texture2D texture)
		{
			if (heightmap.Length > 0)
			{
				texture = new Texture2D(heightmap.Length, heightmap[0].Length);
				for(int i = 0; i < heightmap.Length; i++)
				{
					for(int j = 0; j < heightmap[i].Length;j++)
					{
						//	Debug.Log(heightmap[i][j]);
						float colorValue = Remap(heightmap[i][j], 0, 1, 0, 255);
						texture.SetPixel(i,j,new Color(colorValue,colorValue, colorValue));
					}
				}
				texture.Apply();
				return true;
			}
			Debug.LogWarning("Failed to convert heightmap into a texture: length is 0");
			texture = new Texture2D(0, 0);
			return false;
		}

		public static void SaveNoise(NoiseClass.NoiseType type,string filename,Material mat,float4 inputValues, bool useFileNameAsPath = false)
		{
			Debug.Log("Saving Profile...");
			if (!useFileNameAsPath)
			{
				if (!CheckAndCreateDirectoryIfMissing(path))
					SaveNoise(type, filename, mat, inputValues);
				else if (!CheckAndCreateDirectoryIfMissing(path + "/" + NoiseClass.Type2String(type)))
					SaveNoise(type, filename, mat, inputValues);
				else
				
					PerformNoiseProfileSave(type, filename, mat, inputValues, useFileNameAsPath);
				
			}
			else
				PerformNoiseProfileSave(type, filename, mat, inputValues, useFileNameAsPath);

		}

		private static void PerformNoiseProfileSave(NoiseClass.NoiseType type, string filename, Material mat, float4 inputValues, bool useFileNameAsPath = false)
		{
			float3[][] colors = new float3[mat.mainTexture.width][];
			for (int i = 0; i < colors.Length; i++)
			{
				colors[i] = new float3[mat.mainTexture.height];
				for (int j = 0; j < colors[i].Length; j++)
				{
					Texture2D texture = (Texture2D)mat.mainTexture;
					Color color = texture.GetPixel(i, j);
					colors[i][j] = new float3(color.r, color.g, color.b);
				}
			}
			NoiseSaveData data = new NoiseSaveData
			{
				textureRGB = colors,
				Values = inputValues,
				Noisetype = (int)type
			};
			BinaryFormatter bf = new BinaryFormatter();
			FileStream file = File.Create(!useFileNameAsPath ? path + NoiseClass.Type2String(type) + "/" + filename + ".dat" : filename);
			bf.Serialize(file, data);
			file.Close();
			Debug.Log("Noise Profile Saved!");
		}

		public static bool LoadNoiseFile(string filepath,out NoiseSaveData nsd)
		{
			if (File.Exists(filepath))
			{
				FileStream file = File.Open(filepath, FileMode.Open);
				BinaryFormatter bf = new BinaryFormatter();
				nsd = (NoiseSaveData)bf.Deserialize(file);
				file.Close();
				return true;
			}
			Debug.LogWarning("Failed to load the NoiseSaveData file");
			nsd = new NoiseSaveData { };
			return false;
		}

		public static bool LoadNoise(string filePath, out Texture2D texture, out float4 inputValues, out NoiseClass.NoiseType type)
		{
			NoiseSaveData nsd = new NoiseSaveData { };
			if (LoadNoiseFile(filePath, out nsd))
			{
				inputValues = nsd.Values;
				type = (NoiseClass.NoiseType)nsd.Noisetype;
				int width = nsd.textureRGB.Length;
				if (width > 0)
				{
					int height = nsd.textureRGB[0].Length;
					if (height == 0)
						Debug.LogWarning("Read height is 0");
					texture = new Texture2D(width, height);
					for (int i = 0; i < width; i++)
					{
						for (int j = 0; j < height; j++)
						{
							Color color = new Color(nsd.textureRGB[i][j].x, nsd.textureRGB[i][j].y, nsd.textureRGB[i][j].z);
							texture.SetPixel(i, j, color);
						}
					}
					texture.Apply();
					return true;
				}
				else
					Debug.LogWarning("Read width of data is 0, cannot continue");
			}
			else
				Debug.LogWarning("Failed to load noise profile at \"" + filePath + "\"");

			texture = new Texture2D(0, 0);
			inputValues = new float4();
			type = 0;

			return false;

		}

		public static float Remap(this float value, float low1, float high1, float low2, float high2)
		{
			return low2 + (value - low1) * (high2 - low2) / (high1 - low1);
		}

		public static bool ExportNoiseProfile(NoiseClass.NoiseType type,string filename,Material mat,float4 inputValues)
		{
			Debug.Log("Exporting Noise Profile...");
			string saveLocation = path + NoiseClass.Type2String(type) + "/" + filename + "/";
			if (!CheckAndCreateDirectoryIfMissing(path))
				ExportNoiseProfile(type, filename, mat,inputValues);
			else if (!CheckAndCreateDirectoryIfMissing(path + NoiseClass.Type2String(type)))
				ExportNoiseProfile(type, filename, mat,inputValues);
			else if (!CheckAndCreateDirectoryIfMissing(path + NoiseClass.Type2String(type)+"/"+filename))
				ExportNoiseProfile(type, filename, mat,inputValues);
			else
			{
			//	Material material = new Material(mat);
			//	AssetDatabase.CreateAsset(material,"Assets/temp.mat");
				SaveHeightMap(type,saveLocation + filename + ".map", mat, true);
				SaveNoise(type, saveLocation + filename + ".dat", mat, inputValues, true);
				//	File.Copy(Application.dataPath+"/temp.mat", saveLocation + filename + ".mat", true);
				Texture2D text = (Texture2D)mat.mainTexture;
				byte[] pngBytes = text.EncodeToPNG();
				File.WriteAllBytes(saveLocation + filename + ".png", pngBytes);
				Debug.Log("Successfully Exported Noise Profiles!");
			}

			return false;
		}
	}
	[System.Serializable]
	public struct NoiseSaveData
	{
		public float3[][] textureRGB;
		public float4 Values;
		public int Noisetype;
	}
}
