using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using HarmonyLib;
using Unity;
using SharpConfig;
using System.Collections;
using System.IO;
using UnityEngine.Networking;

namespace MyNewPlugin
{
    [BepInPlugin("org.mcglowsticks.plugins.dinkumBetterDigSpotting", "Better Dig Spotting", "0.5.1")]
    public class Plugin : BaseUnityPlugin
    {

        private bool inSession;
        private bool stopSpam = false;

        private static List<GameObject> BuriedEntity = new List<GameObject>();
        private List<ParticleSystem> nearbyBuriedEntitys = new List<ParticleSystem>();
        private List<Vector2> unburiedItems = new List<Vector2>();
        private GameObject go;
        private Material particleMaterial;

        private bool announceScan = true;
        private GUIStyle textStyleAvailable = null;
        private GUIStyle textStyleUnAvailable = null;

        private bool showTimer = true;
        private bool enableTimerVisibility = true;
        private Vector3 playerPos;


        //Configuration Keybinds
        private string scanKey;
        private string hideTimerKey;
        private string resetKey;
        private bool deepWaterTreasure;
        private float renderDistance;
        private bool oldParticleStyle;
        private Color particleColour = Color.red;

        float countDown = 0.0f;

        private void Start()
        {
            // A simple particle material with no texture.
            particleMaterial = new Material(Shader.Find("Particles/Standard Unlit"));

            // Every 2 secs we will emit.
            InvokeRepeating("DoEmit", 2.0f, 2.0f);
        }

        private void Awake()
        {

            // Plugin startup logic
            Logger.LogInfo($"Plugin Better Dig Spotting is loaded!");

            string gameLocationS = Application.dataPath;
            char[] gameLocationC = gameLocationS.ToCharArray();
            int lastSlash = 0;
            for(int i = 0; i < gameLocationC.Length; i++)
            {
                if (gameLocationC[i] == '/')
                {
                    lastSlash = i;
                }
            }
            string trimmedLocation = gameLocationS.Remove(lastSlash);
            Logger.LogInfo("RUNNING FROM: " + trimmedLocation);
            
            var config = Configuration.LoadFromFile(trimmedLocation + "/BepInEx/plugins/mcglowsticks.dinkumbetterspotting.cfg");
            var section = config["General"];
            scanKey = section["keybind"].StringValue;
            hideTimerKey = section["hideTimerKey"].StringValue;
            resetKey = section["resetKey"].StringValue;
            renderDistance = section["renderDistance"].FloatValue;
            deepWaterTreasure = section["deepWater"].BoolValue;
            enableTimerVisibility = section["defaultOn"].BoolValue;
            oldParticleStyle = section["oldStyle"].BoolValue;
            ColorUtility.TryParseHtmlString("#" + section["particleColour"].StringValue, out particleColour);

        }

