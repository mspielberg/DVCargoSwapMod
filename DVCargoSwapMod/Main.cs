﻿using Harmony12;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace DVCargoSwapMod
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        // Container prefab names.
        public const string CONTAINER_PREFAB = "C_FlatcarContainer";
        public const string CONTAINER_AC = "AC";
        // C_FlatcarContainerAny
        public const string CONTAINER_ANY = CONTAINER_PREFAB + "Any";
        // C_FlatcarContainerOrange3a2
        public const string CONTAINER_MEDIUM = CONTAINER_PREFAB + "Orange3a2";
        // C_FlatcarContainerSunOmni
        public const string CONTAINER_A1_PREFAB = CONTAINER_PREFAB + "SunOmni";
        // C_FlatcarContainerSunOmniAC
        public const string CONTAINER_A1_AC_PREFAB = CONTAINER_A1_PREFAB + CONTAINER_AC;
        // Sure "Red" and "White" are also brands.
        public static readonly string[] CONTAINER_BRANDS =
        {
            "AAG", "Brohm", "Chemlek", "Goorsk", "Iskar", "Krugmann", "NeoGamma", "Novae", "NovaeOld", "Obco", "Red", "Sperex", "SunOmni", "Traeg", "White"
        };
        public const string DEFAULT_BRAND = "Default";
        public const string SKINS_FOLDER = "Skins";
        public const string SKINS_AC_FOLDER = SKINS_FOLDER + CONTAINER_AC;
        public static readonly string[] TEXTURE_TYPES = new string[] { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_EmissionMap" };

        // Names of container model prefabs.
        public static HashSet<string> containerPrefabs = new HashSet<string>();
        public static StringDictionary containerACPrefabs = new StringDictionary();
        // <container brand string, <new brand, is AC>>
        public static Dictionary<string, Dictionary<string, bool>> skinEntries = new Dictionary<string, Dictionary<string, bool>>();
        // <new brand, <texture name, texture>>
        public static Dictionary<string, Dictionary<string, Task<Texture2D>>> skinTextures = new Dictionary<string, Dictionary<string, Task<Texture2D>>>();

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            HarmonyInstance harmony = HarmonyInstance.Create(modEntry.Info.Id);
            // mod.Logger.Log("Made a HarmonyInstance.");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            // mod.Logger.Log("Patch successful.");

            foreach (string s in CONTAINER_BRANDS)
            {
                containerPrefabs.Add(CONTAINER_PREFAB + s);
                containerACPrefabs.Add(CONTAINER_PREFAB + s + CONTAINER_AC, CONTAINER_PREFAB + s);
            }

            LoadSkins(mod.Path + SKINS_FOLDER);
            LoadSkins(mod.Path + SKINS_AC_FOLDER, true);

            return true;
        }

        /// <summary>
        /// Damn I need to write a description here.
        /// </summary>
        /// <param name="mainDir"></param>
        /// <param name="containerAC"></param>
        static void LoadSkins(string mainDir, bool containerAC = false)
        {
            if (!Directory.Exists(mainDir))
                return;

            string[] skinPrefabPaths = Directory.GetDirectories(mainDir);
            
            // Traverse folders of skin prefab categories.
            foreach (string skinPrefabPath in skinPrefabPaths)
            {
                
                string skinPrefab = new DirectoryInfo(skinPrefabPath).Name;
                bool allContainers = skinPrefab.Equals(CONTAINER_ANY);

                string[] skinBrandPaths = Directory.GetDirectories(skinPrefabPath);

                if (!skinEntries.ContainsKey(skinPrefab))
                    skinEntries[skinPrefab] = new Dictionary<string, bool>();

                // Traverse all folders in skin prefab category.
                foreach (string brandNamePath in skinBrandPaths)
                {
                    string brandName = new DirectoryInfo(brandNamePath).Name;
                    string[] skinFilePaths = Directory.GetFiles(brandNamePath);

                    // Add brand entry to skin prefab list.
                    if (allContainers)
                    {
                        foreach (string containerPrefab in containerPrefabs)
                        {
                            if (!skinEntries.ContainsKey(containerPrefab))
                                skinEntries[containerPrefab] = new Dictionary<string, bool>();
                            // if (!skinEntries[containerPrefab].ContainsKey(brandName))
                            skinEntries[containerPrefab][brandName] = containerAC; // false;
                            // skinEntries[containerPrefab][brandName] = containerAC || skinEntries[containerPrefab][brandName];
                        }
                    }
                    else
                    {
                        // if (!skinEntries[skinPrefab].ContainsKey(brandName))
                        skinEntries[skinPrefab][brandName] = containerAC; // false;
                        // skinEntries[skinPrefab][brandName] = containerAC || skinEntries[skinPrefab][brandName];
                    }

                    // Don't read any file for default skin.
                    if (!brandName.Equals(DEFAULT_BRAND, StringComparison.OrdinalIgnoreCase))
                    {
                        // For all files in the folder in the skin prefab category.
                        foreach (string skinFilePath in skinFilePaths)
                        {
                            string fileName = Path.GetFileNameWithoutExtension(new FileInfo(skinFilePath).Name);
                            // TODO: Delete line if Altfuture fixes typo in file name.
                            fileName = fileName.Replace("ContainersAtlas_01", "ContainersAltas_01");

                            // Add texture file for brand entry.
                            if (!skinTextures.ContainsKey(brandName))
                                skinTextures[brandName] = new Dictionary<string, Task<Texture2D>>();
                            // Check if file already read for skin texture.
                            if (!skinTextures[brandName].ContainsKey(fileName))
                            {
                                // Read file
                                var skinTexture = TextureLoader.Add(new FileInfo(skinFilePath), false);
                                skinTextures[brandName][fileName] = skinTexture;
                            }
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(CargoModelController), "InstantiateCargoModel")]
    class CargoModelController_InstantiateCargoModel_Patch
    {

        /// <summary>
        /// Load the MD5 sum of the selected skin into __state. Use null if no change.
        /// </summary>
        /// <param name="cargoPrefabName"></param>
        /// <param name="cargoModel"></param>
        /// <param name="__state"></param>
        /// <returns></returns>
        static bool Prefix(ref string cargoPrefabName, out GameObject cargoModel, out string __state)
        {
            cargoModel = null;
            string normalizedCargoPrefab = cargoPrefabName;
            __state = null;

            // Normalize container prefab name.
            bool acswap = Main.containerACPrefabs.ContainsKey(cargoPrefabName);
            if (acswap)
                normalizedCargoPrefab = Main.containerACPrefabs[cargoPrefabName];

            // Check if there are any skin entries for prefab.
            if (!Main.skinEntries.ContainsKey(normalizedCargoPrefab))
                return true;

            Dictionary<string, bool> skinEntries = Main.skinEntries[normalizedCargoPrefab];
            List<string> skinNames = new List<string>(skinEntries.Keys);
            string skin;

            if (skinNames.Count > 0 && !(skin = skinNames[UnityEngine.Random.Range(0, skinNames.Count)]).Equals(Main.DEFAULT_BRAND, StringComparison.OrdinalIgnoreCase))
            {
                // We only need to swap out the prefab for normal containers.
                acswap = acswap || skinEntries[skin];
                if (acswap || Main.containerPrefabs.Contains(cargoPrefabName))
                    cargoPrefabName = acswap ? Main.CONTAINER_A1_AC_PREFAB : Main.CONTAINER_A1_PREFAB;
                // Set state to key in chosen skin.
                __state = skin;
            }

            return true;
        }

        private static readonly HashSet<Texture2D> appliedTextures = new HashSet<Texture2D>();

        /// <summary>
        /// Apply the skin texture.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="__state"></param>
        static void Postfix(CargoModelController __instance, string __state) // , TrainCar ___trainCar)
        {
            if (__state != null)
            {
                // Main.mod.Logger.Log(string.Format("Some cargo was loaded into a prefab named {0} on car {1}", __state, ___trainCar.logicCar.ID));
                GameObject cargoModel = __instance.GetCurrentCargoModel();
                MeshRenderer[] meshes = cargoModel.GetComponentsInChildren<MeshRenderer>();
                foreach (MeshRenderer m in meshes)
                {
                    if (m.material == null)
                        continue;

                    foreach (string t in Main.TEXTURE_TYPES)
                    {
                        if (!m.material.HasProperty(t))
                            continue;
                        Texture texture = m.material.GetTexture(t);
                        if (texture is Texture2D && Main.skinTextures[__state].ContainsKey(texture.name))
                        {
                            string name = texture.name;
                            Texture2D skinTexture = Main.skinTextures[__state][name].Result; 
                            if (!appliedTextures.Contains(skinTexture))
                            {
                                skinTexture.Apply(false, true);
                                appliedTextures.Add(skinTexture);
                            }
                            m.material.SetTexture(t, skinTexture);
                            if (skinTexture.height != skinTexture.width)
                                Main.mod.Logger.Warning($"The texture '{__state}/{name}' is not a square and may render incorrectly.");
                            else if (skinTexture.height != 8192)
                                Main.mod.Logger.Warning($"The texture '{__state}/{name}' is not 8192x8192 and may render incorrectly.");
                        }
                    }
                    // m.material.SetTexture("_MainTex", Main.testContainerSkin);
                }
            }
        }
    }
}
