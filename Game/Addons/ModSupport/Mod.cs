﻿// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2015 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Lypyl (lypyl@dfworkshop.net)
// Contributors:    
// 
// Notes:
//

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DaggerfallWorkshop.Game.Utility.ModSupport
{
    [Serializable]
    public class Mod
    {
        private bool isReady = false;
        private bool enabled = true;
        private int loadPriorty;
        private ModInfo modInfo;
        private AssetBundle assetBundle;                             //.dfmod file
        private string dirPath;                                      //directory the mod file is in
//        private IModController modController;

        private string[] assetNames;
        private List<Source> Sources;                                //any source code found in asset bundle
        private List<System.Reflection.Assembly> Assemblies;         //compiled source code for this mod
        private Dictionary<string, LoadedAsset> LoadedAssets;


        #region properties

        public string Name { get { return ModInfo.ModName; } private set { ModInfo.ModName = value; } }

        public string Title { get { return ModInfo.ModTitle; } private set { ModInfo.ModTitle = value; } }

        public bool IsReady
        {
            get { return isReady; }
            set { isReady = value; }
        }

        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }

        public int LoadPriority
        {
            get { return loadPriorty; }
            set { loadPriorty = value; }
        }

        public ModInfo ModInfo
        {
            get { return modInfo; }
            private set { modInfo = value; }
        }

        public AssetBundle AssetBundle
        {
            get { return assetBundle; }
            private set { assetBundle = value; }
        }

        public string DirPath
        {
            get { return dirPath; }
            private set { dirPath = value; }
        }

        public string[] AssetNames { get { return (assetNames != null) ? assetNames : GetAllAssetNames(); } }


        #endregion

        #region constructors
        public Mod()
        {
            LoadedAssets = new Dictionary<string, LoadedAsset>();
            Assemblies = new List<System.Reflection.Assembly>(1);
            Sources = new List<Source>(5);
        }

        public Mod(string name, string dirPath, AssetBundle ab)
        {
            LoadedAssets = new Dictionary<string, LoadedAsset>();
            Assemblies = new List<System.Reflection.Assembly>(1);
            Sources = new List<Source>(5);

            this.AssetBundle = ab;
            if (ab != null)
            {
                this.assetNames = GetAllAssetNames();
            }
            else
            {
                Debug.LogError("asset bundle is null for mod: " + name);
            }
            if(!LoadModInfoFromBundle())
            {
                Debug.LogError("Couldn't locate modinfo for mod: " + name);
                modInfo = new ModInfo();
            }

            this.Name = name;
            this.dirPath = dirPath;
            this.LoadSourceCodeFromModBundle();
        }

        #endregion

        /// <summary>
        /// If asset not already in LoadedAssets, will load asset from bundle and add to LoadedAssets
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetName"></param>
        /// <returns></returns>
        private T LoadAssetFromBundle<T>(string assetName) where T : UnityEngine.Object
        {
            LoadedAsset la = new LoadedAsset();

            if (assetBundle == null || string.IsNullOrEmpty(assetName))
            {
                return null;
            }

            try
            {
                assetName = ModManager.GetAssetName(assetName);

                if (LoadedAssets.ContainsKey(assetName))
                {
                    la = LoadedAssets[assetName];
                    return la.Obj as T;
                }
                else if (assetBundle.Contains(assetName))
                {
                    la.Obj = assetBundle.LoadAsset(assetName);
                    la.T = typeof(T);
                    if(this.modInfo != null && this.Name != null)
                        ModManager.OnLoadAsset(this.Name, assetName, typeof(T));
                }
                else
                    return null;

                if (la.Obj != null && la.T != null)
                {
                    LoadedAssets.Add(assetName, la);
                    return la.Obj as T;
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
                return null;
            }

        }

        /// <summary>
        /// Load asset from bundle and return
        /// </summary>
        /// <typeparam name="T">Type of Asset</typeparam>
        /// <param name="assetName">name of asset to load</param>
        /// <param name="clone">create copy of asset if true</param>
        /// <returns>UnityEngine.Object</returns>
        public T GetAssetFromLoadedBundle<T>(string assetName, bool clone = false) where T : UnityEngine.Object
        {
            T asset = LoadAssetFromBundle<T>(assetName);

            //if (!LoadAssetFromBundle<T>(assetName, out la))
            if(asset == null)
            {
                Debug.LogWarning(string.Format("Failed to load asset: {0}", assetName));
                return null;
            }
            else if(clone)
            {
                return UnityEngine.Object.Instantiate(asset) as T;
            }
            else
                return asset as T;
        }

        /// <summary>
        /// Loads all assets immediately from asset bundle and adds them to LoadedAssets
        /// </summary>
        /// <returns></returns>
        public bool LoadAllAssetsFromLoadedBundle()
        {
            if (assetBundle == null || AssetNames == null)
                return false;
            LoadedAsset la;
            for(int i = 0; i < AssetNames.Length; i++)
            {
                try
                {
                    string assetname = ModManager.GetAssetName(AssetNames[i]);
                    if (LoadedAssets.ContainsKey(assetname))
                        continue;

                    la = new LoadedAsset();
                    la.Obj = assetBundle.LoadAsset(assetname);
                    la.T = la.Obj.GetType();
                    LoadedAssets.Add(assetname, la);
                }
                catch(Exception ex)
                {
                    Debug.LogError(ex.Message);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Return Type from Assemblies using name of type
        /// </summary>
        /// <param name="type">name of Type</param>
        /// <returns>System.Type</returns>
        public Type GetCompiledType(string type)
        {
            Type t = null;

            if (string.IsNullOrEmpty(type))
                return null;

            for (int i = 0; i < Assemblies.Count; i++)
            {
                if (Assemblies[i] == null)
                    continue;

                t = Assemblies[i].GetType(type);
                if (t != null)
                    return t;
            }
            return t;
        }

        #region setup

        public string[] GetAllAssetNames()
        {
            try
            {
                if (assetBundle == null)
                    return null;

                string[] assetNames = assetBundle.GetAllAssetNames();
                for (int i = 0; i < assetNames.Length; i++)
                {
                    assetNames[i] = ModManager.GetAssetName(assetNames[i]);
                }

                return assetNames;
            }
            catch(Exception ex)
            {
                Debug.Log(ex.Message);
                return null;
            }
        }

        private bool LoadModInfoFromBundle()
        {
            try
            {
                TextAsset modInfoAsset = null;
                for (int i = 0; i < assetNames.Length; i++)
                {
                    if (assetNames[i].EndsWith(ModManager.MODINFOEXTENSION))
                    {
                        modInfoAsset = GetAssetFromLoadedBundle<TextAsset>(assetNames[i]);
                        break;
                    }
                }
                if (modInfoAsset == null)
                    return false;

                string modInfo = modInfoAsset.ToString();
                this.ModInfo = (ModInfo)JsonUtility.FromJson(modInfo, typeof(ModInfo));
                return ModInfo != null;
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
                return false;
            }
        }

        private bool LoadSourceCodeFromModBundle()
        {
            try
            {
                if (Sources == null)
                    Sources = new List<Source>();

                string[] assetNames = assetBundle.GetAllAssetNames();

                string name = null;
                foreach (string assetName in assetNames)
                {
                    //Debug.Log(string.Format("{0}", assetName));

                    name = assetName.ToLower();

                    if (name.EndsWith(".cs.txt") || name.EndsWith(".cs"))// || name.EndsWith(".byte")) //.byte is for .dll - not tested yet
                    {
                        Source newUncompiledSource;
                        TextAsset newSource = GetAssetFromLoadedBundle<TextAsset>(assetName);

                        if (newSource != null)
                        {
                            newUncompiledSource.sourceTxt = newSource;
                            newUncompiledSource.isPreCompiled = name.EndsWith(".dll");
                            Sources.Add(newUncompiledSource);
                        }
                    }
                }

                return true;
            }
            catch(Exception ex)
            {
                Debug.LogError(ex.Message);
                return false;
            }
        }

        public List<Assembly> CompileSourceToAssemblies()
        {
            List<string> stringSource   = new List<string>(Sources.Count);
            Assembly assembly           = null;

            try
            {
                for (int i = 0; i < Sources.Count; i++)
                {
                    if (Sources[i].isPreCompiled)
                    {
                        assembly = Assembly.Load(Sources[i].sourceTxt.bytes);
                        if (assembly != null)
                            Assemblies.Add(assembly);
                    }
                    else
                    {
                        stringSource.Add(Sources[i].sourceTxt.ToString());
                    }
                }
                if (stringSource.Count > 0)
                {
                    assembly = ModManager.CompileFromSourceAssets(stringSource.ToArray());

                    if (assembly != null)
                        Assemblies.Add(assembly);
                }

                stringSource    = null;
                Sources         = null;
                return Assemblies;
            }
            catch(Exception ex)
            {
                Debug.Log(ex);
                return null;
            }

        }

        public List<SetupOptions> FindModLoaders()
        {
            if (Assemblies == null || Assemblies.Count < 1)
                return null;

            List<SetupOptions> modLoaders = new List<SetupOptions>(1);

            for(int i = 0; i < Assemblies.Count; i++)
            {
                try
                {
                    //var loadableTypes = Compiler.GetLoadableTypes(Assemblies[i]);
                    //if (loadableTypes == null)
                        //continue;
                    //foreach (Type t in loadableTypes)

                    foreach(Type t in Assemblies[i].GetTypes())
                    {
                        if (!t.IsClass)
                            continue;

                        Load initAttribute = (Load)Attribute.GetCustomAttribute(t, typeof(Load));
                        if (initAttribute == null)
                            continue;
                        else
                        {
                            SetupOptions options = new SetupOptions();
                            options.mod = this;
                            options.targetName = (string.IsNullOrEmpty(initAttribute.targetName)) ? "Init" : initAttribute.targetName;
                            options.T = t;
                            modLoaders.Add(options);
                        }
                    }
                }
                catch(Exception ex)
                {
                    Debug.LogError(ex.Message);
                    continue;
                }

            }
            return modLoaders;
        }
        #endregion
    }

}
