# Unity-Mathmatics-Noise-Samples
 
This Demo Project was made to help create a visual aid on the Unity.Mathmatics noise functions in the Unity.Mathmatics package.

Users can modify various attributes to change the out of various noise functions.

Currently 3D and 4D methods are not visualized but are planned for a future release.

for more information on noise functions and how to use them take a look at this pdf.

http://weber.itn.liu.se/~stegu/simplexnoise/simplexnoise.pdf 

**Unity Mathmatics Noise page** 

https://github.com/Unity-Technologies/Unity.Mathematics/tree/master/src/Unity.Mathematics/Noise 

**With the source of the functions here:** 

https://github.com/stegu/webgl-noise

Here are some cool images i made, I plan on making a video in the future on how to use it.

![TestImage](/images/SRDNoise/TestImages.png)

Tutorial:

Before I go over let's go over some things.

This current version only supports 2D visual interpertation meaning the results are displayed on a Quad as a 2D Texture however the noise functions them selves go from
2D to 4D (please look at the pdf for more informatino on dimensions). 

![Tutorial_Image01](/images/SRDNoise/tutorial01.png)

The Texture uses the width and height choosen by the user and is passed to all noise functions. The length and depth are placeholder names for 3D and 4D noise functions inputs and are passed to the noise functions. Note: width,height,length,and depth are all for loops so with length and depth be careful on how big you numbers are because it will affect performace.

![Tutorial_Image02](/images/SRDNoise/tutorial02.png)

The NoiseType is the type of noise you would like to see. Use the dropdown menu to select the one you want.

![Tutorial_Image03](/images/SRDNoise/tutorial03.png)

Some NoiseTypes require additional parameters like a rotation value of gradient. You can modify these in the Gradients and Rotation Section

![Tutorial_Image04](/images/SRDNoise/tutorial04.png)

The **Scale** attribute is used as a zoom in feature (panning not availible yet) for the resulting noise function. These is a checkbox that allows you to set the max
**Gradient and Rotation"** values. (Usually when the **Gradient and Rotation** value surpasses the scale value, no visible change occurs). When this value is unchecked you can set the **Gradient and Rotation**'s min and max values.

![Tutorial_Image05](/images/SRDNoise/tutorial05.png)

**Value Interpertation** referes to how the return value of a noise function is interperted as on the 2D texture. Noise functions can return a <code>float</code>, <code>float2</code>, or <code>float3</code>.
The **Value Interpertation** allows you to choose how to represent the return values in rgb format. Note: you cannot have multiple return values represent an r,g, or b value at the same time. (Value Interperation **X Y Z** will be later changed to **R G B** respectivly)

![Tutorial_Image06](/images/SRDNoise/tutorial06.png)

You can also *Save*, *Load*, and *Export* the noise images and/or values!
Saving a noise profile is easy. 

All you have to do is write the filename and hit **Save Noise Profile**.

This will save the noise profile in a *.dat* file (check the *SaveNoiseDataClass* for more information) in your *Application.persistantDataPath* Directory under the corresponding *NoiseType* folder.

e.g.:
  (*Application.persistantDataPath*)\Perlin Noise\PerlinTest01.dat
  
![Tutorial_Image07](/images/SRDNoise/tutorial07.png)
  
If you hit the checkbox named **Save As Height Map** and then hit **Save Noise Profile** then it will save the map in the corresponding *NoiseType* folder under "HeightMaps".
e.g.:
  (*Application.persistantDataPath*)\Perlin Noise\HeightMaps\PerlinHeightMap.map
Note: the file is just a serialized <code>float[][]</code> (check the code on how to read the files in a script). Also be aware that the color of the converted image is assumed to be black and white and thus grabs only values from the x axis and remaps the values to 0-1, from 0-255.

![Tutorial_Image08](/images/SRDNoise/tutorial08.png)

Hitting the "Export Noise Profile" Button will create a folder, using the given filename as the name, in the corresponding NoiseType folder and then store a heightmap, NoiseProfile data, and png of the noise in that folder.


![Tutorial_Image09](/images/SRDNoise/tutorial09.png)

The Load section uses the given filepath and attempts to load the data from it and display the results on the Quad as a texture. this functionally is limited only to the .dat and .map files created by the "Save Noise Profile" and "Export Noise Profile" file outputs.

![Tutorial_Image10](/images/SRDNoise/tutorial10.png)

That's it with the tutorial for now.

Enjoy!



