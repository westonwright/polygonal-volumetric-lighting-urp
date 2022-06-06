# Polygonal Volumetric Lighting for Universal Render Pipeline

This package can efficiently create local and global volumetric lighting effects using meshes generated in real time. The effects are highly tunable to reach your performance goals.

## Check the [Wiki](https://github.com/westonwright/polygonal-volumetric-lighting-urp/wiki) for more info!
* [Features](https://github.com/westonwright/polygonal-volumetric-lighting-urp/wiki/Features)
* [Roadmap](https://github.com/westonwright/polygonal-volumetric-lighting-urp/wiki/Roadmap)
* [Common Issues](https://github.com/westonwright/polygonal-volumetric-lighting-urp/wiki/Common-Issues)
* [Advanced Setup](https://github.com/westonwright/polygonal-volumetric-lighting-urp/wiki/Advanced-Setup)

## Supported Versions
Any version of Unity that supports Universal Render Pipeline version 12 and up (2021)

## Installation
To install this package, add [package](https://github.com/westonwright/polygonal-volumetric-lighting-urp.git) from git URL in Unity's package manager

Make sure you have [installed](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@12.0/manual/InstallURPIntoAProject.html) the Universal Render Pipeline to your project

## Setup
1. Make sure you have **Depth Textures** AND **Main Light Shadows** enabled on your *Universal Render Pipeline Asset*.
2. Open your *Universal Render Data Asset* and add a **Mesh Light Global Render Feature** or a **Mesh Light Zones Render Feature**. Check [Features](https://github.com/westonwright/polygonal-volumetric-lighting-urp/wiki/Features) for further explanation!
3. *(Skip if using **Mesh Light Global**)* Create a new **Light Volume Zone** in your scene by *right-clicking* in the hierarchy and selecting **Light > Light Volume Zone** (or create an empty **Game Object** and add the **Light Volume Zone** component). Scale Your light zone to cover the area you want. You can use multiple zones, but it might impact performance. 
4. Try changing the Render Feature settings on your *Universal Render Data Asset* to get the effect you want. For a more in depth explanation of each setting check out [Advanced Setup](https://github.com/westonwright/polygonal-volumetric-lighting-urp/wiki/Advanced-Setup)!

## Follow My Stuff :)
Nice to meet ya I'm Weston! Thanks for taking a look at my work

* [twitter](https://twitter.com/WestonWright_)

* [github](https://github.com/westonwright)

## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## License
[MIT](https://github.com/westonwright/polygonal-volumetric-lighting-urp/blob/main/LICENSE)
