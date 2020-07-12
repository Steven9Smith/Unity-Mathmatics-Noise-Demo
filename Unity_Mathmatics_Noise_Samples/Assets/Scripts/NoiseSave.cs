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

		public static string path = Application.persistentDataPath + "/NoiseSaves/";
		/// <summary>
		/// Checks if the directory exists and creates it if it doesn't
		/// </summary>
		/// <param name="dir">directory path</param>
		/// <returns></returns>
		private static bool CheckAndCreateDirectoryIfMissing(string dir)
		{
			if (Directory.Exists(dir))
				return true;
			Debug.Log("Creating Directory \"" + dir + "\"");
			Directory.CreateDirectory(dir);
			return false;
		}
		/// <summary>
		/// Saves the heightmap
		/// </summary>
		/// <param name="type"></param>
		/// <param name="filename"></param>
		/// <param name="mat"></param>
		/// <param name="useFileNameAsPath"></param>
		public static bool LoadNoiseProfiles(string root,out List<NoiseProfileOptions> noiseProfiles,out Texture2D mixedTexture)
		{
			noiseProfiles = new List<NoiseProfileOptions>();
			mixedTexture = new Texture2D(0,0);
			// we need to get the SubDirectores 
			string[] folders = Directory.GetDirectories(root);
			// check if the user used back slashes and fix accordingly
			if (root.Contains("\\"))
				root = root.Replace('\\', '/');
			// we have to determine if the user added a "/" at the end
			if (root[root.Length - 1] != '/')
				root += "/";
			// now we need the name of the noise profile from the path
			string[] split_path = root.Split('/');
			string name = split_path[split_path.Length - 2];
			// now we load the data
			if (folders.Length > 0)
			{
				Debug.Log("Detected folders");
				// detected a mixed type
				for(int i = 0;i < folders.Length; i++)
				{
					if (LoadNoiseProfileV2(root + "NoiseProfile"+i+"/NoiseProfile" + i + ".dat", out NoiseProfileOptions noiseProfile))
					{
						noiseProfiles.Add(noiseProfile);
					}
					else
					{
						Debug.LogWarning("Failed to load NoiseProfile" + i);
						noiseProfiles.Add(new NoiseProfileOptions());
					}
				}
				return true;
			}
			else
			{
				if (LoadNoiseProfileV2(root + name + ".dat",out NoiseProfileOptions noiseProfile))
				{
					noiseProfiles.Add(noiseProfile);
					return true;
				}
				else
					Debug.LogWarning("Failed to load \"" + name + ".dat\"");
			}
			return false;
		}

		/// <summary>
		/// remaps a value within a range to another range and returns the new value within the new range
		/// </summary>
		/// <param name="value">value to remap</param>
		/// <param name="low1">orignal min range</param>
		/// <param name="high1">original max range</param>
		/// <param name="low2">new min range</param>
		/// <param name="high2">new max range</param>
		/// <returns></returns>
		public static float Remap(this float value, float low1, float high1, float low2, float high2)
		{
			return low2 + (value - low1) * (high2 - low2) / (high1 - low1);
		}
		/// <summary>
		/// Export the NoiseProfile(s) to the Application.persistantData Folder
		/// </summary>
		/// <param name="type">NoiseProfile Type</param>
		/// <param name="filename"></param>
		/// <param name="noiseProfiles">List of Noise Profiles</param>
		/// <param name="mixedTexture">this is the mixed final texted is using arithmetic</param>
		/// <returns></returns>
		public static bool ExportNoiseProfiles(NoiseClass.NoiseType type, string filename, List<NoiseProfileOptions> noiseProfiles,Texture2D mixedTexture )
		{
			Debug.Log("Exporting Noise Profile...");
			string saveLocation = path + NoiseClass.Type2String(type) + "/" + filename + "/";
			if (!CheckAndCreateDirectoryIfMissing(path))
				ExportNoiseProfiles(type, filename,noiseProfiles,mixedTexture);
			else if (!CheckAndCreateDirectoryIfMissing(path + NoiseClass.Type2String(type)))
				ExportNoiseProfiles(type, filename, noiseProfiles, mixedTexture);
			else if (!CheckAndCreateDirectoryIfMissing(path + NoiseClass.Type2String(type) + "/" + filename))
				ExportNoiseProfiles(type, filename, noiseProfiles, mixedTexture);
			else
			{
				if (type == NoiseClass.NoiseType.Mixed)
				{
					// lets save the mixedTexture first
					Texture2D text = NoiseClass.CopyTexture2D(mixedTexture);
				//	SaveHeightMap(type, saveLocation + filename + ".map", mixedTexture, true);
					// Now we will save each noise profile and its values in seperate folders
					SaveNoiseProfiles(noiseProfiles, saveLocation);
					SavePng(text, saveLocation + filename + ".png");
				}
				else
				{
					int index = 0;
					// we need to find the enabled profile
					for (int i = 0; i < noiseProfiles.Count; i++)
						if (noiseProfiles[i].profileMode == NoiseClass.NoiseProfileMode.Texture)
						{
							index = i;
							break;
						}

					SaveNoiseProfileV2(saveLocation + filename + ".dat", noiseProfiles[index]);

					Texture2D text = noiseProfiles[index].GetTexture();
					SavePng(text, saveLocation + filename + ".png");
					Debug.Log("Successfully Exported Noise Profiles!");
				}
			}

			return false;
		}

		public static void SaveNoiseProfileV2(string filepath,NoiseProfileOptions noiseProfile)
		{
			BinaryFormatter bf = new BinaryFormatter();
			FileStream file = File.Create(filepath);
			bf.Serialize(file, noiseProfile);
			file.Close();
		}

		public static bool LoadNoiseProfileV2(string filepath,out NoiseProfileOptions noiseProfile)
		{
			if (File.Exists(filepath))
			{
				FileStream file = File.Open(filepath, FileMode.Open);
				BinaryFormatter bf = new BinaryFormatter();
				noiseProfile = (NoiseProfileOptions)bf.Deserialize(file);
				file.Close();
				return true;
			}
			Debug.LogWarning("Failed to load the NoiseSaveData file with path \""+filepath+"\"");
			noiseProfile = new NoiseProfileOptions();
			return false;
		}

		/// <summary>
		/// Save multiple Noise Profiles
		/// </summary>
		/// <param name="noiseProfiles">List of Noise Profiles</param>
		/// <param name="root">root folder to save at</param>
		private static void SaveNoiseProfiles(List<NoiseProfileOptions> noiseProfiles,string root)
		{
			CreateNoiseProfilesFolders(root, noiseProfiles.Count-1);
			for(int i = 0; i < noiseProfiles.Count; i++)
			{
				//	SaveHeightMap(noiseProfiles[i].NoiseType, root + "NoiseProfile" + i + "/NoiseProfile1.map", noiseProfiles[i].GetTexture(), true);
				/*	SaveNoiseProfile(noiseProfiles[i].NoiseType, root + "NoiseProfile" + i + "/NoiseProfile1.dat", noiseProfiles[i].GetTexture(), new float4(
							 noiseProfiles[i].ValueA,
							 noiseProfiles[i].ValueB,
							 noiseProfiles[i].ValueC,
							 noiseProfiles[i].ValueD
							 ), true);*/

				SaveNoiseProfileV2(root + "NoiseProfile"+i+"/NoiseProfile"+i+".dat", noiseProfiles[i]);

				Texture2D text = noiseProfiles[i].GetTexture();
				SavePng(text, root + "NoiseProfile" + i + "/NoiseProfile1.png");
			}
		}
		/// <summary>
		/// create the folders for the multiple noise profiles
		/// </summary>
		/// <param name="root">root directory</param>
		/// <param name="index_length"></param>
		private static void CreateNoiseProfilesFolders(string root,int index_length = 0)
		{
			if(index_length > -1)
			{
				CheckAndCreateDirectoryIfMissing(root + "NoiseProfile" + index_length);
				CreateNoiseProfilesFolders(root, index_length - 1);
			}
		}
		/// <summary>
		/// Saves the given texture as a png using the given filepath (filepath must contain .png)
		/// </summary>
		/// <param name="texture"></param>
		/// <param name="filepath"></param>
		public static void SavePng(Texture2D texture,string filepath)
		{
			byte[] pngBytes = texture.EncodeToPNG();
			File.WriteAllBytes(filepath, pngBytes);
		}
		/// <summary>
		/// Saves a float[][] heightmap
		/// </summary>
		/// <param name="texture"></param>
		/// <param name="filepath"></param>
		public static void SaveHeightMap(float[][] texture,string filepath)
		{
			BinaryFormatter bf = new BinaryFormatter();
			FileStream file = File.Create(filepath);
			bf.Serialize(file, texture);
			file.Close();
		}
		/// <summary>
		/// Saves a float[][] heightmap
		/// </summary>
		/// <param name="texture"></param>
		/// <param name="filepath"></param>
		public static void SaveHeightMap(float3[][] texture,string filepath)
		{
			float[][] tmp = new float[texture.Length][];
			for(int i = 0; i < texture.Length; i++)
			{
				tmp[i] = new float[texture[i].Length];
				for (int j = 0; j < texture[i].Length; j++)
					tmp[i][j] = new Color(texture[i][j].x, texture[i][j].y, texture[i][j].z).grayscale;
			}
			SaveHeightMap(tmp, filepath);
		}
	}
}
