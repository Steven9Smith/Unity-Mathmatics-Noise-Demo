using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

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
	public class NoiseSaveDataClass {
		void SaveNoise(NoiseClass.NoiseType type,string filename,Material mat,float4 values)
		{
			string path = "/Assets/NoiseSaves/" + NoiseClass.Type2String(type);
			if (File.Exists(path))
			{
				NoiseSaveData data = new NoiseSaveData {
					Mat = mat,
					Values = values,
					Noisetype = type
				};

				BinaryFormatter bf = new BinaryFormatter();
				FileStream file = File.Create(path+"/"+filename+".dat");
				bf.Serialize(file,data);

			}
			else
			{
				File.Create(path);
				SaveNoise(type,filename,mat,values);
			}
		}
		
		public bool LoadNoise(string filepath,ref NoiseSaveData nsd)
		{
			if (File.Exists(filepath))
			{
				FileStream file = File.Open(filepath, FileMode.Open);

				BinaryFormatter bf = new BinaryFormatter();
				nsd = (NoiseSaveData)bf.Deserialize(file);
				return true;
			}
			return false;
		}


	}
	[System.Serializable]
	public struct NoiseSaveData
	{
		public Material Mat;
		public float4 Values;
		public NoiseClass.NoiseType Noisetype;
	}
}