        private void Update()
        {
            
            if (NetworkMapSharer.share.localChar != null)
            {
                playerPos = NetworkMapSharer.share.localChar.transform.position;
                inSession = true;
            }
            else
            {
                inSession = false;
            }
            if (inSession && !stopSpam)
            {
                Logger.LogInfo("PLAYER LOADED!");
                
                Logger.LogInfo("BURIED THINGS!");
                locateBuriedItems();
                stopSpam = true;
                
            }

            if (inSession) {

                

                setTimerVisible(!Inventory.inv.isMenuOpen());
                countDown -= Time.deltaTime;
                if(countDown < -20)
                {
                    countDown = 0;
                }
                else if(countDown < 0 && !announceScan)
                {
                    announceScan = true;
                    
                    Logger.LogInfo("SCANNER READY");
                    NotificationManager.manage.createChatNotification("Scanner Ready!", false);
                }
                if (Input.GetKeyDown((KeyCode)System.Enum.Parse(typeof(KeyCode), scanKey)) && announceScan)
                {
                    locateBuriedItems();
                    announceScan = false;
                    countDown = 60.0f;
                }
                if (Input.GetKeyDown((KeyCode)System.Enum.Parse(typeof(KeyCode), hideTimerKey)))
                {
                    enableTimerVisibility = !enableTimerVisibility;
                }
                if(Input.GetKeyDown((KeyCode)System.Enum.Parse(typeof(KeyCode), resetKey)))
                {
                    foreach (ParticleSystem p in nearbyBuriedEntitys)
                    {
                        p.Pause();
                        p.Clear();
                    }
                    nearbyBuriedEntitys.Clear();
                }

            }

            if (inSession)
            {
                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    Vector3 vec = NetworkMapSharer.share.localChar.transform.position + NetworkMapSharer.share.localChar.transform.root.forward * 2f;
                    var xPos = (int)(Mathf.Round(vec.x + 0.5f) / 2f);
                    var zPos = (int)(Mathf.Round(vec.z + 0.5f) / 2f);
                    if (WorldManager.manageWorld.onTileMap[xPos, zPos] == 30)
                    {
                        Logger.LogInfo("FOUND BURIED ITEM!");

                        Vector2 buriedLocation = new Vector2(xPos, zPos);
                        Logger.LogInfo(buriedLocation);
                        unburiedItems.Add(buriedLocation);
                        Logger.LogInfo("ADDED OBJECT TO UNBURIED LOCATIONS");


                        foreach(GameObject g in BuriedEntity.ToList())
                        {

                            Vector3 gLocation = g.transform.position;
                            
                            var xPos2 = (int)(Mathf.Round(gLocation.x + 0.5f) / 2f);
                            var zPos2 = (int)(Mathf.Round(gLocation.z + 0.5f) / 2f);

                            if (xPos2 == xPos && zPos2 == zPos)
                            {
                                BuriedEntity.Remove(g);
                                Logger.LogInfo("REMOVED G-OBJECT");
                            }
                        }

                        foreach(ParticleSystem p in nearbyBuriedEntitys.ToList())
                        {
                            Vector3 pLocation = p.transform.position;
                            var xPos3 = (int)(Mathf.Round(pLocation.x + 0.5f) / 2f);
                            var zPos3 = (int)(Mathf.Round(pLocation.z + 0.5f) / 2f);

                            if (xPos3 == xPos && zPos3 == zPos)
                            {
                                p.Pause();
                                p.Clear();
                                nearbyBuriedEntitys.Remove(p);
                                Logger.LogInfo("REMOVED P-OBJECT");
                            }
                        }

                        

                    }

                }
            }

            
            if ( inSession && nearbyBuriedEntitys.Count > 0)
            {

                foreach (ParticleSystem p in nearbyBuriedEntitys)
                {
                    
                    if(Vector3.Distance(playerPos, p.transform.position) < renderDistance)
                    {
                        
                        if(p.isPaused)
                            p.Play();
                        
                    }
                    else if (Vector3.Distance(playerPos, p.transform.position) > renderDistance)
                    {
                        if (p.isPlaying)
                        {
                            p.Pause();
                            p.Clear();
                        }
                    }
                }

            }
        }

        void OnGUI()
        {
            initStyles();

            if (showTimer && enableTimerVisibility)
            {
                if (inSession)
                {
                    if (announceScan)
                    {
                        GUI.Box(new Rect((Screen.width / 2) - 75, (Screen.height - 200), 150, 30), "SCAN AVAILABLE!", textStyleAvailable);
                    }
                    else if (!announceScan)
                    {
                        GUI.Box(new Rect((Screen.width / 2) - 75, (Screen.height - 200), 160, 40), "SCAN UNAVAILABLE!\n" + Math.Round(countDown) + "s", textStyleUnAvailable);
                    }
                }
               }
           }


        private ParticleSystem makeNewParticle()
        {

            ParticleSystem newParticle = new ParticleSystem();

            var mainModule = newParticle.main;
            mainModule.startColor = particleColour;

            //mainModule.startSize = 0.15f;
            mainModule.startSize3D = true;
            mainModule.startSizeX = 0.1f;
            mainModule.startSizeY = .5f;
            mainModule.startSizeZ = .5f;
            mainModule.maxParticles = 7;
            mainModule.duration = 1f;
            mainModule.loop = true;
            mainModule.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 1.25f);
            //mainModule.startSpeed = 0.05f;

            mainModule.gravityModifier = 0.03f;


            var shapeModule = newParticle.shape;
            shapeModule.shapeType = ParticleSystemShapeType.Box;
            shapeModule.scale = new Vector3(1f, 1f, 1f);


            var emissionModule = newParticle.emission;
            emissionModule.rateOverTime = 5;

            var forceModule = newParticle.forceOverLifetime;
            forceModule.y = 1;

            var sizeLife = newParticle.sizeOverLifetime;
            sizeLife.x = new ParticleSystem.MinMaxCurve(0.0f, 1f);

            return newParticle;
        }

        private ParticleSystem makeOldParticle()
        {
            return null;
        }


        private void locateBuriedItems()
        {
            if (inSession)
            {
                
                Logger.LogInfo("Getting all objects!");
                BuriedEntity.Clear();
                BuriedEntity = UnityEngine.GameObject.FindObjectsOfType<GameObject>().Where<GameObject>(s => (s.name.ToLower().Contains("buried"))).ToList();

                //clear old list
                nearbyBuriedEntitys.Clear();
                int i = 0;

                Logger.LogInfo("FINDING OBJECTS!");

                foreach (GameObject g in BuriedEntity)
                {
                    Vector3 pos = g.transform.position;
                    var xPos = (int)(Mathf.Round(pos.x + 0.5f) / 2f);
                    var zPos = (int)(Mathf.Round(pos.z + 0.5f) / 2f);
                    UnityEngine.Vector2 xzPos = new Vector2(xPos, zPos);
                    if (unburiedItems.Contains(xzPos))
                    {
                        Logger.LogInfo("ITEM ALREDY DUG UP, SKIPPING!");
                    }
                    else
                    {

                        if (Vector3.Distance(playerPos, g.transform.position) < renderDistance && deepWaterTreasure)
                        {

                            // Create a yellow Particle System.
                            if (!oldParticleStyle)
                            {
                                go = new GameObject("ParticleSystem" + i);
                                i++;
                                go.transform.Rotate(-90, 0, 0); // Rotate so the system emits upwards.

                                var newParticle = go.AddComponent<ParticleSystem>();
                                go.GetComponent<ParticleSystemRenderer>().material = particleMaterial;
                                var mainModule = newParticle.main;
                                mainModule.startColor = particleColour;

                                //mainModule.startSize = 0.15f;
                                mainModule.startSize3D = true;
                                mainModule.startSizeX = 0.1f;
                                mainModule.startSizeY = .5f;
                                mainModule.startSizeZ = .5f;
                                mainModule.maxParticles = 7;
                                mainModule.duration = 1f;
                                mainModule.loop = true;
                                mainModule.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 1.25f);
                                //mainModule.startSpeed = 0.05f;

                                mainModule.gravityModifier = 0.03f;

                                var shapeModule = newParticle.shape;
                                shapeModule.shapeType = ParticleSystemShapeType.Box;
                                shapeModule.scale = new Vector3(1f, 1f, 1f);


                                var emissionModule = newParticle.emission;
                                emissionModule.rateOverTime = 5;

                                var forceModule = newParticle.forceOverLifetime;
                                forceModule.y = 1;

                                var sizeLife = newParticle.sizeOverLifetime;
                                sizeLife.x = new ParticleSystem.MinMaxCurve(0.0f, 1f);

                                newParticle.transform.position = new Vector3(g.transform.position.x, g.transform.position.y + 1f, g.transform.position.z);
                                //Logger.LogInfo("CREATED TEMP PARTICLE!");
                                newParticle.Play();
                                nearbyBuriedEntitys.Add(newParticle);
                                //Logger.LogInfo("ADDED PARTICLE!");
                            }
                            else if (oldParticleStyle)
                            {
                                go = new GameObject("ParticleSystem" + i);
                                i++;
                                go.transform.Rotate(-90, 0, 0); // Rotate so the system emits upwards.

                                var newParticle = go.AddComponent<ParticleSystem>();
                                go.GetComponent<ParticleSystemRenderer>().material = particleMaterial;
                                var mainModule = newParticle.main;
                                mainModule.startColor = particleColour;

                                //mainModule.startSize = 0.15f;
                                mainModule.startSize = 0.15f;
                                mainModule.duration = 1.5f;
                                mainModule.loop = true;
                                mainModule.startLifetime = 1.5f;
                                mainModule.startSpeed = 0.05f;
                                mainModule.gravityModifier = 0.07f;

                                var shapeModule = newParticle.shape;
                                shapeModule.angle = 48.48f;
                                shapeModule.radius = 0.58f;


                                var emissionModule = newParticle.emission;
                                emissionModule.rateOverTime = 0.5f;

                                newParticle.transform.position = new Vector3(g.transform.position.x, g.transform.position.y + 1f, g.transform.position.z);
                                //Logger.LogInfo("CREATED TEMP PARTICLE!");
                                newParticle.Play();
                                nearbyBuriedEntitys.Add(newParticle);
                            }
                        }
                        else if (Vector3.Distance(playerPos, g.transform.position) < renderDistance && !deepWaterTreasure)
                        {
                            if (g.transform.position.y > -1f)
                            {
                                if (!oldParticleStyle)
                                {
                                    // Create a yellow Particle System.
                                    go = new GameObject("ParticleSystem" + i);
                                    i++;
                                    go.transform.Rotate(-90, 0, 0); // Rotate so the system emits upwards.
                                    var newParticle = go.AddComponent<ParticleSystem>();

                                    go.GetComponent<ParticleSystemRenderer>().material = particleMaterial;

                                    var mainModule = newParticle.main;
                                    mainModule.startColor = particleColour;

                                    //mainModule.startSize = 0.15f;
                                    mainModule.startSize3D = true;
                                    mainModule.startSizeX = 0.1f;
                                    mainModule.startSizeY = .5f;
                                    mainModule.startSizeZ = .5f;
                                    mainModule.maxParticles = 7;
                                    mainModule.duration = 1f;
                                    mainModule.loop = true;
                                    mainModule.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 1.25f);
                                    //mainModule.startSpeed = 0.05f;

                                    mainModule.gravityModifier = 0.03f;


                                    var shapeModule = newParticle.shape;
                                    shapeModule.shapeType = ParticleSystemShapeType.Box;
                                    shapeModule.scale = new Vector3(1f, 1f, 1f);


                                    var emissionModule = newParticle.emission;
                                    emissionModule.rateOverTime = 5;

                                    var forceModule = newParticle.forceOverLifetime;
                                    forceModule.y = 1;

                                    var sizeLife = newParticle.sizeOverLifetime;
                                    sizeLife.x = new ParticleSystem.MinMaxCurve(0.0f, 1f);

                                    newParticle.transform.position = new Vector3(g.transform.position.x, g.transform.position.y + 1f, g.transform.position.z);
                                    //Logger.LogInfo("CREATED TEMP PARTICLE!");
                                    newParticle.Play();
                                    nearbyBuriedEntitys.Add(newParticle);
                                    //Logger.LogInfo("ADDED PARTICLE!");
                                }
                                else if (oldParticleStyle)
                                {
                                    go = new GameObject("ParticleSystem" + i);
                                    i++;
                                    go.transform.Rotate(-90, 0, 0); // Rotate so the system emits upwards.

                                    var newParticle = go.AddComponent<ParticleSystem>();
                                    go.GetComponent<ParticleSystemRenderer>().material = particleMaterial;
                                    var mainModule = newParticle.main;
                                    mainModule.startColor = particleColour;

                                    //mainModule.startSize = 0.15f;
                                    mainModule.startSize = 0.15f;
                                    mainModule.duration = 1.5f;
                                    mainModule.loop = true;
                                    mainModule.startLifetime = 1.5f;
                                    mainModule.startSpeed = 0.05f;
                                    mainModule.gravityModifier = 0.07f;

                                    var shapeModule = newParticle.shape;
                                    shapeModule.angle = 48.48f;
                                    shapeModule.radius = 0.58f;


                                    var emissionModule = newParticle.emission;
                                    emissionModule.rateOverTime = 0.5f;

                                    newParticle.transform.position = new Vector3(g.transform.position.x, g.transform.position.y + 1f, g.transform.position.z);
                                    //Logger.LogInfo("CREATED TEMP PARTICLE!");
                                    newParticle.Play();
                                    nearbyBuriedEntitys.Add(newParticle);
                                }
                            }
                        }
                    }

                }
            }

            Logger.LogInfo("All Objects gotten! (" + BuriedEntity.Count + ")");         
        }

        private void initStyles()
        {
            if (textStyleAvailable == null)
            {
                textStyleAvailable = new GUIStyle(GUI.skin.box);
                textStyleAvailable.fontSize = 15;
                textStyleAvailable.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.5f));
                textStyleAvailable.normal.textColor = Color.green;
            }
            if (textStyleUnAvailable == null)
            {
                textStyleUnAvailable = new GUIStyle(GUI.skin.box);
                textStyleUnAvailable.fontSize = 15;
                //textStyleUnAvailable.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.5f));
                textStyleUnAvailable.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.5f));
                textStyleUnAvailable.normal.textColor = Color.red;
            }
        }
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void setTimerVisible(bool s)
        {
            showTimer = s;
        }
    }
}

